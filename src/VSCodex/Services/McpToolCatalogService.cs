// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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

/// <summary>Provides the mcp Tool Catalog Service implementation.</summary>
public sealed class McpToolCatalogService : IMcpToolCatalogService
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric10 = 10;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric15 = 15;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric2 = 2;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric30 = 30;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric5000 = 5000;

    /// <summary>Named string used by this type.</summary>
    private const string Repeated043Text = "0.4.3";

    /// <summary>Named string used by this type.</summary>
    private const string Repeated20241105Text = "2024-11-05";

    /// <summary>Named string used by this type.</summary>
    private const string VSCodexText = "VSCodex";

    /// <summary>Named string used by this type.</summary>
    private const string CapabilitiesText = "capabilities";

    /// <summary>Named string used by this type.</summary>
    private const string ClientInfoText = "clientInfo";

    /// <summary>Named string used by this type.</summary>
    private const string DotnetExeText = "dotnet.exe";

    /// <summary>Named string used by this type.</summary>
    private const string ErrorText = "error";

    /// <summary>Named string used by this type.</summary>
    private const string InitializeText = "initialize";

    /// <summary>Named string used by this type.</summary>
    private const string NotificationsInitializedText = "notifications/initialized";

    /// <summary>Named string used by this type.</summary>
    private const string ProtocolVersionText = "protocolVersion";

    /// <summary>Named string used by this type.</summary>
    private const string ResultText = "result";

    /// <summary>Named string used by this type.</summary>
    private const string VersionText = "version";

    /// <summary>Defines the probe Timeout Seconds.</summary>
    private const int ProbeTimeoutSeconds = 20;

    /// <summary>Provides testable access to the system clock.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores the cache.</summary>
    private readonly Dictionary<string, IReadOnlyList<McpToolDefinition>> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new instance of the <see cref="McpToolCatalogService"/> class.</summary>
    /// <param name="config">The config.</param>
    /// <param name="timeProvider">The clock used for request deadlines.</param>
    public McpToolCatalogService(IMcpConfigService config, TimeProvider timeProvider)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Performs the discover Tools operation.</summary>
    /// <param name="server">The server.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    public async Task<IReadOnlyList<McpToolDefinition>> DiscoverToolsAsync(McpServerDefinition server)
    {
        if (server is null || string.IsNullOrWhiteSpace(server.Name))
        {
            return [];
        }

        if (_cache.TryGetValue(server.Name, out var cached))
        {
            return cached;
        }

        IReadOnlyList<McpToolDefinition> tools;
        try
        {
            tools = await ProbeServerToolsAsync(server).ConfigureAwait(false);
        }
        catch
        {
            tools = [];
        }

        if (tools.Count > 0)
        {
            _cache[server.Name] = tools;
            return tools;
        }

        return [
            new McpToolDefinition
            {
                ServerName = server.Name,
                Name = "invoke",
                Description = "Use this MCP server by describing the desired tool/action and inputs for Codex to execute through the configured MCP server.",
                InputFields = { new McpToolInputField { Name = "request", Type = "string", Description = "Describe the MCP tool/action and required input.", IsRequired = true } }
            }
        ];
    }

    /// <summary>Builds invocation Prompt.</summary>
    /// <param name="server">The server.</param>
    /// <param name="tool">The tool.</param>
    /// <returns>The build Invocation Prompt result.</returns>
    public string BuildInvocationPrompt(McpServerDefinition server, McpToolDefinition tool)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine($"/MCP {server.Name} {tool.Name}");
        _ = sb.AppendLine($"Use MCP server '{server.Name}' and tool '{tool.Name}'.");
        if (!string.IsNullOrWhiteSpace(tool.Description))
        {
            _ = sb.AppendLine(tool.Description);
        }

        foreach (var field in tool.InputFields)
        {
            var marker = field.IsRequired ? string.Empty : " option";
            _ = sb.AppendLine($"- {field.Name}{marker}: {field.Value}");
        }

        return sb.ToString().Trim();
    }

    /// <summary>Performs the invoke Tool operation.</summary>
    /// <param name="server">The server.</param>
    /// <param name="toolName">The tool Name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    public async Task<JObject?> InvokeToolAsync(McpServerDefinition server, string toolName, JObject arguments)
    {
        if (server is null || string.IsNullOrWhiteSpace(server.Command) || string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        using var process = CreateServerProcess(server);
        StartServerProcess(process);
        var reader = new McpMessageReader(process.StandardOutput.BaseStream, _timeProvider);
        WriteRpc(process.StandardInput.BaseStream, 1, InitializeText, new JObject
        {
            [ProtocolVersionText] = Repeated20241105Text,
            [CapabilitiesText] = new JObject(),
            [ClientInfoText] = new JObject { ["name"] = VSCodexText, [VersionText] = Repeated043Text }
        });
        await ReadResponseAsync(process, reader, 1, TimeSpan.FromSeconds(Numeric10)).ConfigureAwait(false);
        WriteNotification(process.StandardInput.BaseStream, NotificationsInitializedText, new());
        WriteRpc(process.StandardInput.BaseStream, Numeric2, "tools/call", new JObject
        {
            ["name"] = toolName,
            [nameof(arguments)] = arguments ?? new JObject()
        });

        var response = await ReadResponseAsync(process, reader, Numeric2, TimeSpan.FromSeconds(Numeric15)).ConfigureAwait(false);
        TryKill(process);
        if (response?[ErrorText] is not null)
        {
            throw new InvalidOperationException(JsonConvert.SerializeObject(response[ErrorText]));
        }

        return (response?[ResultText] as JObject) ?? response;
    }

    /// <summary>Performs the invoke Tools operation.</summary>
    /// <param name="server">The server.</param>
    /// <param name="invocations">The invocations.</param>
    /// <param name="timeout">The timeout.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    public async Task<IReadOnlyList<JObject?>> InvokeToolsAsync(McpServerDefinition server, IReadOnlyList<McpToolInvocation> invocations, TimeSpan timeout)
    {
        if (server is null || string.IsNullOrWhiteSpace(server.Command) || invocations is null || invocations.Count == 0)
        {
            return [];
        }

        using var process = CreateServerProcess(server);
        StartServerProcess(process);
        var reader = new McpMessageReader(process.StandardOutput.BaseStream, _timeProvider);
        WriteRpc(process.StandardInput.BaseStream, 1, InitializeText, new JObject
        {
            [ProtocolVersionText] = Repeated20241105Text,
            [CapabilitiesText] = new JObject(),
            [ClientInfoText] = new JObject { ["name"] = VSCodexText, [VersionText] = Repeated043Text }
        });
        await ReadResponseAsync(process, reader, 1, TimeSpan.FromSeconds(Numeric10)).ConfigureAwait(false);
        WriteNotification(process.StandardInput.BaseStream, NotificationsInitializedText, new());

        var effectiveTimeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(Numeric30) : timeout;
        var responses = await InvokeToolBatchAsync(process, reader, invocations, effectiveTimeout).ConfigureAwait(false);
        TryKill(process);
        return responses;
    }

    /// <summary>Invokes a batch of MCP tools using an initialized server process.</summary>
    /// <param name="process">The initialized MCP server process.</param>
    /// <param name="reader">The MCP message reader.</param>
    /// <param name="invocations">The tool invocations.</param>
    /// <param name="timeout">The operation timeout.</param>
    /// <returns>The tool responses.</returns>
    private async Task<IReadOnlyList<JObject?>> InvokeToolBatchAsync(Process process, McpMessageReader reader, IReadOnlyList<McpToolInvocation> invocations, TimeSpan timeout)
    {
        var responses = new JObject?[invocations.Count];
        var deadline = _timeProvider.GetUtcNow().Add(timeout);
        for (var index = 0; index < invocations.Count && _timeProvider.GetUtcNow() < deadline && !process.HasExited; index++)
        {
            var id = index + Numeric2;
            WriteRpc(process.StandardInput.BaseStream, id, "tools/call", new JObject
            {
                ["name"] = invocations[index].ToolName,
                ["arguments"] = invocations[index].Arguments ?? new JObject()
            });
            var response = await ReadResponseAsync(process, reader, id, deadline - _timeProvider.GetUtcNow()).ConfigureAwait(false);
            if (response is null)
            {
                break;
            }

            responses[index] = response[ErrorText] is not null
                ? response
                : (response[ResultText] as JObject) ?? response;
        }

        return responses;
    }

    /// <summary>Performs the probe Server Tools operation.</summary>
    /// <param name="server">The server.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    private async Task<IReadOnlyList<McpToolDefinition>> ProbeServerToolsAsync(McpServerDefinition server)
    {
        if (string.IsNullOrWhiteSpace(server.Command))
        {
            return [];
        }

        using var process = CreateServerProcess(server);
        StartServerProcess(process);
        var reader = new McpMessageReader(process.StandardOutput.BaseStream, _timeProvider);
        WriteRpc(process.StandardInput.BaseStream, 1, InitializeText, new JObject
        {
            [ProtocolVersionText] = Repeated20241105Text,
            [CapabilitiesText] = new JObject(),
            [ClientInfoText] = new JObject { ["name"] = VSCodexText, [VersionText] = Repeated043Text }
        });
        await ReadResponseAsync(process, reader, 1, TimeSpan.FromSeconds(Numeric10)).ConfigureAwait(false);
        WriteNotification(process.StandardInput.BaseStream, NotificationsInitializedText, new());
        WriteRpc(process.StandardInput.BaseStream, Numeric2, "tools/list", new());

        var deadline = _timeProvider.GetUtcNow().AddSeconds(ProbeTimeoutSeconds);
        while (_timeProvider.GetUtcNow() < deadline && !process.HasExited)
        {
            var json = await reader.ReadAsync(process, deadline - _timeProvider.GetUtcNow()).ConfigureAwait(false);
            if (json is null)
            {
                continue;
            }

            if ((int?)json["id"] == Numeric2)
            {
                return ParseTools(server.Name, json[ResultText]?["tools"] as JArray);
            }
        }

        TryKill(process);

        return [];
    }

    /// <summary>Creates server Process.</summary>
    /// <param name="server">The server.</param>
    /// <returns>The create Server Process result.</returns>
    private Process CreateServerProcess(McpServerDefinition server)
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

    /// <summary>Starts server Process.</summary>
    /// <param name="process">The process.</param>
    private void StartServerProcess(Process process)
    {
        process.ErrorDataReceived += (_, __) => { };
        _ = process.Start();
        try
        {
            process.BeginErrorReadLine();
        }
        catch (InvalidOperationException exception)
        {
            Debug.WriteLine(exception);
        }
    }

    /// <summary>Creates server Start Info.</summary>
    /// <param name="command">The command.</param>
    /// <param name="args">The args.</param>
    /// <returns>The create Server Start Info result.</returns>
    private ProcessStartInfo CreateServerStartInfo(string command, IReadOnlyList<string> args)
    {
        var commandText = (command ?? string.Empty).Trim().Trim('"');
        if (IsDnxCommand(commandText))
        {
            return CreateRedirectedStartInfo(ResolveDotNetPath(), $"dnx{BuildArgumentSuffix(args)}");
        }

        var resolvedCommand = ResolveCommandPath(commandText) ?? commandText;
        var extension = Path.GetExtension(resolvedCommand);
        if (IsPowerShellScript(extension))
        {
            return CreateRedirectedStartInfo(
                ResolveCommandPath("powershell.exe") ?? "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File {QuoteArg(resolvedCommand)}{BuildArgumentSuffix(args)}");
        }

        if (IsBatchScript(extension))
        {
            var commandLine = QuoteArg(resolvedCommand) + BuildArgumentSuffix(args);
            return CreateRedirectedStartInfo(
                Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                $"/d /s /c {QuoteArg(commandLine)}");
        }

        return CreateRedirectedStartInfo(resolvedCommand, string.Join(" ", args.Select(QuoteArg)));
    }

    /// <summary>Determines whether a command uses the dnx launcher.</summary>
    /// <param name="command">The command.</param>
    /// <returns><see langword="true"/> when the command uses dnx.</returns>
    private bool IsDnxCommand(string command) => IsWindows() && string.Equals(command, "dnx", StringComparison.OrdinalIgnoreCase);

    /// <summary>Determines whether an extension identifies a PowerShell script.</summary>
    /// <param name="extension">The extension.</param>
    /// <returns><see langword="true"/> when the extension identifies a PowerShell script.</returns>
    private bool IsPowerShellScript(string extension) => IsWindows() && string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase);

    /// <summary>Determines whether an extension identifies a batch script.</summary>
    /// <param name="extension">The extension.</param>
    /// <returns><see langword="true"/> when the extension identifies a batch script.</returns>
    private bool IsBatchScript(string extension)
    {
        return IsWindows()
            && (string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Resolves dot Net Path.</summary>
    /// <returns>The resolve Dot Net Path result.</returns>
    private string ResolveDotNetPath()
    {
        var fromPath = ResolveCommandPath(DotnetExeText);
        if (!string.IsNullOrWhiteSpace(fromPath))
        {
            return fromPath!;
        }

        foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.Combine(root!, "dotnet", DotnetExeText);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return DotnetExeText;
    }

    /// <summary>Creates redirected Start Info.</summary>
    /// <param name="fileName">The file Name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The create Redirected Start Info result.</returns>
    private ProcessStartInfo CreateRedirectedStartInfo(string fileName, string arguments)
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

    /// <summary>Builds argument Suffix.</summary>
    /// <param name="args">The args.</param>
    /// <returns>The build Argument Suffix result.</returns>
    private string BuildArgumentSuffix(IEnumerable<string> args)
    {
        var text = string.Join(" ", (args ?? Array.Empty<string>()).Select(QuoteArg));
        return string.IsNullOrWhiteSpace(text) ? string.Empty : $" {text}";
    }

    /// <summary>Resolves command Path.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The resolve Command Path result.</returns>
    private string? ResolveCommandPath(string command)
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

    /// <summary>Gets executable Extensions.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The get Executable Extensions result.</returns>
    private IEnumerable<string> GetExecutableExtensions(string command)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(command)))
        {
            yield return string.Empty;
            yield break;
        }

        yield return string.Empty;
        if (!IsWindows())
        {
            yield break;
        }

        var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.PS1")
            .Split(';')
            .Where(x => !string.IsNullOrWhiteSpace(x));
        foreach (var extension in pathExt.Concat([".ps1"]).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return extension.StartsWith(".", StringComparison.Ordinal) ? extension : $".{extension}";
        }
    }

    /// <summary>Determines whether is Windows.</summary>
    /// <returns><see langword="true"/> when is Windows succeeds; otherwise, <see langword="false"/>.</returns>
    private bool IsWindows() => Path.DirectorySeparatorChar == '\\';

    /// <summary>Reads response.</summary>
    /// <param name="process">The process.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="id">The id.</param>
    /// <param name="timeout">The timeout.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    private async Task<JObject?> ReadResponseAsync(Process process, McpMessageReader reader, int id, TimeSpan timeout)
    {
        var deadline = _timeProvider.GetUtcNow().Add(timeout);
        while (_timeProvider.GetUtcNow() < deadline && !process.HasExited)
        {
            var json = await reader.ReadAsync(process, deadline - _timeProvider.GetUtcNow()).ConfigureAwait(false);
            if (json is null)
            {
                continue;
            }

            if ((int?)json[nameof(id)] == id)
            {
                return json;
            }
        }

        return null;
    }

    /// <summary>Parses tools.</summary>
    /// <param name="serverName">The server Name.</param>
    /// <param name="tools">The tools.</param>
    /// <returns>The parse Tools result.</returns>
    private IReadOnlyList<McpToolDefinition> ParseTools(string serverName, JArray? tools)
    {
        if (tools is null)
        {
            return [];
        }

        var result = new List<McpToolDefinition>();
        foreach (var token in tools.OfType<JObject>())
        {
            result.Add(CreateToolDefinition(serverName, token));
        }

        return result;
    }

    /// <summary>Creates a tool definition from an MCP tool token.</summary>
    /// <param name="serverName">The server name.</param>
    /// <param name="token">The MCP tool token.</param>
    /// <returns>The tool definition.</returns>
    private McpToolDefinition CreateToolDefinition(string serverName, JObject token)
    {
        var tool = new McpToolDefinition
        {
            ServerName = serverName,
            Name = (string?)token["name"] ?? string.Empty,
            Description = (string?)token["description"] ?? string.Empty
        };
        PopulateToolInputFields(tool, token["inputSchema"] as JObject);
        return tool;
    }

    /// <summary>Populates the tool input fields from a schema.</summary>
    /// <param name="tool">The tool definition.</param>
    /// <param name="schema">The input schema.</param>
    private void PopulateToolInputFields(McpToolDefinition tool, JObject? schema)
    {
        var requiredNames = GetRequiredFieldNames(schema);
        var properties = schema?[nameof(JObject.Properties).ToLowerInvariant()] as JObject;
        foreach (var property in properties?.Properties() ?? [])
        {
            var definition = property.Value as JObject;
            tool.InputFields.Add(new McpToolInputField
            {
                Name = property.Name,
                Type = (string?)definition?["type"] ?? "string",
                Description = (string?)definition?["description"] ?? string.Empty,
                IsRequired = requiredNames.Contains(property.Name)
            });
        }
    }

    /// <summary>Gets required input field names from a schema.</summary>
    /// <param name="schema">The input schema.</param>
    /// <returns>The required field names.</returns>
    private HashSet<string> GetRequiredFieldNames(JObject? schema)
    {
        var requiredTokens = schema?["required"] as JArray;
        return new(
            requiredTokens?.Select(x => (string?)x).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>()
            ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Writes rpc.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="id">The id.</param>
    /// <param name="method">The method.</param>
    /// <param name="parameters">The parameters.</param>
    private void WriteRpc(Stream stream, int id, string method, JObject parameters)
    {
        WriteMcpMessage(stream, new JObject
        {
            ["jsonrpc"] = "2.0",
            [nameof(id)] = id,
            [nameof(method)] = method,
            ["params"] = parameters
        });
    }

    /// <summary>Writes notification.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="method">The method.</param>
    /// <param name="parameters">The parameters.</param>
    private void WriteNotification(Stream stream, string method, JObject parameters)
    {
        WriteMcpMessage(stream, new JObject
        {
            ["jsonrpc"] = "2.0",
            [nameof(method)] = method,
            ["params"] = parameters
        });
    }

    /// <summary>Writes mcp Message.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="message">The message.</param>
    private void WriteMcpMessage(Stream stream, JObject message)
    {
        var json = JsonConvert.SerializeObject(message);
        var body = Encoding.UTF8.GetBytes(json + Environment.NewLine);
        stream.Write(body, 0, body.Length);
        stream.Flush();
    }

    /// <summary>Performs the quote Arg operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The quote Arg result.</returns>
    private string QuoteArg(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        return value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    }

    /// <summary>Attempts to kill.</summary>
    /// <param name="process">The process.</param>
    private void TryKill(Process process)
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
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Performs the kill Process Tree operation.</summary>
    /// <param name="processId">The process Id.</param>
    private void KillProcessTree(int processId)
    {
        try
        {
            using var killer = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                Arguments = $"/PID {processId.ToString(System.Globalization.CultureInfo.InvariantCulture)} /T /F",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            killer?.WaitForExit(Numeric5000);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (ArgumentException exception)
            {
                Debug.WriteLine(exception);
            }
        }
    }

    /// <summary>Provides the mcp Message Reader implementation.</summary>
    private sealed class McpMessageReader
    {
        /// <summary>Named number used by this type.</summary>
        private const int Numeric500 = 500;

        /// <summary>Stores the reader.</summary>
        private readonly StreamReader _reader;

        /// <summary>Provides testable access to the system clock.</summary>
        private readonly TimeProvider _timeProvider;

        /// <summary>Stores the line Task.</summary>
        private Task<string?>? _lineTask;

        /// <summary>Initializes a new instance of the <see cref="McpMessageReader"/> class.</summary>
        /// <param name="stream">The stream.</param>
        /// <param name="timeProvider">The clock used for read deadlines.</param>
        public McpMessageReader(Stream stream, TimeProvider timeProvider)
        {
            _reader = new(stream, Encoding.UTF8);
            _timeProvider = timeProvider;
        }

        /// <summary>Reads the operation.</summary>
        /// <param name="process">The process.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns>A task whose result contains the operation result.</returns>
        public async Task<JObject?> ReadAsync(Process process, TimeSpan timeout)
        {
            var deadline = _timeProvider.GetUtcNow().Add(timeout <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : timeout);
            while (_timeProvider.GetUtcNow() < deadline && !process.HasExited)
            {
                var remaining = deadline - _timeProvider.GetUtcNow();
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                var wait = remaining < TimeSpan.FromMilliseconds(Numeric500) ? remaining : TimeSpan.FromMilliseconds(Numeric500);
                _lineTask = _lineTask ?? _reader.ReadLineAsync();
                var completed = await Task.WhenAny(_lineTask, Task.Delay(wait)).ConfigureAwait(false);
                if (completed != _lineTask)
                {
                    continue;
                }

                var line = await _lineTask.ConfigureAwait(false);
                _lineTask = null;
                if (line is null)
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

        /// <summary>Attempts to read Json.</summary>
        /// <param name="text">The text.</param>
        /// <param name="json">The json.</param>
        /// <returns><see langword="true"/> when try Read Json succeeds; otherwise, <see langword="false"/>.</returns>
        private static bool TryReadJson(string text, out JObject json)
        {
            json = new();
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
    }
}
