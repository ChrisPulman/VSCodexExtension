using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

public interface IReactiveMemoryService
{
    Task<ReactiveMemoryCallResult> ReactToPromptAsync(string prompt, WorkspaceIdentity identity, string? threadId);
    Task<ReactiveMemoryCallResult> WriteDiaryAsync(string prompt, string response, WorkspaceIdentity identity, string? threadId);
    Task<ReactiveMemoryCallResult> AddMemoryAsync(string text, string scope, WorkspaceIdentity identity);
    Task<ReactiveMemoryCallResult> ScanWorkspaceAsync(WorkspaceIdentity identity, bool automatic = false);
}

public sealed class ReactiveMemoryCallResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public JObject? RawResult { get; set; }
}

public sealed class ReactiveMemoryService : IReactiveMemoryService
{
    private const int MaxProjectMinerFiles = 180;
    private const int MaxProjectMinerChunks = 320;
    private const int MaxAutomaticProjectMinerFiles = 24;
    private const int MaxAutomaticProjectMinerChunks = 32;
    private const int ProjectMinerChunkSize = 800;
    private const int ProjectMinerChunkOverlap = 100;
    private static readonly TimeSpan AutomaticScanInterval = TimeSpan.FromHours(24);
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastWorkspaceScans = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
    private readonly IMcpConfigService _mcpConfig;
    private readonly IMcpToolCatalogService _mcpTools;

    public ReactiveMemoryService(IMcpConfigService mcpConfig, IMcpToolCatalogService mcpTools)
    {
        _mcpConfig = mcpConfig;
        _mcpTools = mcpTools;
    }

    public Task<ReactiveMemoryCallResult> ReactToPromptAsync(string prompt, WorkspaceIdentity identity, string? threadId)
    {
        var args = new JObject
        {
            ["prompt"] = BuildProjectPrompt(prompt, identity),
            ["agentName"] = "VSCodex",
            ["sector"] = WorkspaceSector(identity),
            ["vault"] = identity?.RootPath ?? string.Empty
        };
        return InvokeReactiveMemoryAsync(args, "reactivememory_react_to_prompt", "react_to_prompt", "current_user_prompt", "current user prompt");
    }

    public Task<ReactiveMemoryCallResult> WriteDiaryAsync(string prompt, string response, WorkspaceIdentity identity, string? threadId)
    {
        var entry = "Prompt: " + Trim(prompt, 1200) + Environment.NewLine + Environment.NewLine + "Response: " + Trim(response, 2400);
        var args = new JObject
        {
            ["agentName"] = "VSCodex",
            ["topic"] = WorkspaceSector(identity),
            ["entry"] = "Workspace identity: " + (identity?.Id ?? string.Empty) + Environment.NewLine + "Workspace root: " + (identity?.RootPath ?? string.Empty) + Environment.NewLine + "Thread: " + (threadId ?? string.Empty) + Environment.NewLine + Environment.NewLine + entry
        };
        return InvokeReactiveMemoryAsync(args, "reactivememory_diary_write", "diary_write", "agent_diary", "agent diary");
    }

    public Task<ReactiveMemoryCallResult> AddMemoryAsync(string text, string scope, WorkspaceIdentity identity)
    {
        var args = new JObject
        {
            ["agentName"] = "VSCodex",
            ["topic"] = WorkspaceSector(identity) + " " + scope + " memory",
            ["entry"] = "Workspace identity: " + (identity?.Id ?? string.Empty) + Environment.NewLine + "Workspace root: " + (identity?.RootPath ?? string.Empty) + Environment.NewLine + Environment.NewLine + (text ?? string.Empty)
        };
        return InvokeReactiveMemoryAsync(args, "reactivememory_diary_write", "diary_write", "agent_diary", "agent diary");
    }

