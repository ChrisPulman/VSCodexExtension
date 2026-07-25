// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the mcp Config Service implementation.</summary>
public sealed class McpConfigService : IMcpConfigService
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric2 = 2;

    /// <summary>Named string used by this type.</summary>
    private const string CPReactiveMemoryMCPServerDllText = "CP.ReactiveMemory.MCP.Server.dll";

    /// <summary>Named string used by this type.</summary>
    private const string ReactiveMemoryText = "ReactiveMemory";

    /// <summary>Named string used by this type.</summary>
    private const string CpReactivememoryMcpServerText = "cp-reactivememory-mcp-server";

    /// <summary>Named string used by this type.</summary>
    private const string ReactivememoryText = "reactivememory";

    /// <summary>Defines the preferred Reactive Memory Server Name.</summary>
    private const string PreferredReactiveMemoryServerName = CpReactivememoryMcpServerText;

    /// <summary>Defines the legacy Reactive Memory Server Name.</summary>
    private const string LegacyReactiveMemoryServerName = ReactivememoryText;

    /// <summary>Matches MCP server section headers.</summary>
    private static readonly Regex McpServerHeaderRegex = new("^\\[mcp_servers\\.([^\\]]+)\\]", RegexOptions.Compiled);

    /// <summary>Matches MCP server sections.</summary>
    private static readonly Regex McpServerSectionsRegex = new("(?ms)^\\s*\\[mcp_servers\\.[^\\]]+\\].*?(?=^\\s*\\[|\\z)", RegexOptions.Compiled);

    /// <summary>Matches TOML arrays.</summary>
    private static readonly Regex TomlArrayRegex = new("\\[(.*)\\]", RegexOptions.Compiled);

    /// <summary>Matches TOML string values.</summary>
    private static readonly Regex TomlStringRegex = new("\"([^\"]*)\"", RegexOptions.Compiled);

    /// <summary>Matches disabled legacy MCP server entries.</summary>
    private static readonly Regex LegacyEnabledRegex = new("(?im)^\\s*enabled\\s*=\\s*false\\s*$", RegexOptions.Compiled);

    /// <summary>Matches enabled legacy MCP server entries.</summary>
    private static readonly Regex LegacyEnabledAssignmentRegex = new("(?ims)(^\\s*\\[mcp_servers\\.reactivememory\\].*?^\\s*enabled\\s*=\\s*)true(\\s*$)", RegexOptions.Compiled);

    /// <summary>Stores the servers.</summary>
    private readonly BehaviorSubject<IReadOnlyList<McpServerDefinition>> _servers = new([]);

    /// <summary>Gets the servers.</summary>
    public IObservable<IReadOnlyList<McpServerDefinition>> Servers => _servers.AsObservable();

    /// <summary>Gets the snapshot.</summary>
    public IReadOnlyList<McpServerDefinition> Snapshot => _servers.Value;

    /// <summary>Refreshes the operation.</summary>
    public void Refresh()
    {
        string userCodexConfig = LocalPaths.UserCodexConfig;
        EnsureReactiveMemoryDefault(userCodexConfig);
        List<McpServerDefinition> list = ParseServers(File.ReadAllLines(userCodexConfig));
        MarkRequiredServers(list);
        _servers.OnNext(list);
    }

    /// <summary>Saves the operation.</summary>
    /// <param name="servers">The servers.</param>
    public void Save(IEnumerable<McpServerDefinition> servers) => SaveCore(servers);

    /// <summary>Creates template.</summary>
    /// <param name="transportType">The transport Type.</param>
    /// <returns>The create Template result.</returns>
    public McpServerDefinition CreateTemplate(string transportType) => CreateTemplateCore(transportType);

    /// <summary>Parses MCP server definitions from configuration lines.</summary>
    /// <param name="lines">The configuration lines.</param>
    /// <returns>The parsed server definitions.</returns>
    private List<McpServerDefinition> ParseServers(IEnumerable<string> lines)
    {
        List<McpServerDefinition> list = [];
        McpServerDefinition? current = null;
        foreach (string sourceLine in lines)
        {
            string line = sourceLine.Trim();
            Match header = McpServerHeaderRegex.Match(line);
            if (header.Success)
            {
                current = new McpServerDefinition
                {
                    Name = header.Groups[1].Value
                };
                list.Add(current);
            }
            else
            {
                ApplyServerProperty(current, line);
            }
        }

        return list;
    }

    /// <summary>Applies a TOML property to an MCP server definition.</summary>
    /// <param name="server">The server to update.</param>
    /// <param name="line">The TOML line.</param>
    private void ApplyServerProperty(McpServerDefinition? server, string line)
    {
        if (server is null || line.StartsWith("#", StringComparison.Ordinal) || !line.Contains('='))
        {
            return;
        }

        string[] parts = line.Split(['='], Numeric2);
        string key = parts[0].Trim();
        string value = parts[1].Trim().Trim('"');
        if (key.Equals("command", StringComparison.OrdinalIgnoreCase))
        {
            server.Command = value;
        }
        else if (key.Equals("url", StringComparison.OrdinalIgnoreCase))
        {
            server.Url = value;
            server.TransportType = "url";
        }
        else if (key.Equals("args", StringComparison.OrdinalIgnoreCase))
        {
            server.Args.Clear();
            server.Args.AddRange(ParseArray(parts[1]));
        }
        else if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase))
        {
            server.IsEnabled = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Marks required MCP servers.</summary>
    /// <param name="list">The configured servers.</param>
    private void MarkRequiredServers(IEnumerable<McpServerDefinition> list)
    {
        foreach (McpServerDefinition server in list)
        {
            server.ArgumentsText = string.Join(Environment.NewLine, server.Args);
            if (server.Name.Equals(CpReactivememoryMcpServerText, StringComparison.OrdinalIgnoreCase))
            {
                server.IsRequired = true;
                server.IsEnabled = true;
                server.Health = "required";
            }
        }
    }

    /// <summary>Saves the operation.</summary>
    /// <param name="servers">The servers.</param>
    private void SaveCore(IEnumerable<McpServerDefinition> servers)
    {
        string path = LocalPaths.UserCodexConfig;
        string text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        string preserved = McpServerSectionsRegex.Replace(text, string.Empty).TrimEnd();
        List<string> builder = new();
        if (!string.IsNullOrWhiteSpace(preserved))
        {
            builder.Add(preserved);
            builder.Add(string.Empty);
        }

        List<McpServerDefinition> requestedServers = (servers ?? Enumerable.Empty<McpServerDefinition>()).ToList();
        McpServerDefinition requiredServer = CreateRequiredReactiveMemoryServer();
        IEnumerable<McpServerDefinition> candidates = requestedServers.Where(IsRequestedServer);
        foreach (McpServerDefinition server in new[] { requiredServer }.Concat(candidates).Where(IsValidServer))
        {
            builder.Add($"[mcp_servers.{server.Name.Trim()}]");
            if (string.Equals(server.TransportType, "url", StringComparison.OrdinalIgnoreCase))
            {
                builder.Add($"url = {EncodeTomlString(server.Url.Trim())}");
            }
            else
            {
                builder.Add($"command = {EncodeTomlString(server.Command.Trim())}");
                List<string> args = NormalizeArgs(server).ToList();
                if (args.Count > 0)
                {
                    builder.Add($"args = [{string.Join(", ", args.Select(EncodeTomlString))}]");
                }
            }

            builder.Add($"enabled = {(server.IsEnabled ? "true" : "false")}");
            builder.Add(string.Empty);
        }

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, string.Join(Environment.NewLine, builder).TrimEnd() + Environment.NewLine);
        Refresh();
    }

    /// <summary>Creates template.</summary>
    /// <param name="transportType">The transport Type.</param>
    /// <returns>The create Template result.</returns>
    private McpServerDefinition CreateTemplateCore(string transportType)
    {
        HashSet<string> existingNames = new(Snapshot.Select((x) => x.Name), StringComparer.OrdinalIgnoreCase);
        string prefix = (string.Equals(transportType, "url", StringComparison.OrdinalIgnoreCase) ? "remote-mcp" : "local-mcp");
        int index = 1;
        string name = prefix;
        while (existingNames.Contains(name))
        {
            index++;
            name = $"{prefix}-{index.ToString(CultureInfo.InvariantCulture)}";
        }

        return new McpServerDefinition
        {
            Name = name,
            TransportType = (string.Equals(transportType, "url", StringComparison.OrdinalIgnoreCase) ? "url" : "stdio"),
            Command = (string.Equals(transportType, "url", StringComparison.OrdinalIgnoreCase) ? string.Empty : "npx"),
            Url = (string.Equals(transportType, "url", StringComparison.OrdinalIgnoreCase) ? "https://example.com/mcp" : string.Empty),
            IsEnabled = true,
            Health = "new"
        };
    }

    /// <summary>Ensures reactive Memory Default.</summary>
    /// <param name="path">The path.</param>
    private void EnsureReactiveMemoryDefault(string path)
    {
        string text = (File.Exists(path) ? File.ReadAllText(path) : string.Empty);
        Match preferredBlock = FindMcpServerBlock(text, CpReactivememoryMcpServerText);
        if (preferredBlock.Success)
        {
            string updated = ReplaceReactiveMemoryBlock(text, preferredBlock);
            updated = DisableLegacyReactiveMemoryFallback(updated);
            if (!StringComparer.Ordinal.Equals(updated, text))
            {
                File.WriteAllText(path, updated);
            }

            return;
        }

        Match legacyBlock = FindMcpServerBlock(text, ReactivememoryText);
        if (legacyBlock.Success)
        {
            File.WriteAllText(path, ReplaceReactiveMemoryBlock(text, legacyBlock));
            return;
        }

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        string separator = (string.IsNullOrWhiteSpace(text) ? string.Empty : (Environment.NewLine + Environment.NewLine));
        File.WriteAllText(path, text.TrimEnd() + separator + BuildRequiredReactiveMemoryBlock());
    }

    /// <summary>Builds reactive Memory Command.</summary>
    /// <param name="args">The args.</param>
    /// <returns>The build Reactive Memory Command result.</returns>
    private string BuildReactiveMemoryCommand(out IReadOnlyList<string> args)
    {
        string? bundledServer = FindBundledReactiveMemoryServer();
        if (bundledServer?.Trim().Length > 0)
        {
            args = [bundledServer];
            return "dotnet";
        }

        args = ["CP.ReactiveMemory.Mcp.Server@1.*", "--yes"];
        return "dnx";
    }

    /// <summary>Creates required Reactive Memory Server.</summary>
    /// <returns>The create Required Reactive Memory Server result.</returns>
    private McpServerDefinition CreateRequiredReactiveMemoryServer()
    {
        string command = BuildReactiveMemoryCommand(out var args);
        return new McpServerDefinition
        {
            Name = CpReactivememoryMcpServerText,
            TransportType = "stdio",
            Command = command,
            ArgumentsText = string.Join(Environment.NewLine, args),
            IsEnabled = true,
            IsRequired = true,
            Health = "required"
        };
    }

    /// <summary>Builds required Reactive Memory Block.</summary>
    /// <returns>The build Required Reactive Memory Block result.</returns>
    private string BuildRequiredReactiveMemoryBlock()
    {
        McpServerDefinition server = CreateRequiredReactiveMemoryServer();
        const string Description = "# Required VSCodex durable pause and project memory service. This server cannot be removed or disabled.";
        string args = string.Join(", ", server.Args.Select(EncodeTomlString));
        return string.Join(
            Environment.NewLine,
            Description,
            "[mcp_servers.cp-reactivememory-mcp-server]",
            $"command = {EncodeTomlString(server.Command)}",
            $"args = [{args}]",
            "enabled = true",
            string.Empty);
    }

    /// <summary>Performs the replace Reactive Memory Block operation.</summary>
    /// <param name="text">The text.</param>
    /// <param name="block">The block.</param>
    /// <returns>The replace Reactive Memory Block result.</returns>
    private string ReplaceReactiveMemoryBlock(string text, Match block)
    {
        if (!block.Success)
        {
            return text;
        }

        string before = text.Substring(0, block.Index).TrimEnd();
        string after = text.Substring(block.Index + block.Length).TrimStart('\r', '\n');
        string separator = (string.IsNullOrWhiteSpace(before) ? string.Empty : (Environment.NewLine + Environment.NewLine));
        string suffix = (string.IsNullOrWhiteSpace(after) ? string.Empty : (Environment.NewLine + after));
        return before + separator + BuildRequiredReactiveMemoryBlock() + suffix;
    }

    /// <summary>Finds mcp Server Block.</summary>
    /// <param name="text">The text.</param>
    /// <param name="serverName">The server Name.</param>
    /// <returns>The find Mcp Server Block result.</returns>
    private Match FindMcpServerBlock(string text, string serverName)
    {
        var pattern = $"(?ims)^\\s*\\[mcp_servers\\.{Regex.Escape(serverName)}\\](?<body>.*?)(?=^\\s*\\[|\\z)";
        var expression = new Regex(pattern, RegexOptions.Compiled);
        return expression.Match(text ?? string.Empty);
    }

    /// <summary>Performs the disable Legacy Reactive Memory Fallback operation.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The disable Legacy Reactive Memory Fallback result.</returns>
    private string DisableLegacyReactiveMemoryFallback(string text)
    {
        Match legacyBlock = FindMcpServerBlock(text, ReactivememoryText);
        if (!legacyBlock.Success || !LooksLikeReactiveMemoryServerBlock(legacyBlock))
        {
            return text;
        }

        if (LegacyEnabledRegex.IsMatch(legacyBlock.Groups["body"].Value))
        {
            return text;
        }

        return LegacyEnabledAssignmentRegex.IsMatch(legacyBlock.Groups["body"].Value)
            ? LegacyEnabledAssignmentRegex.Replace(text, "$1false$2")
            : text.Insert(legacyBlock.Index + legacyBlock.Length, $"{Environment.NewLine}enabled = false");
    }

    /// <summary>Performs the looks Like Reactive Memory Server Block operation.</summary>
    /// <param name="block">The block.</param>
    /// <returns><see langword="true"/> when looks Like Reactive Memory Server Block succeeds; otherwise, <see langword="false"/>.</returns>
    private bool LooksLikeReactiveMemoryServerBlock(Match block)
    {
        string value = block.Value ?? string.Empty;
        return value.IndexOf(ReactiveMemoryText, StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("CP.ReactiveMemory.Mcp.Server", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("CP.ReactiveMemory.MCP.Server", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Finds bundled Reactive Memory Server.</summary>
    /// <returns>The find Bundled Reactive Memory Server result.</returns>
    private string? FindBundledReactiveMemoryServer()
    {
        string explicitServer = Environment.GetEnvironmentVariable("VSCODEX_REACTIVEMEMORY_SERVER_PATH");
        if (!string.IsNullOrWhiteSpace(explicitServer) && File.Exists(explicitServer))
        {
            return Path.GetFullPath(explicitServer);
        }

        string extensionRoot = Path.GetDirectoryName(typeof(McpConfigService).Assembly.Location) ?? string.Empty;
        string direct = new string[4]
        {
            Path.Combine(extensionRoot, ReactiveMemoryText, CPReactiveMemoryMCPServerDllText),
            Path.Combine(extensionRoot, ReactiveMemoryText, "CP.ReactiveMemory.Mcp.Server.dll"),
            Path.Combine(extensionRoot, CPReactiveMemoryMCPServerDllText),
            Path.Combine(extensionRoot, "CP.ReactiveMemory.Mcp.Server.dll")
        }.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        string bundledRoot = Path.Combine(extensionRoot, ReactiveMemoryText);
        if (!Directory.Exists(bundledRoot))
        {
            return null;
        }

        try
        {
            return Directory.EnumerateFiles(bundledRoot, CPReactiveMemoryMCPServerDllText, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Parses array.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The parse Array result.</returns>
    private IEnumerable<string> ParseArray(string value)
    {
        Match m = TomlArrayRegex.Match(value);
        if (!m.Success)
        {
            yield break;
        }

        foreach (Match item in TomlStringRegex.Matches(m.Groups[1].Value))
        {
            yield return item.Groups[1].Value;
        }
    }

    /// <summary>Determines whether is Valid Server.</summary>
    /// <param name="server">The server.</param>
    /// <returns><see langword="true"/> when is Valid Server succeeds; otherwise, <see langword="false"/>.</returns>
    private bool IsValidServer(McpServerDefinition server)
    {
        if (server is null || string.IsNullOrWhiteSpace(server.Name))
        {
            return false;
        }

        if (server.Name.Trim().Any((ch) => !char.IsLetterOrDigit(ch) && ch != '_' && ch != '-'))
        {
            return false;
        }

        return !string.Equals(server.TransportType, "url", StringComparison.OrdinalIgnoreCase) ? !string.IsNullOrWhiteSpace(server.Command) : !string.IsNullOrWhiteSpace(server.Url);
    }

    /// <summary>Determines whether a server was requested by the user.</summary>
    /// <param name="server">The server.</param>
    /// <returns><see langword="true"/> when the server is requested.</returns>
    private bool IsRequestedServer(McpServerDefinition server)
    {
        return server is not null
            && !string.Equals(server.Name, CpReactivememoryMcpServerText, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(server.Name, ReactivememoryText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Performs the normalize Args operation.</summary>
    /// <param name="server">The server.</param>
    /// <returns>The normalize Args result.</returns>
    private IEnumerable<string> NormalizeArgs(McpServerDefinition server)
    {
        if (!string.IsNullOrWhiteSpace(server.ArgumentsText))
        {
            return from x in server.ArgumentsText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                   select x.Trim() into x
                   where !string.IsNullOrWhiteSpace(x)
                   select x;
        }

        IEnumerable<string> args = server.Args;
        return args ?? Enumerable.Empty<string>();
    }

    /// <summary>Performs the encode Toml String operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The encode Toml String result.</returns>
    private string EncodeTomlString(string value)
    {
        return $"\"{(value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
