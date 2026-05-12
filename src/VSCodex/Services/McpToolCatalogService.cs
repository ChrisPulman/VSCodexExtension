using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VSCodex.Models;

namespace VSCodex.Services;

public interface IMcpToolCatalogService
{
    Task<IReadOnlyList<McpToolDefinition>> DiscoverToolsAsync(McpServerDefinition server);
    Task<JObject?> InvokeToolAsync(McpServerDefinition server, string toolName, JObject arguments);
    Task<IReadOnlyList<JObject?>> InvokeToolsAsync(McpServerDefinition server, IReadOnlyList<McpToolInvocation> invocations, TimeSpan timeout);
    string BuildInvocationPrompt(McpServerDefinition server, McpToolDefinition tool);
}

public sealed class McpToolCatalogService : IMcpToolCatalogService
{
    private const int ProbeTimeoutSeconds = 20;
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

        if (tools.Count > 0)
        {
            _cache[server.Name] = tools;
            return tools;
        }

        return new[]
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

    public async Task<JObject?> InvokeToolAsync(McpServerDefinition server, string toolName, JObject arguments)
    {
        if (server == null || string.IsNullOrWhiteSpace(server.Command) || string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        using (var process = CreateServerProcess(server))
        {
            StartServerProcess(process);
            var reader = new McpMessageReader(process.StandardOutput.BaseStream);
            WriteRpc(process.StandardInput.BaseStream, 1, "initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject { ["name"] = "VSCodex", ["version"] = "0.3.0" }
            });
            await ReadResponseAsync(process, reader, 1, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            WriteNotification(process.StandardInput.BaseStream, "notifications/initialized", new JObject());
            WriteRpc(process.StandardInput.BaseStream, 2, "tools/call", new JObject
            {
                ["name"] = toolName,
                ["arguments"] = arguments ?? new JObject()
            });

            var response = await ReadResponseAsync(process, reader, 2, TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            TryKill(process);
            if (response?["error"] != null)
            {
                throw new InvalidOperationException(JsonConvert.SerializeObject(response["error"]));
            }

            return response?["result"] as JObject ?? response;
        }
    }

    public async Task<IReadOnlyList<JObject?>> InvokeToolsAsync(McpServerDefinition server, IReadOnlyList<McpToolInvocation> invocations, TimeSpan timeout)
    {
        if (server == null || string.IsNullOrWhiteSpace(server.Command) || invocations == null || invocations.Count == 0)
        {
            return Array.Empty<JObject?>();
        }

        using (var process = CreateServerProcess(server))
        {
            StartServerProcess(process);
            var reader = new McpMessageReader(process.StandardOutput.BaseStream);
            WriteRpc(process.StandardInput.BaseStream, 1, "initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject { ["name"] = "VSCodex", ["version"] = "0.3.0" }
            });
            await ReadResponseAsync(process, reader, 1, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            WriteNotification(process.StandardInput.BaseStream, "notifications/initialized", new JObject());

            var responses = new JObject?[invocations.Count];
            var deadline = DateTimeOffset.UtcNow.Add(timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : timeout);
            for (var index = 0; index < invocations.Count && DateTimeOffset.UtcNow < deadline && !process.HasExited; index++)
            {
                var id = index + 2;
                WriteRpc(process.StandardInput.BaseStream, id, "tools/call", new JObject
                {
                    ["name"] = invocations[index].ToolName,
                    ["arguments"] = invocations[index].Arguments ?? new JObject()
                });
                var response = await ReadResponseAsync(process, reader, id, deadline - DateTimeOffset.UtcNow).ConfigureAwait(false);
                if (response == null)
                {
                    break;
                }

                responses[index] = response["error"] != null
                    ? response
                    : response["result"] as JObject ?? response;
            }

            TryKill(process);
            return responses;
        }
    }

    private static async Task<IReadOnlyList<McpToolDefinition>> ProbeServerToolsAsync(McpServerDefinition server)
    {
        if (string.IsNullOrWhiteSpace(server.Command)) return Array.Empty<McpToolDefinition>();

        using (var process = CreateServerProcess(server))
        {
            StartServerProcess(process);
            var reader = new McpMessageReader(process.StandardOutput.BaseStream);
            WriteRpc(process.StandardInput.BaseStream, 1, "initialize", new JObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JObject(),
                ["clientInfo"] = new JObject { ["name"] = "VSCodex", ["version"] = "0.1.0" }
            });
            await ReadResponseAsync(process, reader, 1, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            WriteNotification(process.StandardInput.BaseStream, "notifications/initialized", new JObject());
            WriteRpc(process.StandardInput.BaseStream, 2, "tools/list", new JObject());

            var deadline = DateTimeOffset.UtcNow.AddSeconds(ProbeTimeoutSeconds);
            while (DateTimeOffset.UtcNow < deadline && !process.HasExited)
            {
                var json = await reader.ReadAsync(process, deadline - DateTimeOffset.UtcNow).ConfigureAwait(false);
                if (json == null)
                {
                    continue;
                }

                if ((int?)json["id"] == 2)
                {
                    return ParseTools(server.Name, json["result"]?["tools"] as JArray);
                }
            }

            TryKill(process);
        }

        return Array.Empty<McpToolDefinition>();
    }

    private static Process CreateServerProcess(McpServerDefinition server)
    {
        var startInfo = CreateServerStartInfo(server.Command, server.Args);
        var process = new Process
        {
            StartInfo = startInfo
        };

        foreach (var pair in server.Env)
        {
            process.StartInfo.EnvironmentVariables[pair.Key] = pair.Value;
        }

        return process;
    }

    private static void StartServerProcess(Process process)
    {
        process.ErrorDataReceived += (_, __) => { };
        process.Start();
        try
        {
            process.BeginErrorReadLine();
        }
        catch
        {
        }
    }

    private static ProcessStartInfo CreateServerStartInfo(string command, IReadOnlyList<string> args)
    {
        var commandText = (command ?? string.Empty).Trim().Trim('"');
        if (IsWindows() && string.Equals(commandText, "dnx", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRedirectedStartInfo(ResolveDotNetPath(), "dnx" + BuildArgumentSuffix(args));
        }

        var resolvedCommand = ResolveCommandPath(commandText) ?? commandText;
        var extension = Path.GetExtension(resolvedCommand);
        if (IsWindows() && string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRedirectedStartInfo(
                ResolveCommandPath("powershell.exe") ?? "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -File " + QuoteArg(resolvedCommand) + BuildArgumentSuffix(args));
        }

        if (IsWindows() && (string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)))
        {
            var commandLine = QuoteArg(resolvedCommand) + BuildArgumentSuffix(args);
            return CreateRedirectedStartInfo(
                Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                "/d /s /c " + QuoteArg(commandLine));
        }

        return CreateRedirectedStartInfo(resolvedCommand, string.Join(" ", args.Select(QuoteArg)));
    }

    private static string ResolveDotNetPath()
    {
        var fromPath = ResolveCommandPath("dotnet.exe");
        if (!string.IsNullOrWhiteSpace(fromPath))
        {
            return fromPath!;
        }

        foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.Combine(root!, "dotnet", "dotnet.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "dotnet.exe";
    }

    private static ProcessStartInfo CreateRedirectedStartInfo(string fileName, string arguments)
        => new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

    private static string BuildArgumentSuffix(IEnumerable<string> args)
    {
        var text = string.Join(" ", (args ?? Array.Empty<string>()).Select(QuoteArg));
        return string.IsNullOrWhiteSpace(text) ? string.Empty : " " + text;
    }

    private static string? ResolveCommandPath(string command)
    {
        var value = Environment.ExpandEnvironmentVariables((command ?? string.Empty).Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Path.IsPathRooted(value))
        {
            return File.Exists(value) ? value : null;
        }

        var extensions = GetExecutableExtensions(value).ToArray();
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory.Trim().Trim('"'), value + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetExecutableExtensions(string command)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(command)))
        {
            yield return string.Empty;
            yield break;
        }

        yield return string.Empty;
        if (IsWindows())
        {
            var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.PS1")
                .Split(';')
                .Where(x => !string.IsNullOrWhiteSpace(x));
            foreach (var extension in pathExt.Concat(new[] { ".ps1" }).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
            }
        }
    }

    private static bool IsWindows() => Path.DirectorySeparatorChar == '\\';

    private static async Task<JObject?> ReadResponseAsync(Process process, McpMessageReader reader, int id, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline && !process.HasExited)
        {
            var json = await reader.ReadAsync(process, deadline - DateTimeOffset.UtcNow).ConfigureAwait(false);
            if (json == null)
            {
                continue;
            }

            if ((int?)json["id"] == id)
            {
                return json;
            }
        }

        return null;
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

    private static void WriteRpc(Stream stream, int id, string method, JObject parameters)
    {
        WriteMcpMessage(stream, new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters
        });
    }

