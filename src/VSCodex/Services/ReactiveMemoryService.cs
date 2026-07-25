// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides ReactiveMemory MCP operations for VSCodex.</summary>
/// <param name="mcpConfig">The Codex MCP server configuration.</param>
/// <param name="mcpTools">The service used to discover and invoke MCP tools.</param>
/// <param name="timeProvider">The time source used to throttle workspace scans.</param>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class ReactiveMemoryService(IMcpConfigService mcpConfig, IMcpToolCatalogService mcpTools, TimeProvider timeProvider) : IReactiveMemoryService
{
    /// <summary>The MCP argument key for the memory topic.</summary>
    private const string TopicArgument = "topic";

    /// <summary>The preferred ReactiveMemory add-drawer tool.</summary>
    private const string AddDrawerTool = "reactivememory_add_drawer";

    /// <summary>The short alias for the ReactiveMemory add-drawer tool.</summary>
    private const string AddDrawerToolAlias = "add_drawer";

    /// <summary>The preferred ReactiveMemory search tool.</summary>
    private const string SearchTool = "reactivememory_search";

    /// <summary>The message returned when the required server is unavailable.</summary>
    private const string ServerUnavailable = "ReactiveMemory MCP server is not enabled in Codex config.";

    /// <summary>The number of minutes before a workspace may be scanned again.</summary>
    private const int ScanCooldownMinutes = 30;

    /// <summary>The automatic workspace scan timeout, in seconds.</summary>
    private const int AutomaticScanTimeoutSeconds = 45;

    /// <summary>The manual workspace scan timeout, in minutes.</summary>
    private const int ManualScanTimeoutMinutes = 4;

    /// <summary>The score for an exact CP ReactiveMemory server-name match.</summary>
    private const int ExactServerNameScore = 100;

    /// <summary>The score for an exact CP ReactiveMemory package match.</summary>
    private const int ExactPackageScore = 95;

    /// <summary>The score for a ReactiveMemory package-argument match.</summary>
    private const int PackageArgumentScore = 90;

    /// <summary>The score for a ReactiveMemory server-name fragment match.</summary>
    private const int ServerNameFragmentScore = 80;

    /// <summary>The last time each workspace and scan mode was mined.</summary>
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastWorkspaceScans = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The source of configured MCP servers.</summary>
    private readonly IMcpConfigService _mcpConfig = mcpConfig;

    /// <summary>The MCP tool discovery and invocation service.</summary>
    private readonly IMcpToolCatalogService _mcpTools = mcpTools;

    /// <summary>The time source used for scan throttling.</summary>
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>Gets the debugger display for this service.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => ToString();

    /// <inheritdoc/>
    public Task<ReactiveMemoryCallResult> ReactToPromptAsync(string prompt, WorkspaceIdentity identity, string? threadId)
    {
        JObject arguments = MemoryUtilities.CreateWorkspaceArguments(identity, threadId);
        arguments[nameof(prompt)] = MemoryUtilities.BuildPrompt(prompt, identity);
        return InvokeAsync(arguments, "reactivememory_react_to_prompt", "react_to_prompt", "current_user_prompt");
    }

    /// <inheritdoc/>
    public Task<ReactiveMemoryCallResult> WriteDiaryAsync(string prompt, string response, WorkspaceIdentity identity, string? threadId)
    {
        JObject arguments = MemoryUtilities.CreateWorkspaceArguments(identity, threadId);
        arguments[TopicArgument] = MemoryUtilities.Sector(identity);
        arguments["entry"] = MemoryUtilities.BuildDiaryEntry(prompt, response, identity, threadId);
        return InvokeAsync(arguments, "reactivememory_diary_write", "diary_write", "agent_diary");
    }

    /// <inheritdoc/>
    public async Task<ReactiveMemoryCallResult> AddMemoryAsync(string text, string scope, WorkspaceIdentity identity)
    {
        JObject arguments = MemoryUtilities.CreateMemoryArguments(text, scope, identity);
        McpServerDefinition? server = FindServer();
        if (server is null)
        {
            return MemoryUtilities.Unavailable(ServerUnavailable);
        }

        ReactiveMemoryCallResult? saved = await TryAddMemoryAsync(server, arguments).ConfigureAwait(false);
        return saved
            ?? await InvokeAsync(
                arguments,
                AddDrawerTool,
                AddDrawerToolAlias,
                "reactivememory_facts_add",
                "reactivememory_diary_write").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ReactiveMemoryCallResult> ScanWorkspaceAsync(WorkspaceIdentity identity, bool automatic)
    {
        if (!MemoryUtilities.HasWorkspaceRoot(identity))
        {
            return MemoryUtilities.Unavailable(
                "ReactiveMemory ProjectMiner scan skipped because the Visual Studio workspace root is unavailable.");
        }

        string key = MemoryUtilities.GetScanKey(identity, automatic);
        if (!CanScan(key))
        {
            return MemoryUtilities.Success("ReactiveMemory ProjectMiner scan already ran for this workspace.");
        }

        McpServerDefinition? server = FindServer();
        if (server is null)
        {
            return MemoryUtilities.Unavailable(ServerUnavailable);
        }

        try
        {
            IReadOnlyList<McpToolDefinition> tools = await _mcpTools.DiscoverToolsAsync(server).ConfigureAwait(false);
            ReactiveMemoryCallResult? mined = await TryMineProjectAsync(server, tools, identity, automatic).ConfigureAwait(false);
            return mined ?? await FileWorkspaceChunksAsync(server, tools, identity, automatic, key).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return MemoryUtilities.Unavailable(
                "ReactiveMemory ProjectMiner scan was cancelled by the host; continuing without blocking VSCodex.");
        }
        catch (IOException exception)
        {
            return MemoryUtilities.Unavailable($"ReactiveMemory ProjectMiner scan failed: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            return MemoryUtilities.Unavailable($"ReactiveMemory ProjectMiner scan failed: {exception.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<ReactiveMemoryCallResult> ScanWorkspaceAsync(WorkspaceIdentity identity) => ScanWorkspaceAsync(identity, automatic: false);

    /// <inheritdoc/>
    public async Task<ReactiveMemoryPauseCheckpointResult> SavePauseCheckpointAsync(ReactiveMemoryPauseCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        string validationError = MemoryUtilities.ValidateCheckpoint(checkpoint);
        if (!string.IsNullOrEmpty(validationError))
        {
            return MemoryUtilities.Failure("invalid_checkpoint", validationError, checkpoint);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return MemoryUtilities.Cancelled(checkpoint, "Pause checkpoint save was cancelled.");
        }

        checkpoint.State = PauseCheckpointState.Pending;
        McpServerDefinition? server = FindServer();
        if (server is null)
        {
            checkpoint.State = PauseCheckpointState.Failed;
            return MemoryUtilities.Failure("server_unavailable", ServerUnavailable, checkpoint);
        }

        try
        {
            JObject result = await InvokeCheckpointSaveAsync(server, checkpoint, cancellationToken).ConfigureAwait(false);
            checkpoint.MemoryDrawerId = MemoryUtilities.FindString(result, "drawerId");
            checkpoint.State = PauseCheckpointState.Saved;
            return MemoryUtilities.CreateCheckpointResult(true, "Pause checkpoint saved.", checkpoint, result);
        }
        catch (OperationCanceledException)
        {
            return MemoryUtilities.Cancelled(checkpoint, "Pause checkpoint save was cancelled.");
        }
        catch (InvalidOperationException exception)
        {
            checkpoint.State = PauseCheckpointState.Failed;
            return MemoryUtilities.Failure(
                "save_failed",
                $"ReactiveMemory could not save the pause checkpoint: {exception.Message}",
                checkpoint);
        }
    }

    /// <inheritdoc/>
    public async Task<ReactiveMemoryPauseCheckpointResult> RestorePauseCheckpointAsync(ReactiveMemoryPauseCheckpointQuery query, CancellationToken cancellationToken)
    {
        string validationError = MemoryUtilities.ValidateQuery(query);
        if (!string.IsNullOrEmpty(validationError))
        {
            return MemoryUtilities.Failure("invalid_query", validationError, null);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return MemoryUtilities.Cancelled(null, "Pause checkpoint restore was cancelled.");
        }

        McpServerDefinition? server = FindServer();
        if (server is null)
        {
            return MemoryUtilities.Failure("server_unavailable", ServerUnavailable, null);
        }

        try
        {
            JObject result = await InvokeCheckpointRestoreAsync(server, query, cancellationToken).ConfigureAwait(false);
            ReactiveMemoryPauseCheckpoint? checkpoint = MemoryUtilities.FindCheckpoint(result);
            return MemoryUtilities.CreateRestoreResult(checkpoint, query, result);
        }
        catch (OperationCanceledException)
        {
            return MemoryUtilities.Cancelled(null, "Pause checkpoint restore was cancelled.");
        }
        catch (InvalidOperationException exception)
        {
            return MemoryUtilities.Failure(
                "restore_failed",
                $"ReactiveMemory could not restore the pause checkpoint: {exception.Message}",
                null);
        }
    }

    /// <summary>Scores a configured MCP server by how closely it matches ReactiveMemory.</summary>
    /// <param name="server">The configured MCP server.</param>
    /// <returns>A score where higher values represent a closer match.</returns>
    private static int ScoreServer(McpServerDefinition server)
    {
        string name = server.Name ?? string.Empty;
        IEnumerable<string> arguments = server.Args ?? new List<string>();
        if (name.Equals("cp-reactivememory-mcp-server", StringComparison.OrdinalIgnoreCase))
        {
            return ExactServerNameScore;
        }

        if (arguments.Any(argument => argument.IndexOf("CP.ReactiveMemory.Mcp.Server@", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return ExactPackageScore;
        }

        if (arguments.Any(argument => argument.IndexOf("ReactiveMemory", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return PackageArgumentScore;
        }

        return name.IndexOf("reactivememory", StringComparison.OrdinalIgnoreCase) >= 0
            ? ServerNameFragmentScore
            : 0;
    }

    /// <summary>Finds the first tool matching any preferred tool name.</summary>
    /// <param name="tools">The discovered MCP tools.</param>
    /// <param name="names">The preferred tool names and aliases.</param>
    /// <returns>The first matching tool, or <see langword="null"/>.</returns>
    private static McpToolDefinition? FindTool(IReadOnlyList<McpToolDefinition> tools, params string[] names) =>
        tools.FirstOrDefault(tool => names.Any(name => Matches(tool, name)));

    /// <summary>Tests whether a tool name or description matches a preferred name.</summary>
    /// <param name="tool">The discovered MCP tool.</param>
    /// <param name="name">The preferred name or alias.</param>
    /// <returns><see langword="true"/> when the normalized values match.</returns>
    private static bool Matches(McpToolDefinition tool, string name)
    {
        string candidate = MemoryUtilities.Normalize(tool.Name);
        string expected = MemoryUtilities.Normalize(name);
        return candidate.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0
            || MemoryUtilities.Normalize(tool.Description).IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Adds an explicit memory through the best available dedicated tool.</summary>
    /// <param name="server">The ReactiveMemory MCP server.</param>
    /// <param name="arguments">The memory arguments.</param>
    /// <returns>The call result, or <see langword="null"/> when no dedicated tool is available.</returns>
    private async Task<ReactiveMemoryCallResult?> TryAddMemoryAsync(McpServerDefinition server, JObject arguments)
    {
        IReadOnlyList<McpToolDefinition> tools = await _mcpTools.DiscoverToolsAsync(server).ConfigureAwait(false);
        await TryDuplicateCheckAsync(server, tools, arguments).ConfigureAwait(false);
        McpToolDefinition? tool = FindTool(tools, AddDrawerTool, AddDrawerToolAlias, "reactivememory_facts_add");
        if (tool is null)
        {
            return null;
        }

        JObject? result = await _mcpTools.InvokeToolAsync(server, tool.Name, arguments).ConfigureAwait(false);
        return result is null
            ? null
            : new ReactiveMemoryCallResult
            {
                Success = true,
                Message = $"ReactiveMemory saved explicit memory through {tool.Name}",
                ContextText = MemoryUtilities.ExtractText(result),
                RawResult = result,
            };
    }

    /// <summary>Asks ReactiveMemory to detect a duplicate before filing a memory.</summary>
    /// <param name="server">The ReactiveMemory MCP server.</param>
    /// <param name="tools">The discovered MCP tools.</param>
    /// <param name="arguments">The candidate memory arguments.</param>
    /// <returns>A task representing the duplicate check.</returns>
    private async Task TryDuplicateCheckAsync(McpServerDefinition server, IReadOnlyList<McpToolDefinition> tools, JObject arguments)
    {
        McpToolDefinition? tool = FindTool(tools, "reactivememory_check_duplicate", "check_duplicate");
        if (tool is null)
        {
            return;
        }

        try
        {
            await _mcpTools.InvokeToolAsync(server, tool.Name, arguments).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>Runs the server's native project miner when available.</summary>
    /// <param name="server">The ReactiveMemory MCP server.</param>
    /// <param name="tools">The discovered MCP tools.</param>
    /// <param name="identity">The workspace identity.</param>
    /// <param name="automatic">Whether this is an automatic scan.</param>
    /// <returns>The native miner result, or <see langword="null"/> when unavailable.</returns>
    private async Task<ReactiveMemoryCallResult?> TryMineProjectAsync(McpServerDefinition server, IReadOnlyList<McpToolDefinition> tools, WorkspaceIdentity identity, bool automatic)
    {
        McpToolDefinition? tool = automatic
            ? null
            : FindTool(
                tools,
                "reactivememory_mine_project",
                "reactivememory_project_miner",
                "project_miner",
                "mine_project");
        if (tool is null)
        {
            return null;
        }

        JObject? result = await _mcpTools
            .InvokeToolAsync(server, tool.Name, MemoryUtilities.CreateProjectArguments(identity))
            .ConfigureAwait(false);
        return result is null
            ? MemoryUtilities.Unavailable("ReactiveMemory ProjectMiner returned no result.")
            : MemoryUtilities.Success($"ReactiveMemory ProjectMiner scanned {identity.RootPath}", result);
    }

    /// <summary>Files bounded safe workspace chunks when a native project miner is unavailable.</summary>
    /// <param name="server">The ReactiveMemory MCP server.</param>
    /// <param name="tools">The discovered MCP tools.</param>
    /// <param name="identity">The workspace identity.</param>
    /// <param name="automatic">Whether to apply automatic scan limits.</param>
    /// <param name="key">The workspace scan throttle key.</param>
    /// <returns>The aggregate chunk-filing result.</returns>
    private async Task<ReactiveMemoryCallResult> FileWorkspaceChunksAsync(McpServerDefinition server, IReadOnlyList<McpToolDefinition> tools, WorkspaceIdentity identity, bool automatic, string key)
    {
        string toolName = FindTool(tools, AddDrawerTool, AddDrawerToolAlias)?.Name ?? AddDrawerTool;
        List<McpToolInvocation> invocations = MemoryUtilities
            .BuildChunkInvocations(identity, toolName, automatic)
            .ToList();
        if (invocations.Count == 0)
        {
            RememberScan(key);
            return MemoryUtilities.Success("ReactiveMemory ProjectMiner scan found no safe text files to mine.");
        }

        TimeSpan timeout = automatic
            ? TimeSpan.FromSeconds(AutomaticScanTimeoutSeconds)
            : TimeSpan.FromMinutes(ManualScanTimeoutMinutes);
        IReadOnlyList<JObject?> responses = await _mcpTools.InvokeToolsAsync(server, invocations, timeout).ConfigureAwait(false);
        RememberScan(key);
        int completed = responses.Count(response => response is not null && response["error"] is null);
        return new ReactiveMemoryCallResult
        {
            Success = completed > 0,
            Message = $"ReactiveMemory ProjectMiner-compatible scan filed {completed} chunk(s) from {identity.RootPath}.",
            RawResult = new JObject
            {
                ["requested"] = invocations.Count,
                [nameof(completed)] = completed,
            },
        };
    }

    /// <summary>Saves a pause checkpoint through the best available add-drawer tool.</summary>
    /// <param name="server">The ReactiveMemory MCP server.</param>
    /// <param name="checkpoint">The checkpoint to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The raw MCP response.</returns>
    private async Task<JObject> InvokeCheckpointSaveAsync(McpServerDefinition server, ReactiveMemoryPauseCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        IReadOnlyList<McpToolDefinition> tools = await _mcpTools.DiscoverToolsAsync(server).WithCancellation(cancellationToken).ConfigureAwait(false);
        string toolName = FindTool(tools, AddDrawerTool, AddDrawerToolAlias)?.Name ?? AddDrawerTool;
        JObject? result = await _mcpTools
            .InvokeToolAsync(server, toolName, MemoryUtilities.CreateCheckpointArguments(checkpoint))
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("ReactiveMemory returned no result while saving the pause checkpoint.");
    }

    /// <summary>Restores a pause checkpoint through the best available retrieval tool.</summary>
    /// <param name="server">The ReactiveMemory MCP server.</param>
    /// <param name="query">The checkpoint correlation query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The raw MCP response.</returns>
    private async Task<JObject> InvokeCheckpointRestoreAsync(McpServerDefinition server, ReactiveMemoryPauseCheckpointQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<McpToolDefinition> tools = await _mcpTools.DiscoverToolsAsync(server).WithCancellation(cancellationToken).ConfigureAwait(false);
        McpToolDefinition? tool = MemoryUtilities.SelectRestoreTool(tools, query);
        string toolName = tool?.Name
            ?? (string.IsNullOrWhiteSpace(query.MemoryDrawerId) ? SearchTool : "reactivememory_get_drawer");
        JObject? result = await _mcpTools
            .InvokeToolAsync(server, toolName, MemoryUtilities.CreateRestoreArguments(query))
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("ReactiveMemory returned no result while restoring the pause checkpoint.");
    }

    /// <summary>Invokes the first available tool from an ordered set of aliases.</summary>
    /// <param name="arguments">The MCP tool arguments.</param>
    /// <param name="toolNames">The preferred tool names and aliases.</param>
    /// <returns>The normalized ReactiveMemory call result.</returns>
    private async Task<ReactiveMemoryCallResult> InvokeAsync(JObject arguments, params string[] toolNames)
    {
        McpServerDefinition? server = FindServer();
        if (server is null)
        {
            return MemoryUtilities.Unavailable(ServerUnavailable);
        }

        try
        {
            IReadOnlyList<McpToolDefinition> tools = await _mcpTools.DiscoverToolsAsync(server).ConfigureAwait(false);
            string? toolName = FindTool(tools, toolNames)?.Name
                ?? toolNames.FirstOrDefault(name => name.StartsWith("reactivememory_", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return MemoryUtilities.Unavailable(
                    $"ReactiveMemory MCP server did not expose the required tool: {string.Join(", ", toolNames)}");
            }

            JObject? result = await _mcpTools.InvokeToolAsync(server, toolName, arguments).ConfigureAwait(false);
            return result is null
                ? MemoryUtilities.Unavailable($"ReactiveMemory returned no result through {toolName}.")
                : new ReactiveMemoryCallResult
                {
                    Success = true,
                    Message = $"ReactiveMemory updated through {toolName}",
                    ContextText = MemoryUtilities.ExtractText(result),
                    RawResult = result,
                };
        }
        catch (OperationCanceledException)
        {
            return MemoryUtilities.Unavailable(
                "ReactiveMemory MCP call was cancelled by the host; continuing without blocking VSCodex.");
        }
        catch (InvalidOperationException exception)
        {
            return MemoryUtilities.Unavailable($"ReactiveMemory unavailable: {exception.Message}");
        }
    }

    /// <summary>Finds the highest-scoring enabled ReactiveMemory server.</summary>
    /// <returns>The selected server, or <see langword="null"/>.</returns>
    private McpServerDefinition? FindServer() =>
        _mcpConfig.Snapshot
            .Where(server => server.IsEnabled)
            .Select(server => (Server: server, Score: ScoreServer(server)))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => candidate.Server)
            .FirstOrDefault();

    /// <summary>Determines whether the workspace scan cooldown has expired.</summary>
    /// <param name="key">The scan throttle key.</param>
    /// <returns><see langword="true"/> when a scan may start.</returns>
    private bool CanScan(string key) =>
        !LastWorkspaceScans.TryGetValue(key, out DateTimeOffset scanned)
        || _timeProvider.GetUtcNow() - scanned >= TimeSpan.FromMinutes(ScanCooldownMinutes);

    /// <summary>Records the current time as the last successful workspace scan.</summary>
    /// <param name="key">The scan throttle key.</param>
    private void RememberScan(string key) => LastWorkspaceScans[key] = _timeProvider.GetUtcNow();

    /// <summary>Provides pure transformations for ReactiveMemory arguments and results.</summary>
    internal static class MemoryUtilities
    {
        /// <summary>The MCP argument key for the calling agent name.</summary>
        private const string AgentNameArgument = "agentName";

        /// <summary>The name reported to ReactiveMemory for VSCodex operations.</summary>
        private const string AgentName = "VSCodex";

        /// <summary>The MCP argument key for the identity of the current chat.</summary>
        private const string ChatIdArgument = "chatId";

        /// <summary>The MCP argument key for a serialized checkpoint.</summary>
        private const string CheckpointArgument = "checkpoint";

        /// <summary>The MCP argument key for persisted memory content.</summary>
        private const string ContentArgument = "content";

        /// <summary>The MCP argument key for the durable memory root.</summary>
        private const string MemoryRootArgument = "memoryRoot";

        /// <summary>The MCP argument key for the current operation identity.</summary>
        private const string OperationIdArgument = "operationId";

        /// <summary>The MCP argument key for the current project sector.</summary>
        private const string SectorArgument = "sector";

        /// <summary>The MCP argument key for the source file.</summary>
        private const string SourceFileArgument = "sourceFile";

        /// <summary>The MCP argument key for the current thread identity.</summary>
        private const string ThreadIdArgument = "threadId";

        /// <summary>The MCP argument key for the current turn identity.</summary>
        private const string TurnIdArgument = "turnId";

        /// <summary>The MCP argument key for the current memory vault.</summary>
        private const string VaultArgument = "vault";

        /// <summary>The MCP argument key for the stable workspace identity.</summary>
        private const string WorkspaceIdentityArgument = "workspaceIdentity";

        /// <summary>The MCP argument key for the workspace root.</summary>
        private const string WorkspaceRootArgument = "workspaceRoot";

        /// <summary>The MCP argument key for the actor that added a memory.</summary>
        private const string AddedByArgument = "addedBy";

        /// <summary>The maximum number of chunks filed by an automatic scan.</summary>
        private const int AutomaticChunkLimit = 32;

        /// <summary>The maximum number of files examined by an automatic scan.</summary>
        private const int AutomaticFileLimit = 24;

        /// <summary>The maximum number of string result items included as prompt context.</summary>
        private const int ContextItemLimit = 8;

        /// <summary>The maximum number of chunks filed by a manual scan.</summary>
        private const int ManualChunkLimit = 320;

        /// <summary>The maximum number of files examined by a manual scan.</summary>
        private const int ManualFileLimit = 180;

        /// <summary>The largest file, in bytes, that a workspace scan can read.</summary>
        private const int MaximumContentLength = 200_000;

        /// <summary>The maximum prompt length included in a diary entry.</summary>
        private const int PromptLimit = 1_200;

        /// <summary>The maximum response length included in a diary entry.</summary>
        private const int ResponseLimit = 2_400;

        /// <summary>The number of characters in each mined source chunk.</summary>
        private const int TextChunkLength = 800;

        /// <summary>The maximum length of extracted context returned to the caller.</summary>
        private const int ExtractedContextLimit = 12_000;

        /// <summary>The minimum length of a file or chunk eligible for mining.</summary>
        private const int MinimumMineableLength = 50;

        /// <summary>The number of restore matches requested from ReactiveMemory.</summary>
        private const int RestoreResultLimit = 5;

        /// <summary>The file extensions that are safe for workspace text mining.</summary>
        private static readonly string[] SafeFileExtensions =
        [
            ".cs",
            ".xaml",
            ".csproj",
            ".props",
            ".targets",
            ".json",
            ".xml",
            ".md",
            ".txt",
            ".yml",
            ".yaml",
            ".ps1",
            ".sln",
            ".slnx",
            ".config",
            ".js",
            ".ts",
            ".css",
            ".html",
            ".razor",
        ];

        /// <summary>Creates the workspace correlation arguments shared by memory operations.</summary>
        /// <param name="identity">The workspace identity.</param>
        /// <param name="threadId">The optional Codex thread identity.</param>
        /// <returns>The shared MCP arguments.</returns>
        internal static JObject CreateWorkspaceArguments(WorkspaceIdentity identity, string? threadId) =>
            new()
            {
                [AgentNameArgument] = AgentName,
                [SectorArgument] = Sector(identity),
                [VaultArgument] = Vault(identity),
                [WorkspaceIdentityArgument] = identity?.Id ?? string.Empty,
                [WorkspaceRootArgument] = identity?.RootPath ?? string.Empty,
                [MemoryRootArgument] = identity?.MemoryRoot ?? string.Empty,
                [ThreadIdArgument] = threadId ?? string.Empty,
            };

        /// <summary>Creates arguments for filing an explicit user memory.</summary>
        /// <param name="text">The memory content.</param>
        /// <param name="scope">The memory scope.</param>
        /// <param name="identity">The workspace identity.</param>
        /// <returns>The add-memory MCP arguments.</returns>
        internal static JObject CreateMemoryArguments(string text, string scope, WorkspaceIdentity identity)
        {
            JObject arguments = CreateWorkspaceArguments(identity, null);
            arguments[TopicArgument] = $"{Sector(identity)} {scope} memory";
            arguments["drawer"] = scope ?? string.Empty;
            arguments[nameof(scope)] = scope ?? string.Empty;
            arguments[ContentArgument] = text ?? string.Empty;
            arguments[SourceFileArgument] = "VSCodex explicit memory";
            arguments[AddedByArgument] = AgentName;
            arguments["entry"] =
                $"Workspace identity: {identity?.Id ?? string.Empty}{Environment.NewLine}"
                + $"Workspace root: {identity?.RootPath ?? string.Empty}{Environment.NewLine}"
                + $"{Environment.NewLine}{text ?? string.Empty}";
            return arguments;
        }

        /// <summary>Creates arguments for a native ReactiveMemory project-miner request.</summary>
        /// <param name="identity">The workspace identity.</param>
        /// <returns>The project-miner MCP arguments.</returns>
        internal static JObject CreateProjectArguments(WorkspaceIdentity identity) =>
            new()
            {
                ["projectRoot"] = identity.RootPath,
                [WorkspaceRootArgument] = identity.RootPath,
                ["solutionPath"] = identity.SolutionPath ?? string.Empty,
                [SectorArgument] = Sector(identity),
                [AgentNameArgument] = AgentName,
                [WorkspaceIdentityArgument] = identity.Id ?? string.Empty,
                [MemoryRootArgument] = identity.MemoryRoot ?? string.Empty,
                [VaultArgument] = Vault(identity),
            };

        /// <summary>Creates arguments for persisting a paused-turn checkpoint.</summary>
        /// <param name="checkpoint">The checkpoint to serialize.</param>
        /// <returns>The add-drawer MCP arguments.</returns>
        internal static JObject CreateCheckpointArguments(ReactiveMemoryPauseCheckpoint checkpoint) =>
            new()
            {
                [AgentNameArgument] = AgentName,
                [TopicArgument] = $"Paused Codex turn {checkpoint.OperationId}",
                [SectorArgument] = CheckpointSector(checkpoint.WorkspaceName),
                [VaultArgument] = "pause-checkpoints",
                ["drawer"] = checkpoint.CheckpointId,
                ["scope"] = "pause-checkpoint",
                [ContentArgument] = new JObject
                {
                    ["schema"] = "vscodex.pause-checkpoint/1",
                    [CheckpointArgument] = JObject.FromObject(checkpoint),
                }.ToString(Formatting.None),
                [SourceFileArgument] = "VSCodex pause checkpoint",
                [AddedByArgument] = AgentName,
                [WorkspaceIdentityArgument] = checkpoint.WorkspaceIdentityId,
                [WorkspaceRootArgument] = checkpoint.WorkspaceRoot,
                [MemoryRootArgument] = checkpoint.MemoryRoot,
                [ChatIdArgument] = checkpoint.ChatId,
                [ThreadIdArgument] = checkpoint.ThreadId,
                [TurnIdArgument] = checkpoint.TurnId,
                [OperationIdArgument] = checkpoint.OperationId,
            };

        /// <summary>Creates arguments for restoring a paused-turn checkpoint.</summary>
        /// <param name="query">The checkpoint correlation query.</param>
        /// <returns>The retrieval MCP arguments.</returns>
        internal static JObject CreateRestoreArguments(ReactiveMemoryPauseCheckpointQuery query) =>
            new()
            {
                [nameof(query)] = SearchQuery(query),
                [SectorArgument] = CheckpointSector(query.WorkspaceName),
                [VaultArgument] = "pause-checkpoints",
                ["limit"] = RestoreResultLimit,
                ["drawerId"] = query.MemoryDrawerId,
                [WorkspaceIdentityArgument] = query.WorkspaceIdentityId,
                [WorkspaceRootArgument] = query.WorkspaceRoot,
                [MemoryRootArgument] = query.MemoryRoot,
                [ChatIdArgument] = query.ChatId,
                [ThreadIdArgument] = query.ThreadId,
                [TurnIdArgument] = query.TurnId,
                [OperationIdArgument] = query.OperationId,
            };

        /// <summary>Builds bounded add-drawer invocations for safe workspace text.</summary>
        /// <param name="identity">The workspace identity.</param>
        /// <param name="toolName">The add-drawer tool name.</param>
        /// <param name="automatic">Whether to apply automatic scan limits.</param>
        /// <returns>The lazily generated chunk invocations.</returns>
        internal static IEnumerable<McpToolInvocation> BuildChunkInvocations(WorkspaceIdentity identity, string toolName, bool automatic)
        {
            int fileLimit = automatic ? AutomaticFileLimit : ManualFileLimit;
            int chunkLimit = automatic ? AutomaticChunkLimit : ManualChunkLimit;
            int chunks = 0;
            foreach (string file in EnumerateFiles(identity.RootPath, fileLimit))
            {
                foreach (string chunk in ReadChunks(file))
                {
                    chunks++;
                    if (chunks > chunkLimit)
                    {
                        yield break;
                    }

                    McpToolInvocation invocation = new() { ToolName = toolName };
                    invocation.Arguments[SectorArgument] = Sector(identity);
                    invocation.Arguments[VaultArgument] = FileVault(file);
                    invocation.Arguments["workspaceVault"] = Vault(identity);
                    invocation.Arguments[WorkspaceIdentityArgument] = identity.Id ?? string.Empty;
                    invocation.Arguments[MemoryRootArgument] = identity.MemoryRoot ?? string.Empty;
                    invocation.Arguments[ContentArgument] = chunk;
                    invocation.Arguments[SourceFileArgument] = Relative(identity.RootPath, file);
                    invocation.Arguments[AddedByArgument] = "project_miner";
                    yield return invocation;
                }
            }
        }

        /// <summary>Enumerates at most the requested number of safe workspace files.</summary>
        /// <param name="root">The workspace root.</param>
        /// <param name="limit">The maximum number of files to yield.</param>
        /// <returns>The safe file paths.</returns>
        internal static IEnumerable<string> EnumerateFiles(string root, int limit)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                yield break;
            }

            int count = 0;
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (count >= limit)
                {
                    yield break;
                }

                if (IsSafeFile(file))
                {
                    count++;
                    yield return file;
                }
            }
        }

        /// <summary>Reads a file as bounded text chunks.</summary>
        /// <param name="file">The text file to read.</param>
        /// <returns>The non-trivial text chunks.</returns>
        internal static IEnumerable<string> ReadChunks(string file)
        {
            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch (IOException)
            {
                yield break;
            }

            for (int start = 0; start < content.Length; start += TextChunkLength)
            {
                int length = Math.Min(TextChunkLength, content.Length - start);
                string chunk = content.Remove(start + length).Remove(0, start).Trim();
                if (chunk.Length >= MinimumMineableLength)
                {
                    yield return chunk;
                }
            }
        }

        /// <summary>Determines whether a file contains bounded safe text suitable for mining.</summary>
        /// <param name="file">The candidate file.</param>
        /// <returns><see langword="true"/> when the extension and length are safe.</returns>
        internal static bool IsSafeFile(string file)
        {
            string extension = Path.GetExtension(file);
            long length = new FileInfo(file).Length;
            return SafeFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
                && length is > MinimumMineableLength and <= MaximumContentLength;
        }

        /// <summary>Creates the correlated result for a restored checkpoint.</summary>
        /// <param name="checkpoint">The checkpoint returned by ReactiveMemory.</param>
        /// <param name="query">The original correlation query.</param>
        /// <param name="result">The raw MCP response.</param>
        /// <returns>The normalized checkpoint result.</returns>
        internal static ReactiveMemoryPauseCheckpointResult CreateRestoreResult(
            ReactiveMemoryPauseCheckpoint? checkpoint,
            ReactiveMemoryPauseCheckpointQuery query,
            JObject result)
        {
            if (checkpoint is null)
            {
                return CreateCheckpointResult(
                    false,
                    "ReactiveMemory did not return a complete VSCodex pause checkpoint.",
                    null,
                    result,
                    "checkpoint_not_found");
            }

            string mismatch = CorrelationMismatch(checkpoint, query);
            if (!string.IsNullOrEmpty(mismatch))
            {
                return CreateCheckpointResult(
                    false,
                    $"ReactiveMemory returned a pause checkpoint for a different {mismatch}.",
                    checkpoint,
                    result,
                    "correlation_mismatch");
            }

            checkpoint.MemoryDrawerId = string.IsNullOrWhiteSpace(checkpoint.MemoryDrawerId)
                ? query.MemoryDrawerId
                : checkpoint.MemoryDrawerId;
            checkpoint.State = PauseCheckpointState.Restored;
            return CreateCheckpointResult(true, "Pause checkpoint restored.", checkpoint, result);
        }

        /// <summary>Selects the best available tool for the supplied checkpoint query.</summary>
        /// <param name="tools">The discovered MCP tools.</param>
        /// <param name="query">The checkpoint correlation query.</param>
        /// <returns>The best retrieval tool, or <see langword="null"/>.</returns>
        internal static McpToolDefinition? SelectRestoreTool(
            IReadOnlyList<McpToolDefinition> tools,
            ReactiveMemoryPauseCheckpointQuery query) =>
            !string.IsNullOrWhiteSpace(query.MemoryDrawerId)
                ? FindTool(tools, "reactivememory_get_drawer", "get_drawer")
                    ?? FindTool(tools, SearchTool, "search")
                : FindTool(tools, SearchTool, "search", "reactivememory_memory_get_relevant");

        /// <summary>Validates the fields required to persist a checkpoint.</summary>
        /// <param name="checkpoint">The candidate checkpoint.</param>
        /// <returns>An error message, or an empty string when valid.</returns>
        internal static string ValidateCheckpoint(ReactiveMemoryPauseCheckpoint? checkpoint)
        {
            if (checkpoint is null)
            {
                return "A pause checkpoint is required.";
            }

            if (string.IsNullOrWhiteSpace(checkpoint.CheckpointId))
            {
                return "A pause checkpoint ID is required.";
            }

            if (string.IsNullOrWhiteSpace(checkpoint.WorkspaceIdentityId) && string.IsNullOrWhiteSpace(checkpoint.WorkspaceRoot))
            {
                return "A workspace identity or workspace root is required for a pause checkpoint.";
            }

            if (string.IsNullOrWhiteSpace(checkpoint.ChatId) && string.IsNullOrWhiteSpace(checkpoint.ThreadId))
            {
                return "A chat ID or Codex thread ID is required for a pause checkpoint.";
            }

            return string.IsNullOrWhiteSpace(checkpoint.TurnId) && string.IsNullOrWhiteSpace(checkpoint.OperationId)
                ? "A turn ID or operation ID is required for a pause checkpoint."
                : string.Empty;
        }

        /// <summary>Validates the correlation fields required to restore a checkpoint.</summary>
        /// <param name="query">The candidate restore query.</param>
        /// <returns>An error message, or an empty string when valid.</returns>
        internal static string ValidateQuery(ReactiveMemoryPauseCheckpointQuery? query)
        {
            if (query is null)
            {
                return "A pause checkpoint query is required.";
            }

            if (string.IsNullOrWhiteSpace(query.CheckpointId) && string.IsNullOrWhiteSpace(query.MemoryDrawerId))
            {
                return "A pause checkpoint ID or ReactiveMemory drawer ID is required.";
            }

            if (string.IsNullOrWhiteSpace(query.WorkspaceIdentityId) && string.IsNullOrWhiteSpace(query.WorkspaceRoot))
            {
                return "A workspace identity or workspace root is required to restore a pause checkpoint.";
            }

            bool hasNoCorrelation =
                string.IsNullOrWhiteSpace(query.ChatId)
                && string.IsNullOrWhiteSpace(query.ThreadId)
                && string.IsNullOrWhiteSpace(query.TurnId)
                && string.IsNullOrWhiteSpace(query.OperationId);
            return hasNoCorrelation
                ? "At least one chat, thread, turn, or operation correlation ID is required to restore a pause checkpoint."
                : string.Empty;
        }

        /// <summary>Finds and deserializes the first complete checkpoint in an MCP result.</summary>
        /// <param name="token">The result token to inspect recursively.</param>
        /// <returns>The first complete checkpoint, or <see langword="null"/>.</returns>
        internal static ReactiveMemoryPauseCheckpoint? FindCheckpoint(JToken token)
        {
            if (token is JObject objectToken)
            {
                if (objectToken[CheckpointArgument] is JObject checkpoint)
                {
                    return checkpoint.ToObject<ReactiveMemoryPauseCheckpoint>();
                }

                return objectToken.Properties().Any(
                    property => property.Name.Equals("checkpointId", StringComparison.OrdinalIgnoreCase))
                    ? objectToken.ToObject<ReactiveMemoryPauseCheckpoint>()
                    : objectToken
                        .Properties()
                        .Select(property => FindCheckpoint(property.Value))
                        .FirstOrDefault(value => value is not null);
            }

            if (token is JArray array)
            {
                return array.Select(FindCheckpoint).FirstOrDefault(value => value is not null);
            }

            return token.Type == JTokenType.String
                ? ParseCheckpoint(token.Value<string>())
                : null;
        }

        /// <summary>Parses a checkpoint embedded in a string-valued MCP result.</summary>
        /// <param name="value">The string that may contain checkpoint JSON.</param>
        /// <returns>The parsed checkpoint, or <see langword="null"/>.</returns>
        internal static ReactiveMemoryPauseCheckpoint? ParseCheckpoint(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string text = value ?? string.Empty;
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            try
            {
                return FindCheckpoint(JToken.Parse(text.Substring(start, (end - start) + 1)));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Finds the first non-empty string value for a property in a JSON tree.</summary>
        /// <param name="token">The JSON tree to inspect.</param>
        /// <param name="propertyName">The property name to match.</param>
        /// <returns>The first matching string, or an empty string.</returns>
        internal static string FindString(JToken token, string propertyName)
        {
            JProperty? property = (token as JObject)?.Properties().FirstOrDefault(
                candidate => candidate.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            return property is not null
                ? property.Value.Value<string>() ?? string.Empty
                : token
                    .Children()
                    .Select(child => FindString(child, propertyName))
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    ?? string.Empty;
        }

        /// <summary>Extracts bounded text context from a ReactiveMemory result.</summary>
        /// <param name="result">The raw MCP result.</param>
        /// <returns>The bounded context text.</returns>
        internal static string ExtractText(JObject result)
        {
            IEnumerable<string?> values = result
                .DescendantsAndSelf()
                .OfType<JValue>()
                .Where(value => value.Type == JTokenType.String)
                .Select(value => value.Value<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(ContextItemLimit);
            return Trim(string.Join(Environment.NewLine, values), ExtractedContextLimit);
        }

        /// <summary>Builds the workspace-scoped prompt filed before a Codex turn.</summary>
        /// <param name="prompt">The current user prompt.</param>
        /// <param name="identity">The workspace identity.</param>
        /// <returns>The workspace-scoped prompt.</returns>
        internal static string BuildPrompt(string prompt, WorkspaceIdentity identity) =>
            $"Workspace identity: {identity?.Id ?? string.Empty}{Environment.NewLine}"
            + $"Workspace root: {identity?.RootPath ?? string.Empty}{Environment.NewLine}"
            + $"ReactiveMemory scope: {identity?.MemoryRoot ?? string.Empty}{Environment.NewLine}"
            + $"Solution: {identity?.SolutionRelativePath ?? string.Empty}{Environment.NewLine}"
            + $"{Environment.NewLine}{prompt ?? string.Empty}";

        /// <summary>Builds the bounded prompt and response text filed in the diary.</summary>
        /// <param name="prompt">The user prompt.</param>
        /// <param name="response">The assistant response.</param>
        /// <param name="identity">The workspace identity.</param>
        /// <param name="threadId">The optional Codex thread identity.</param>
        /// <returns>The bounded diary entry.</returns>
        internal static string BuildDiaryEntry(
            string prompt,
            string response,
            WorkspaceIdentity identity,
            string? threadId) =>
            $"Workspace identity: {identity?.Id ?? string.Empty}{Environment.NewLine}"
            + $"Workspace root: {identity?.RootPath ?? string.Empty}{Environment.NewLine}"
            + $"Thread: {threadId ?? string.Empty}{Environment.NewLine}"
            + $"{Environment.NewLine}Prompt: {Trim(prompt, PromptLimit)}{Environment.NewLine}"
            + $"{Environment.NewLine}Response: {Trim(response, ResponseLimit)}";

        /// <summary>Returns the first correlation dimension that differs from the restore query.</summary>
        /// <param name="checkpoint">The restored checkpoint.</param>
        /// <param name="query">The expected correlation values.</param>
        /// <returns>The mismatched correlation dimension, or an empty string.</returns>
        internal static string CorrelationMismatch(
            ReactiveMemoryPauseCheckpoint checkpoint,
            ReactiveMemoryPauseCheckpointQuery query) =>
            FirstMismatch(
                (checkpoint.CheckpointId, query.CheckpointId, "checkpoint ID"),
                (checkpoint.WorkspaceIdentityId, query.WorkspaceIdentityId, "workspace identity"),
                (checkpoint.ChatId, query.ChatId, "chat"),
                (checkpoint.ThreadId, query.ThreadId, "thread"),
                (checkpoint.TurnId, query.TurnId, "turn"),
                (checkpoint.OperationId, query.OperationId, "operation"));

        /// <summary>Returns the name of the first mismatched expected correlation value.</summary>
        /// <param name="values">The actual, expected, and display-name tuples.</param>
        /// <returns>The first mismatched display name, or an empty string.</returns>
        internal static string FirstMismatch(params (string Actual, string Expected, string Name)[] values) =>
            values.FirstOrDefault(
                value =>
                    !string.IsNullOrWhiteSpace(value.Expected)
                    && !string.Equals(value.Actual, value.Expected, StringComparison.Ordinal)).Name
            ?? string.Empty;

        /// <summary>Builds a bounded correlation query for locating a saved checkpoint.</summary>
        /// <param name="query">The checkpoint correlation query.</param>
        /// <returns>The search query text.</returns>
        internal static string SearchQuery(ReactiveMemoryPauseCheckpointQuery query)
        {
            string[] terms =
            [
                "schema:vscodex.pause-checkpoint/1",
            $"checkpointId:{query.CheckpointId}",
            $"workspaceIdentity:{query.WorkspaceIdentityId}",
            $"chatId:{query.ChatId}",
            $"threadId:{query.ThreadId}",
            $"turnId:{query.TurnId}",
            $"operationId:{query.OperationId}",
        ];
            return string.Join(" ", terms.Where(value => !value.EndsWith(":", StringComparison.Ordinal)));
        }

        /// <summary>Returns the workspace-specific ReactiveMemory sector.</summary>
        /// <param name="identity">The workspace identity.</param>
        /// <returns>The workspace sector.</returns>
        internal static string Sector(WorkspaceIdentity? identity) =>
            string.IsNullOrWhiteSpace(identity?.Name) ? "VSCodex workspace" : identity?.Name ?? string.Empty;

        /// <summary>Returns the workspace-specific ReactiveMemory vault.</summary>
        /// <param name="identity">The workspace identity.</param>
        /// <returns>The workspace vault.</returns>
        internal static string Vault(WorkspaceIdentity? identity) =>
            string.IsNullOrWhiteSpace(identity?.MemoryRoot)
                ? identity?.RootPath ?? string.Empty
                : identity?.MemoryRoot ?? string.Empty;

        /// <summary>Returns the sector used for a workspace checkpoint.</summary>
        /// <param name="workspaceName">The workspace display name.</param>
        /// <returns>The checkpoint sector.</returns>
        internal static string CheckpointSector(string workspaceName) =>
            string.IsNullOrWhiteSpace(workspaceName) ? "VSCodex workspace" : workspaceName;

        /// <summary>Classifies a mined file into a stable memory vault.</summary>
        /// <param name="file">The mined file path.</param>
        /// <returns>The classified vault.</returns>
        internal static string FileVault(string file)
        {
            if (file.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "tests";
            }

            if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                return "docs";
            }

            return file.IndexOf("mcp", StringComparison.OrdinalIgnoreCase) >= 0 ? "mcp" : "source";
        }

        /// <summary>Returns a source path relative to the workspace root when possible.</summary>
        /// <param name="root">The workspace root.</param>
        /// <param name="path">The source file path.</param>
        /// <returns>The relative or original path.</returns>
        internal static string Relative(string root, string path)
        {
            if (string.IsNullOrWhiteSpace(root) || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path
                .Substring(normalizedRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>Trims text to a maximum length and appends an ellipsis when needed.</summary>
        /// <param name="value">The source text.</param>
        /// <param name="maximum">The maximum number of source characters.</param>
        /// <returns>The bounded text.</returns>
        internal static string Trim(string? value, int maximum) =>
            value is { Length: > 0 } text && text.Length > maximum
                ? $"{text.Remove(maximum)}..."
                : value ?? string.Empty;

        /// <summary>Normalizes an MCP tool name for fuzzy comparison.</summary>
        /// <param name="value">The tool name or description.</param>
        /// <returns>The lowercase alphanumeric representation.</returns>
        internal static string Normalize(string? value) =>
            new((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());

        /// <summary>Determines whether the workspace has an existing root directory.</summary>
        /// <param name="identity">The workspace identity.</param>
        /// <returns><see langword="true"/> when the root exists.</returns>
        internal static bool HasWorkspaceRoot(WorkspaceIdentity? identity)
        {
            string root = identity?.RootPath ?? string.Empty;
            return !string.IsNullOrWhiteSpace(root) && Directory.Exists(root);
        }

        /// <summary>Creates a successful ReactiveMemory call result.</summary>
        /// <param name="message">The user-facing result message.</param>
        /// <param name="result">The optional raw MCP result.</param>
        /// <returns>The successful call result.</returns>
        internal static ReactiveMemoryCallResult Success(string message, JObject? result = null) =>
            new()
            {
                Success = true,
                Message = message,
                RawResult = result,
            };

        /// <summary>Creates an unavailable ReactiveMemory call result.</summary>
        /// <param name="message">The user-facing result message.</param>
        /// <returns>The unavailable call result.</returns>
        internal static ReactiveMemoryCallResult Unavailable(string message) =>
            new()
            {
                Success = false,
                Message = message,
            };

        /// <summary>Creates a failed pause-checkpoint result.</summary>
        /// <param name="code">The stable error code.</param>
        /// <param name="message">The user-facing result message.</param>
        /// <param name="checkpoint">The related checkpoint, if available.</param>
        /// <returns>The failed checkpoint result.</returns>
        internal static ReactiveMemoryPauseCheckpointResult Failure(
            string code,
            string message,
            ReactiveMemoryPauseCheckpoint? checkpoint) =>
            new()
            {
                ErrorCode = code,
                Message = message,
                Checkpoint = checkpoint,
            };

        /// <summary>Creates a cancelled pause-checkpoint result.</summary>
        /// <param name="checkpoint">The related checkpoint, if available.</param>
        /// <param name="message">The user-facing cancellation message.</param>
        /// <returns>The cancelled checkpoint result.</returns>
        internal static ReactiveMemoryPauseCheckpointResult Cancelled(
            ReactiveMemoryPauseCheckpoint? checkpoint,
            string message) =>
            new()
            {
                IsCancelled = true,
                ErrorCode = "cancelled",
                Message = message,
                Checkpoint = checkpoint,
            };

        /// <summary>Creates a checkpoint result and attaches its raw MCP response.</summary>
        /// <param name="success">Whether the operation succeeded.</param>
        /// <param name="message">The user-facing result message.</param>
        /// <param name="checkpoint">The related checkpoint, if available.</param>
        /// <param name="result">The raw MCP result.</param>
        /// <param name="errorCode">The optional stable error code.</param>
        /// <returns>The populated checkpoint result.</returns>
        internal static ReactiveMemoryPauseCheckpointResult CreateCheckpointResult(
            bool success,
            string message,
            ReactiveMemoryPauseCheckpoint? checkpoint,
            JObject result,
            string errorCode = "")
        {
            ReactiveMemoryPauseCheckpointResult response = new()
            {
                Success = success,
                Message = message,
                Checkpoint = checkpoint,
                ErrorCode = errorCode,
            };
            response.SetRawResult(result);
            return response;
        }

        /// <summary>Builds the throttle key for a workspace and scheduling mode.</summary>
        /// <param name="identity">The workspace identity.</param>
        /// <param name="automatic">Whether this is an automatic scan.</param>
        /// <returns>The scan throttle key.</returns>
        internal static string GetScanKey(WorkspaceIdentity identity, bool automatic) =>
            $"{(automatic ? "automatic" : "manual")}|{identity.Id ?? identity.RootPath}";
    }
}
