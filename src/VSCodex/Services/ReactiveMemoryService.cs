using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VSCodex.Models;

namespace VSCodex.Services;

public interface IReactiveMemoryService
{
    Task<ReactiveMemoryCallResult> ReactToPromptAsync(string prompt, WorkspaceIdentity identity, string? threadId);
    Task<ReactiveMemoryCallResult> WriteDiaryAsync(string prompt, string response, WorkspaceIdentity identity, string? threadId);
    Task<ReactiveMemoryCallResult> AddMemoryAsync(string text, string scope, WorkspaceIdentity identity);
}

public sealed class ReactiveMemoryCallResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public JObject? RawResult { get; set; }
}

public sealed class ReactiveMemoryService : IReactiveMemoryService
{
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
            if (tool == null)
            {
                return Unavailable("ReactiveMemory MCP server did not expose the required tool: " + string.Join(", ", toolNames));
            }

            var result = await _mcpTools.InvokeToolAsync(server, tool.Name, arguments).ConfigureAwait(false);
            return new ReactiveMemoryCallResult { Success = true, Message = "ReactiveMemory updated through " + tool.Name, RawResult = result };
        }
        catch (Exception ex)
        {
            return Unavailable("ReactiveMemory unavailable: " + ex.Message);
        }
    }

    private McpServerDefinition? FindReactiveMemoryServer()
    {
        var servers = _mcpConfig.Snapshot;
        return servers.FirstOrDefault(x => x.IsEnabled
            && (x.Name.IndexOf("reactivememory", StringComparison.OrdinalIgnoreCase) >= 0
                || x.Command.IndexOf("ReactiveMemory", StringComparison.OrdinalIgnoreCase) >= 0
                || x.Args.Any(arg => arg.IndexOf("ReactiveMemory", StringComparison.OrdinalIgnoreCase) >= 0)));
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

    private static ReactiveMemoryCallResult Unavailable(string message) => new ReactiveMemoryCallResult { Success = false, Message = message };

    private static string Trim(string value, int maxLength)
    {
        var text = value ?? string.Empty;
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    private static string Normalize(string value) => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