    public async Task<ReactiveMemoryCallResult> ScanWorkspaceAsync(WorkspaceIdentity identity, bool automatic = false)
    {
        if (identity == null || string.IsNullOrWhiteSpace(identity.RootPath) || !Directory.Exists(identity.RootPath))
        {
            return Unavailable("ReactiveMemory ProjectMiner scan skipped because the Visual Studio workspace root is unavailable.");
        }

        var key = string.IsNullOrWhiteSpace(identity.Id) ? identity.RootPath : identity.Id;
        var scanCacheKey = (automatic ? "automatic|" : "manual|") + key;
        if (LastWorkspaceScans.TryGetValue(scanCacheKey, out var lastScan) && DateTimeOffset.UtcNow - lastScan < TimeSpan.FromMinutes(30))
        {
            return new ReactiveMemoryCallResult { Success = true, Message = "ReactiveMemory ProjectMiner scan already ran for this workspace." };
        }

        if (automatic && HasRecentAutomaticScan(key))
        {
            return new ReactiveMemoryCallResult { Success = true, Message = "ReactiveMemory ProjectMiner automatic scan skipped because this workspace was mined recently." };
        }

        try
        {
            var server = FindReactiveMemoryServer();
            if (server == null)
            {
                return Unavailable("ReactiveMemory MCP server is not enabled in Codex config.");
            }

            var tools = await _mcpTools.DiscoverToolsAsync(server).ConfigureAwait(false);
            var projectMinerTool = tools.FirstOrDefault(tool => ToolMatches(tool, "reactivememory_mine_project")
                || ToolMatches(tool, "reactivememory_project_miner")
                || ToolMatches(tool, "project_miner")
                || ToolMatches(tool, "mine_project")
                || ToolMatches(tool, "scan_project"));
            if (projectMinerTool != null)
            {
                var result = await _mcpTools.InvokeToolAsync(server, projectMinerTool.Name, new JObject
                {
                    ["projectRoot"] = identity.RootPath,
                    ["workspaceRoot"] = identity.RootPath,
                    ["solutionPath"] = identity.SolutionPath ?? string.Empty,
                    ["sector"] = WorkspaceSector(identity),
                    ["agentName"] = "VSCodex"
                }).ConfigureAwait(false);
                LastWorkspaceScans[scanCacheKey] = DateTimeOffset.UtcNow;
                MarkAutomaticScan(key, automatic);
                return new ReactiveMemoryCallResult { Success = true, Message = "ReactiveMemory ProjectMiner scanned " + identity.RootPath, RawResult = result };
            }

            var addDrawerToolName = tools.FirstOrDefault(tool => ToolMatches(tool, "reactivememory_add_drawer") || ToolMatches(tool, "add_drawer"))?.Name;
            if (string.IsNullOrWhiteSpace(addDrawerToolName) && ScoreReactiveMemoryServer(server) > 0)
            {
                addDrawerToolName = "reactivememory_add_drawer";
            }

            if (string.IsNullOrWhiteSpace(addDrawerToolName))
            {
                return Unavailable("ReactiveMemory MCP server does not expose ProjectMiner or add_drawer tools.");
            }

            var maxFiles = automatic ? MaxAutomaticProjectMinerFiles : MaxProjectMinerFiles;
            var maxChunks = automatic ? MaxAutomaticProjectMinerChunks : MaxProjectMinerChunks;
            var invocations = BuildProjectMinerFallbackInvocations(identity, addDrawerToolName!, maxFiles, maxChunks).ToList();
            if (invocations.Count == 0)
            {
                LastWorkspaceScans[scanCacheKey] = DateTimeOffset.UtcNow;
                MarkAutomaticScan(key, automatic);
                return new ReactiveMemoryCallResult { Success = true, Message = "ReactiveMemory ProjectMiner scan found no safe text files to mine." };
            }

            var timeout = automatic ? TimeSpan.FromSeconds(45) : TimeSpan.FromMinutes(4);
            var responses = await _mcpTools.InvokeToolsAsync(server, invocations, timeout).ConfigureAwait(false);
            LastWorkspaceScans[scanCacheKey] = DateTimeOffset.UtcNow;
            MarkAutomaticScan(key, automatic);
            var completed = responses.Count(response => response != null && response["error"] == null);
            return new ReactiveMemoryCallResult
            {
                Success = completed > 0,
                Message = $"ReactiveMemory ProjectMiner-compatible scan filed {completed} chunk(s) from {identity.RootPath}.",
                RawResult = new JObject { ["requested"] = invocations.Count, ["completed"] = completed }
            };
        }
        catch (Exception ex)
        {
            return Unavailable("ReactiveMemory ProjectMiner scan failed: " + ex.Message);
        }
    }

