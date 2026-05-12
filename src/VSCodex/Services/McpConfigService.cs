using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

public interface IMcpConfigService
{
    IObservable<IReadOnlyList<McpServerDefinition>> Servers { get; }
    IReadOnlyList<McpServerDefinition> Snapshot { get; }
    void Refresh();
    void Save(IEnumerable<McpServerDefinition> servers);
    McpServerDefinition CreateTemplate(string transportType);
}
public sealed class McpConfigService : IMcpConfigService
{
    private readonly BehaviorSubject<IReadOnlyList<McpServerDefinition>> _servers = new BehaviorSubject<IReadOnlyList<McpServerDefinition>>(Array.Empty<McpServerDefinition>());
    public IObservable<IReadOnlyList<McpServerDefinition>> Servers => _servers.AsObservable();
    public IReadOnlyList<McpServerDefinition> Snapshot => _servers.Value;
    public void Refresh()
    {
        var path = LocalPaths.UserCodexConfig; EnsureReactiveMemoryDefault(path);
        var list = new List<McpServerDefinition>(); McpServerDefinition? current = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim(); var header = Regex.Match(line, "^\\[mcp_servers\\.([^\\]]+)\\]");
            if (header.Success) { current = new McpServerDefinition { Name = header.Groups[1].Value }; list.Add(current); continue; }
            if (current == null || line.StartsWith("#") || !line.Contains("=")) continue;
            var parts = line.Split(new[] { '=' }, 2); var key = parts[0].Trim(); var value = parts[1].Trim().Trim('"');
            if (key.Equals("command", StringComparison.OrdinalIgnoreCase)) current.Command = value;
            else if (key.Equals("url", StringComparison.OrdinalIgnoreCase)) { current.Url = value; current.TransportType = "url"; }
            else if (key.Equals("args", StringComparison.OrdinalIgnoreCase)) current.Args = ParseArray(parts[1]).ToList();
            else if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase)) current.IsEnabled = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
        foreach (var server in list)
        {
            server.ArgumentsText = string.Join(Environment.NewLine, server.Args);
        }
        _servers.OnNext(list);
    }

    public void Save(IEnumerable<McpServerDefinition> servers)
    {
        var path = LocalPaths.UserCodexConfig;
        var existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var preserved = Regex.Replace(existing, @"(?ms)^\s*\[mcp_servers\.[^\]]+\].*?(?=^\s*\[|\z)", string.Empty).TrimEnd();
        var builder = new List<string>();
        if (!string.IsNullOrWhiteSpace(preserved))
        {
            builder.Add(preserved);
            builder.Add(string.Empty);
        }

        foreach (var server in (servers ?? Enumerable.Empty<McpServerDefinition>()).Where(IsValidServer))
        {
            builder.Add("[mcp_servers." + server.Name.Trim() + "]");
            if (string.Equals(server.TransportType, "url", StringComparison.OrdinalIgnoreCase))
            {
                builder.Add("url = " + EncodeTomlString(server.Url.Trim()));
            }
            else
            {
                builder.Add("command = " + EncodeTomlString(server.Command.Trim()));
                var args = NormalizeArgs(server).ToList();
                if (args.Count > 0)
                {
                    builder.Add("args = [" + string.Join(", ", args.Select(EncodeTomlString)) + "]");
                }
            }

            builder.Add("enabled = " + (server.IsEnabled ? "true" : "false"));
            builder.Add(string.Empty);
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, string.Join(Environment.NewLine, builder).TrimEnd() + Environment.NewLine);
        Refresh();
    }

    public McpServerDefinition CreateTemplate(string transportType)
    {
        var existingNames = new HashSet<string>(Snapshot.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
        var prefix = string.Equals(transportType, "url", StringComparison.OrdinalIgnoreCase) ? "remote-mcp" : "local-mcp";
        var index = 1;
        var name = prefix;
        while (existingNames.Contains(name))
        {
            name = prefix + "-" + (++index).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return new McpServerDefinition
        {
            Name = name,
            TransportType = string.Equals(transportType, "url", StringComparison.OrdinalIgnoreCase) ? "url" : "stdio",
            Command = string.Equals(transportType, "url", StringComparison.OrdinalIgnoreCase) ? string.Empty : "npx",
            Url = string.Equals(transportType, "url", StringComparison.OrdinalIgnoreCase) ? "https://example.com/mcp" : string.Empty,
            IsEnabled = true,
            Health = "new"
        };
    }

    private static void EnsureReactiveMemoryDefault(string path)
    {
        var text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var existingBlock = Regex.Match(text, @"(?ms)^\s*\[mcp_servers\.reactivememory\](?<body>.*?)(?=^\s*\[|\z)");
        if (existingBlock.Success)
        {
            if (Regex.IsMatch(existingBlock.Groups["body"].Value, @"(?im)^\s*enabled\s*=\s*false\s*$"))
            {
                text = Regex.Replace(text, @"(?ims)(^\s*\[mcp_servers\.reactivememory\].*?^\s*enabled\s*=\s*)false(\s*$)", "$1true$2");
                File.WriteAllText(path, text);
            }

            return;
        }

        var args = BuildReactiveMemoryArgs();
        var defaultBlock = Environment.NewLine
            + "# VSCodex default durable memory system." + Environment.NewLine
            + "[mcp_servers.reactivememory]" + Environment.NewLine
            + "command = \"dotnet\"" + Environment.NewLine
            + "args = [" + string.Join(", ", args.Select(x => "\"" + x.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")) + "]" + Environment.NewLine
            + "enabled = true" + Environment.NewLine;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.AppendAllText(path, defaultBlock);
    }

    private static IReadOnlyList<string> BuildReactiveMemoryArgs()
    {
        var project = FindReactiveMemoryProject();
        if (!string.IsNullOrWhiteSpace(project))
        {
            return new[] { "run", "--project", project! };
        }

        return new[] { "tool", "run", "CP.ReactiveMemory.Mcp.Server" };
    }

    private static string? FindReactiveMemoryProject()
    {
        var explicitProject = Environment.GetEnvironmentVariable("REACTIVEMEMORY_MCP_PROJECT");
        if (!string.IsNullOrWhiteSpace(explicitProject) && File.Exists(explicitProject))
        {
            return explicitProject;
        }

        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(user, "source", "repos", "ReactiveMemory.MCP.Server", "src", "ReactiveMemory.MCP.Server", "ReactiveMemory.MCP.Server.csproj"),
            Path.Combine(user, "source", "repos", "ReactiveMemory.MCP.Server", "src", "ReactiveMemory.MCP.Server", "CP.ReactiveMemory.MCP.Server.csproj"),
            Path.Combine(user, "Projects", "Github", "ReactiveMemory.MCP.Server", "src", "ReactiveMemory.MCP.Server", "CP.ReactiveMemory.MCP.Server.csproj"),
            @"D:\Projects\Github\chrispulman\ReactiveMemory.MCP.Server\src\ReactiveMemory.MCP.Server\CP.ReactiveMemory.MCP.Server.csproj"
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> ParseArray(string value)
    { var m = Regex.Match(value, "\\[(.*)\\]"); if (!m.Success) yield break; foreach (Match item in Regex.Matches(m.Groups[1].Value, "\"([^\"]*)\"")) yield return item.Groups[1].Value; }

    private static bool IsValidServer(McpServerDefinition server)
    {
        if (server == null || string.IsNullOrWhiteSpace(server.Name))
        {
            return false;
        }

        var name = server.Name.Trim();
        if (name.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')))
        {
            return false;
        }

        return string.Equals(server.TransportType, "url", StringComparison.OrdinalIgnoreCase)
            ? !string.IsNullOrWhiteSpace(server.Url)
            : !string.IsNullOrWhiteSpace(server.Command);
    }

    private static IEnumerable<string> NormalizeArgs(McpServerDefinition server)
    {
        if (!string.IsNullOrWhiteSpace(server.ArgumentsText))
        {
            return server.ArgumentsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));
        }

        return server.Args ?? Enumerable.Empty<string>();
    }

    private static string EncodeTomlString(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
