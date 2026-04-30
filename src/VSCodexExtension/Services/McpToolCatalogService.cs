using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VSCodexExtension.Models;

namespace VSCodexExtension.Services
{
    public interface IMcpToolCatalogService
    {
        Task<IReadOnlyList<McpToolDefinition>> DiscoverToolsAsync(McpServerDefinition server);
        string BuildInvocationPrompt(McpServerDefinition server, McpToolDefinition tool);
    }

    public sealed class McpToolCatalogService : IMcpToolCatalogService
    {
        private readonly IMcpConfigService _config;
        private readonly Dictionary<string, IReadOnlyList<McpToolDefinition>> _cache = new Dictionary<string, IReadOnlyList<McpToolDefinition>>(StringComparer.OrdinalIgnoreCase);

        public McpToolCatalogService(IMcpConfigService config) => _config = config;

        public async Task<IReadOnlyList<McpToolDefinition>> DiscoverToolsAsync(McpServerDefinition server)
        {
            if (server == null || string.IsNullOrWhiteSpace(server.Name)) return Array.Empty<McpToolDefinition>();
            if (_cache.TryGetValue(server.Name, out var cached)) return cached;

            IReadOnlyList<McpToolDefinition> tools;
            try
            {
                tools = await ProbeServerToolsAsync(server).ConfigureAwait(false);
            }
            catch
            {
                tools = Array.Empty<McpToolDefinition>();
            }

            if (tools.Count == 0)
            {
                tools = new[]
                {
                    new McpToolDefinition
                    {
                        ServerName = server.Name,
                        Name = "invoke",
                        Description = "Use this MCP server by describing the desired tool/action and inputs for Codex to execute through the configured MCP server.",
                        InputFields = new List<McpToolInputField>
                        {
                            new McpToolInputField { Name = "request", Type = "string", Description = "Describe the MCP tool/action and required input.", IsRequired = true }
                        }
                    }
                };
            }

            _cache[server.Name] = tools;
            return tools;
        }

        public string BuildInvocationPrompt(McpServerDefinition server, McpToolDefinition tool)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"/MCP {server.Name} {tool.Name}");
            sb.AppendLine($"Use MCP server '{server.Name}' and tool '{tool.Name}'.");
            if (!string.IsNullOrWhiteSpace(tool.Description)) sb.AppendLine(tool.Description);
            foreach (var field in tool.InputFields)
            {
                var marker = field.IsRequired ? "" : " option";
                sb.AppendLine($"- {field.Name}{marker}: {field.Value}");
            }

            return sb.ToString().Trim();
        }

        private static async Task<IReadOnlyList<McpToolDefinition>> ProbeServerToolsAsync(McpServerDefinition server)
        {
            if (string.IsNullOrWhiteSpace(server.Command)) return Array.Empty<McpToolDefinition>();

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = server.Command,
                    Arguments = string.Join(" ", server.Args.Select(QuoteArg)),
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                foreach (var pair in server.Env)
                {
                    process.StartInfo.EnvironmentVariables[pair.Key] = pair.Value;
                }

                process.Start();
                WriteRpc(process.StandardInput, 1, "initialize", new JObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JObject(),
                    ["clientInfo"] = new JObject { ["name"] = "VSCodexExtension", ["version"] = "0.1.0" }
                });
                WriteNotification(process.StandardInput, "notifications/initialized", new JObject());
                WriteRpc(process.StandardInput, 2, "tools/list", new JObject());

                var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
                var lineTask = process.StandardOutput.ReadLineAsync();
                while (DateTimeOffset.UtcNow < deadline && !process.HasExited)
                {
                    var completed = await Task.WhenAny(lineTask, Task.Delay(TimeSpan.FromMilliseconds(500))).ConfigureAwait(false);
                    if (completed != lineTask) continue;
                    var line = await lineTask.ConfigureAwait(false);
                    if (line == null) break;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        lineTask = process.StandardOutput.ReadLineAsync();
                        continue;
                    }

                    var json = JObject.Parse(line);
                    if ((int?)json["id"] == 2)
                    {
                        return ParseTools(server.Name, json["result"]?["tools"] as JArray);
                    }

                    lineTask = process.StandardOutput.ReadLineAsync();
                }

                TryKill(process);
            }

            return Array.Empty<McpToolDefinition>();
        }

        private static IReadOnlyList<McpToolDefinition> ParseTools(string serverName, JArray? tools)
        {
            if (tools == null) return Array.Empty<McpToolDefinition>();
            var result = new List<McpToolDefinition>();
            foreach (var token in tools.OfType<JObject>())
            {
                var tool = new McpToolDefinition
                {
                    ServerName = serverName,
                    Name = (string?)token["name"] ?? string.Empty,
                    Description = (string?)token["description"] ?? string.Empty
                };
                var schema = token["inputSchema"] as JObject;
                var required = new HashSet<string>((schema?["required"] as JArray)?.Select(x => (string?)x).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>() ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                var properties = schema?["properties"] as JObject;
                if (properties != null)
                {
                    foreach (var property in properties.Properties())
                    {
                        var definition = property.Value as JObject;
                        tool.InputFields.Add(new McpToolInputField
                        {
                            Name = property.Name,
                            Type = (string?)definition?["type"] ?? "string",
                            Description = (string?)definition?["description"] ?? string.Empty,
                            IsRequired = required.Contains(property.Name)
                        });
                    }
                }
                result.Add(tool);
            }
            return result;
        }

        private static void WriteRpc(TextWriter writer, int id, string method, JObject parameters)
        {
            writer.WriteLine(JsonConvert.SerializeObject(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters
            }));
            writer.Flush();
        }

        private static void WriteNotification(TextWriter writer, string method, JObject parameters)
        {
            writer.WriteLine(JsonConvert.SerializeObject(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters
            }));
            writer.Flush();
        }

        private static string QuoteArg(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "\"\"";
            return value.Any(char.IsWhiteSpace) ? "\"" + value.Replace("\"", "\\\"") + "\"" : value;
        }

        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
        }
    }
}