    private async Task<ReactiveMemoryCallResult> InvokeReactiveMemoryAsync(JObject arguments, params string[] toolNames)
    {
        try
        {
            var server = FindReactiveMemoryServer();
            if (server == null)
            {
                return Unavailable("ReactiveMemory MCP server is not enabled in Codex config.");
            }

            var tools = await _mcpTools.DiscoverToolsAsync(server).ConfigureAwait(false);
            var tool = tools.FirstOrDefault(candidate => toolNames.Any(name => ToolMatches(candidate, name)));
            var toolName = tool?.Name ?? toolNames.FirstOrDefault(name => name.StartsWith("reactivememory_", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return Unavailable("ReactiveMemory MCP server did not expose the required tool: " + string.Join(", ", toolNames));
            }

            var result = await _mcpTools.InvokeToolAsync(server, toolName, arguments).ConfigureAwait(false);
            return new ReactiveMemoryCallResult { Success = true, Message = "ReactiveMemory updated through " + toolName, RawResult = result };
        }
        catch (Exception ex)
        {
            return Unavailable("ReactiveMemory unavailable: " + ex.Message);
        }
    }

    private McpServerDefinition? FindReactiveMemoryServer()
    {
        var servers = _mcpConfig.Snapshot;
        return servers
            .Where(x => x.IsEnabled)
            .Select(x => new { Server = x, Score = ScoreReactiveMemoryServer(x) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Server)
            .FirstOrDefault();
    }

    private static int ScoreReactiveMemoryServer(McpServerDefinition server)
    {
        var name = server.Name ?? string.Empty;
        var command = server.Command ?? string.Empty;
        var args = server.Args ?? new List<string>();
        if (name.Equals("cp-reactivememory-mcp-server", StringComparison.OrdinalIgnoreCase)) return 100;
        if (args.Any(arg => arg.IndexOf("CP.ReactiveMemory.Mcp.Server@", StringComparison.OrdinalIgnoreCase) >= 0)) return 95;
        if (args.Any(arg => arg.IndexOf("CP.ReactiveMemory.MCP.Server.csproj", StringComparison.OrdinalIgnoreCase) >= 0)) return 90;
        if (name.Equals("reactivememory", StringComparison.OrdinalIgnoreCase)) return 80;
        if (name.IndexOf("reactivememory", StringComparison.OrdinalIgnoreCase) >= 0) return 70;
        if (command.IndexOf("ReactiveMemory", StringComparison.OrdinalIgnoreCase) >= 0) return 60;
        if (args.Any(arg => arg.IndexOf("ReactiveMemory", StringComparison.OrdinalIgnoreCase) >= 0)) return 50;
        return 0;
    }

    private static bool ToolMatches(McpToolDefinition tool, string name)
    {
        var normalizedTool = Normalize(tool.Name);
        var normalizedName = Normalize(name);
        return normalizedTool.Equals(normalizedName, StringComparison.OrdinalIgnoreCase)
            || normalizedTool.IndexOf(normalizedName, StringComparison.OrdinalIgnoreCase) >= 0
            || Normalize(tool.Description).IndexOf(normalizedName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string BuildProjectPrompt(string prompt, WorkspaceIdentity identity)
        => "Workspace identity: " + (identity?.Id ?? string.Empty)
            + Environment.NewLine + "Workspace root: " + (identity?.RootPath ?? string.Empty)
            + Environment.NewLine + "Solution: " + (identity?.SolutionRelativePath ?? string.Empty)
            + Environment.NewLine + Environment.NewLine + (prompt ?? string.Empty);

    private static string WorkspaceSector(WorkspaceIdentity? identity)
    {
        var name = identity?.Name;
        return string.IsNullOrWhiteSpace(name) ? "VSCodex workspace" : name!;
    }

    private static IEnumerable<McpToolInvocation> BuildProjectMinerFallbackInvocations(WorkspaceIdentity identity, string toolName, int maxFiles, int maxChunks)
    {
        var sector = WorkspaceSector(identity);
        var files = EnumerateProjectMinerFiles(identity.RootPath, maxFiles);
        var chunks = 0;
        foreach (var file in files)
        {
            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            foreach (var chunk in ChunkText(content))
            {
                if (++chunks > maxChunks)
                {
                    yield break;
                }

                yield return new McpToolInvocation
                {
                    ToolName = toolName,
                    Arguments = new JObject
                    {
                        ["sector"] = sector,
                        ["vault"] = DetectProjectVault(file, content, identity.RootPath),
                        ["content"] = chunk,
                        ["sourceFile"] = MakeRelative(identity.RootPath, file),
                        ["addedBy"] = "project_miner"
                    }
                };
            }
        }
    }

    private static IEnumerable<string> EnumerateProjectMinerFiles(string root, int limit)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            yield break;
        }

        var pending = new Stack<string>();
        pending.Push(root);
        var count = 0;
        while (pending.Count > 0 && count < limit)
        {
            var current = pending.Pop();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                if (count >= limit)
                {
                    yield break;
                }

                if (IsProjectMinerCandidate(file))
                {
                    count++;
                    yield return file;
                }
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories.Where(IsProjectMinerDirectory))
            {
                pending.Push(directory);
            }
        }
    }

    private static bool IsProjectMinerDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return !new[] { ".git", ".vs", "bin", "obj", "node_modules", "packages", ".idea", ".vscode", "TestResults", "artifacts" }
            .Any(blocked => string.Equals(name, blocked, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProjectMinerCandidate(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (!new[] { ".cs", ".xaml", ".csproj", ".props", ".targets", ".json", ".xml", ".md", ".txt", ".yml", ".yaml", ".ps1", ".sln", ".slnx", ".config", ".js", ".ts", ".tsx", ".jsx", ".css", ".html", ".razor" }.Contains(extension))
        {
            return false;
        }

        try
        {
            return new FileInfo(path).Length is > 50 and <= 200_000;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> ChunkText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            yield break;
        }

        var start = 0;
        while (start < content.Length)
        {
            var end = Math.Min(start + ProjectMinerChunkSize, content.Length);
            if (end < content.Length)
            {
                var length = Math.Max(1, end - start);
                var paragraphBreak = content.LastIndexOf("\n\n", end - 1, length, StringComparison.Ordinal);
                if (paragraphBreak > start + ProjectMinerChunkSize / 2)
                {
                    end = paragraphBreak;
                }
                else
                {
                    var lineBreak = content.LastIndexOf('\n', end - 1, length);
                    if (lineBreak > start + ProjectMinerChunkSize / 2)
                    {
                        end = lineBreak;
                    }
                }
            }

            var chunk = content.Substring(start, end - start).Trim();
            if (chunk.Length >= 50)
            {
                yield return chunk;
            }

            if (end >= content.Length)
            {
                yield break;
            }

            start = Math.Max(0, end - ProjectMinerChunkOverlap);
        }
    }

    private static string DetectProjectVault(string filePath, string content, string root)
    {
        var relative = MakeRelative(root, filePath).ToLowerInvariant();
        var sample = content.Length > 2000 ? content.Substring(0, 2000).ToLowerInvariant() : content.ToLowerInvariant();
        if (relative.Contains("test") || sample.Contains("[test]")) return "tests";
        if (relative.EndsWith(".md", StringComparison.Ordinal) || relative.Contains("docs")) return "docs";
        if (relative.Contains("view") || relative.EndsWith(".xaml", StringComparison.Ordinal) || relative.EndsWith(".css", StringComparison.Ordinal)) return "ui";
        if (relative.Contains("mcp") || sample.Contains("modelcontextprotocol")) return "mcp";
        if (relative.EndsWith(".csproj", StringComparison.Ordinal) || relative.EndsWith(".props", StringComparison.Ordinal) || relative.EndsWith(".targets", StringComparison.Ordinal) || relative.EndsWith(".json", StringComparison.Ordinal)) return "configuration";
        return "source";
    }

    private static string MakeRelative(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path) || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return path.Substring(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool HasRecentAutomaticScan(string key)
    {
        try
        {
            var path = AutomaticScanStampPath(key);
            if (!File.Exists(path))
            {
                return false;
            }

            var text = File.ReadAllText(path);
            return DateTimeOffset.TryParse(text, out var scannedAt)
                && DateTimeOffset.UtcNow - scannedAt < AutomaticScanInterval;
        }
        catch
        {
            return false;
        }
    }

    private static void MarkAutomaticScan(string key, bool automatic)
    {
        if (!automatic)
        {
            return;
        }

        try
        {
            File.WriteAllText(AutomaticScanStampPath(key), DateTimeOffset.UtcNow.ToString("O"));
        }
        catch
        {
        }
    }

    private static string AutomaticScanStampPath(string key)
    {
        var root = LocalPaths.Ensure(Path.Combine(LocalPaths.AppRoot, "projectminer"));
        return Path.Combine(root, Sha256(key ?? string.Empty) + ".stamp");
    }

    private static string Sha256(string value)
    {
        using (var sha = SHA256.Create())
        {
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    private static ReactiveMemoryCallResult Unavailable(string message) => new ReactiveMemoryCallResult { Success = false, Message = message };

    private static string Trim(string value, int maxLength)
    {
        var text = value ?? string.Empty;
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    private static string Normalize(string value) => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