    private static void WriteNotification(Stream stream, string method, JObject parameters)
    {
        WriteMcpMessage(stream, new JObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = parameters
        });
    }

    private static void WriteMcpMessage(Stream stream, JObject message)
    {
        var json = JsonConvert.SerializeObject(message);
        var body = Encoding.UTF8.GetBytes(json + Environment.NewLine);
        stream.Write(body, 0, body.Length);
        stream.Flush();
    }

    private static string QuoteArg(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "\"\"";
        return value.Any(char.IsWhiteSpace) ? "\"" + value.Replace("\"", "\\\"") + "\"" : value;
    }

    private static bool TryReadJson(string text, out JObject json)
    {
        json = new JObject();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            json = JObject.Parse(text);
            return true;
        }
        catch (JsonReaderException)
        {
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            if (IsWindows())
            {
                KillProcessTree(process.Id);
                return;
            }

            process.Kill();
        }
        catch
        {
        }
    }

    private static void KillProcessTree(int processId)
    {
        try
        {
            using (var killer = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = "/PID " + processId.ToString(System.Globalization.CultureInfo.InvariantCulture) + " /T /F",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }))
            {
                killer?.WaitForExit(5000);
            }
        }
        catch
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }
    }

    private sealed class McpMessageReader
    {
        private readonly StreamReader _reader;
        private Task<string?>? _lineTask;

        public McpMessageReader(Stream stream) => _reader = new StreamReader(stream, Encoding.UTF8);

        public async Task<JObject?> ReadAsync(Process process, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.UtcNow.Add(timeout <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : timeout);
            while (DateTimeOffset.UtcNow < deadline && !process.HasExited)
            {
                var remaining = deadline - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var wait = remaining < TimeSpan.FromMilliseconds(500) ? remaining : TimeSpan.FromMilliseconds(500);
                _lineTask = _lineTask ?? _reader.ReadLineAsync();
                var completed = await Task.WhenAny(_lineTask, Task.Delay(wait)).ConfigureAwait(false);
                if (completed != _lineTask)
                {
                    continue;
                }

                var line = await _lineTask.ConfigureAwait(false);
                _lineTask = null;
                if (line == null)
                {
                    break;
                }

                if (TryReadJson(line, out var json))
                {
                    return json;
                }
            }

            return null;
        }
    }
}
