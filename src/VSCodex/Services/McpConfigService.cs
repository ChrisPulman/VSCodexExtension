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

public interface IMcpConfigService { IObservable<IReadOnlyList<McpServerDefinition>> Servers { get; } IReadOnlyList<McpServerDefinition> Snapshot { get; } void Refresh(); }
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
            else if (key.Equals("args", StringComparison.OrdinalIgnoreCase)) current.Args = ParseArray(parts[1]).ToList();
            else if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase)) current.IsEnabled = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
        _servers.OnNext(list);
    }

    private static void EnsureReactiveMemoryDefault(string path)
    {
        var text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        if (text.IndexOf("[mcp_servers.reactivememory]", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("ReactiveMemory.MCP.Server", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("CP.ReactiveMemory.Mcp.Server", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return;
        }

        var args = BuildReactiveMemoryArgs();
        var block = Environment.NewLine
            + "# VSCodex default durable memory system." + Environment.NewLine
            + "[mcp_servers.reactivememory]" + Environment.NewLine
            + "command = \"dotnet\"" + Environment.NewLine
            + "args = [" + string.Join(", ", args.Select(x => "\"" + x.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"")) + "]" + Environment.NewLine
            + "enabled = true" + Environment.NewLine;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.AppendAllText(path, block);
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
            Path.Combine(user, "Projects", "Github", "ReactiveMemory.MCP.Server", "src", "ReactiveMemory.MCP.Server", "ReactiveMemory.MCP.Server.csproj"),
            @"D:\Projects\Github\chrispulman\ReactiveMemory.MCP.Server\src\ReactiveMemory.MCP.Server\ReactiveMemory.MCP.Server.csproj"
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> ParseArray(string value)
    { var m = Regex.Match(value, "\\[(.*)\\]"); if (!m.Success) yield break; foreach (Match item in Regex.Matches(m.Groups[1].Value, "\"([^\"]*)\"")) yield return item.Groups[1].Value; }
}
