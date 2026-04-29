using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using VSCodexExtension.Infrastructure;
using VSCodexExtension.Models;
namespace VSCodexExtension.Services
{
    public interface IMcpConfigService { IObservable<IReadOnlyList<McpServerDefinition>> Servers { get; } IReadOnlyList<McpServerDefinition> Snapshot { get; } void Refresh(); }
    public sealed class McpConfigService : IMcpConfigService
    {
        private readonly BehaviorSubject<IReadOnlyList<McpServerDefinition>> _servers = new BehaviorSubject<IReadOnlyList<McpServerDefinition>>(Array.Empty<McpServerDefinition>());
        public IObservable<IReadOnlyList<McpServerDefinition>> Servers => _servers.AsObservable();
        public IReadOnlyList<McpServerDefinition> Snapshot => _servers.Value;
        public void Refresh()
        {
            var path = LocalPaths.UserCodexConfig; if (!File.Exists(path)) { _servers.OnNext(Array.Empty<McpServerDefinition>()); return; }
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
        private static IEnumerable<string> ParseArray(string value)
        { var m = Regex.Match(value, "\\[(.*)\\]"); if (!m.Success) yield break; foreach (Match item in Regex.Matches(m.Groups[1].Value, "\"([^\"]*)\"")) yield return item.Groups[1].Value; }
    }
}
