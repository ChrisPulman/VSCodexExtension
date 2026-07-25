// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI;

namespace VSCodex.ViewModels;

/// <summary>Maintains prompt suggestions, analytics, and workspace settings snapshots.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Updates skills.</summary>
    /// <param name="items">The items.</param>
    private void UpdateSkills(IReadOnlyList<SkillDefinition> items)
    {
        HashSet<string> enabledPaths = new(_settingsStore.Current.EnabledSkillPaths ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (SkillDefinition item in items)
        {
            item.IsEnabled = enabledPaths.Contains(item.MarkdownPath);
        }

        Replace(Skills, items);
    }

    /// <summary>Updates memories.</summary>
    /// <param name="items">The items.</param>
    private void UpdateMemories(IReadOnlyList<MemoryEntry> items)
    {
        Replace(Memories, items);
    }

    /// <summary>Updates mcp Servers.</summary>
    /// <param name="items">The items.</param>
    private void UpdateMcpServers(IReadOnlyList<McpServerDefinition> items)
    {
        Replace(McpServers, items);
    }

    /// <summary>Updates reference Suggestions.</summary>
    /// <param name="prompt">The prompt.</param>
    private void UpdateReferenceSuggestions(string prompt)
    {
        string? token = LastReferenceToken(prompt);
        if (token?.StartsWith("@", StringComparison.Ordinal) == true)
        {
            Replace(FileSuggestions, _workspace.SearchFiles(token, Numeric16));
        }
        else
        {
            Replace(FileSuggestions, []);
        }

        if (token?.StartsWith("#", StringComparison.Ordinal) == true)
        {
            Replace(ContextSuggestions, _workspace.SearchContextReferences(token, Numeric12));
        }
        else
        {
            Replace(ContextSuggestions, []);
        }
    }

    /// <summary>Updates prompt Suggestions.</summary>
    /// <param name="prompt">The prompt.</param>
    private void UpdatePromptSuggestions(string prompt)
    {
        string? token = LastPromptToken(prompt);
        if (token is null || token.Trim().Length == 0)
        {
            Replace(PromptSuggestions, []);
            SelectedPromptSuggestion = null;
            IsPromptSuggestionOpen = false;
            return;
        }

        string activeToken = token;
        IReadOnlyList<PromptSuggestionItem> suggestions;
        if (!activeToken.StartsWith("@", StringComparison.Ordinal))
        {
            suggestions = activeToken.StartsWith("#", StringComparison.Ordinal)
                ? ContextSuggestions.Select((x) => new PromptSuggestionItem
                {
                    Kind = x.ReferenceKind == "selection" ? "Selected code" : "Reference",
                    DisplayText = x.ReferenceKey,
                    Detail = x.ReferenceKind == "selection" ? $"{x.RelativePath} lines {x.StartLine}-{x.EndLine}" : x.RelativePath,
                    InsertText = $"{x.ReferenceKey} "
                }).ToList()
                : GetNonReferencePromptSuggestions(activeToken);
        }
        else
        {
            List<PromptSuggestionItem> fileSuggestions = (from x in _workspace.SearchFiles(activeToken, Numeric24)
                                                          select new PromptSuggestionItem
                                                          {
                                                              Kind = "File",
                                                              DisplayText = x.ReferenceKey,
                                                              Detail = x.RelativePath,
                                                              InsertText = $"{x.ReferenceKey} "
                                                          }).ToList();
            PromptSuggestionItem browseSuggestion = new PromptSuggestionItem
            {
                Kind = "Disk",
                DisplayText = "Browse files...",
                Detail = "Choose one or more files from the repository or elsewhere on disk",
                TargetTab = "browse-files"
            };
            suggestions = fileSuggestions.Count == 0
                ? [browseSuggestion]
                : [.. fileSuggestions, browseSuggestion];
        }

        Replace(PromptSuggestions, suggestions);
        SelectedPromptSuggestion = suggestions.FirstOrDefault();
        IsPromptSuggestionOpen = suggestions.Count > 0;
    }

    /// <summary>Builds slash Command Suggestions.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The build Slash Command Suggestions result.</returns>
    private IEnumerable<PromptSuggestionItem> BuildSlashCommandSuggestions(string token)
    {
        string query = token.TrimStart('/').Trim();
        return (from x in SlashCommandSuggestions()
                where string.IsNullOrWhiteSpace(query) || x.DisplayText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || x.Detail.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                select x).Take(Numeric12);
    }

    /// <summary>Gets prompt suggestions for non-reference tokens.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The prompt suggestions.</returns>
    private IReadOnlyList<PromptSuggestionItem> GetNonReferencePromptSuggestions(string token)
    {
        return token.StartsWith("/", StringComparison.Ordinal)
            ? BuildSlashCommandSuggestions(token).ToList()
            : [];
    }

    /// <summary>Performs the slash Command Suggestions operation.</summary>
    /// <returns>The slash Command Suggestions result.</returns>
    private IEnumerable<PromptSuggestionItem> SlashCommandSuggestions()
    {
        return
        [
            new() { Kind = ActionText, DisplayText = "/explain", Detail = "Explain selected code or active editor context", InsertText = "/explain " },
            new() { Kind = ActionText, DisplayText = "/fix", Detail = "Fix selected code with the smallest safe change", InsertText = "/fix " },
            new() { Kind = ActionText, DisplayText = "/review", Detail = "Review selected code for bugs and risks", InsertText = "/review " },
            new() { Kind = ActionText, DisplayText = "/optimize", Detail = "Optimize selected code without changing behavior", InsertText = "/optimize " },
            new() { Kind = ActionText, DisplayText = "/docs", Detail = "Generate or improve comments and documentation", InsertText = "/docs " },
            new() { Kind = ActionText, DisplayText = "/test", Detail = "Create focused tests for selected code", InsertText = "/test " },
            new() { Kind = "Debug", DisplayText = "/debug", Detail = "Debug current exception, break mode, stack, or selected code", InsertText = "/debug " },
            new() { Kind = "Plan", DisplayText = "/plan", Detail = "Create an agent-oriented implementation plan", InsertText = "/plan " },
            new() { Kind = "History", DisplayText = "/history", Detail = "Open saved VSCodex conversation history", InsertText = "/history " },
            new() { Kind = ToolsText, DisplayText = "/mcp", Detail = "Open VSCodex MCP server and tool selection", InsertText = "/mcp " },
            new() { Kind = "Options", DisplayText = "/settings", Detail = "Use Tools > Options > VSCodex for model, sandbox, and runtime settings", InsertText = "/settings " },
            new() { Kind = "Context", DisplayText = "/context", Detail = "Open selected-code and repository file context", InsertText = "/context " },
            new() { Kind = ToolsText, DisplayText = "/memory", Detail = "Open ReactiveMemory controls and saved context", InsertText = "/memory " },
            new() { Kind = ToolsText, DisplayText = "/agents", Detail = "Open multi-agent roles and orchestration controls", InsertText = "/agents " },
            new() { Kind = ToolsText, DisplayText = "/skills", Detail = "Open Codex skills controls", InsertText = "/skills " },
            new() { Kind = "Files", DisplayText = "/attachments", Detail = "Open prompt attachments", InsertText = "/attachments " }
        ];
    }

    /// <summary>Saves input Area Height.</summary>
    /// <param name="value">The value.</param>
    private void SaveInputAreaHeight(double value)
    {
        ExtensionSettings settings = _settingsStore.Current;
        if (Math.Abs(settings.DefaultInputAreaHeight - value) < Numeric0Point1)
        {
            return;
        }

        settings.DefaultInputAreaHeight = value;
        SaveSettingsForCurrentWorkspace(settings);
    }

    /// <summary>Performs the schedule Model Settings Save operation.</summary>
    /// <param name="refreshAnalytics">The refresh Analytics.</param>
    private void ScheduleModelSettingsSave(bool refreshAnalytics)
    {
        ExtensionSettings settings = CaptureModelSettingsSnapshot();
        WorkspaceIdentity? workspaceIdentity = CloneWorkspaceIdentity(_workspace.CurrentWorkspaceIdentity);
        int revision = Interlocked.Increment(ref _modelSettingsSaveRevision);
        CancellationTokenSource cancellation = new();
        CancellationTokenSource? previous;
        lock (_modelSettingsSaveGate)
        {
            previous = _modelSettingsSaveCancellation;
            _modelSettingsSaveCancellation = cancellation;
            _hasPendingModelSettingsSave = true;
        }

        previous?.Cancel();
        TaskObserver.FireAndForget(_joinableTaskFactory.RunAsync(async () =>
        {
            _ = Numeric2;
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(ModelSettingsSaveDebounceMilliseconds), cancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
                if (cancellation.IsCancellationRequested || revision != Volatile.Read(ref _modelSettingsSaveRevision))
                {
                    return;
                }

                SaveSettingsForWorkspace(workspaceIdentity, settings);
                if (refreshAnalytics)
                {
                    await _joinableTaskFactory.SwitchToMainThreadAsync(cancellation.Token);
                    if (!cancellation.IsCancellationRequested && revision == Volatile.Read(ref _modelSettingsSaveRevision))
                    {
                        UpdateAnalytics(Prompt);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex2)
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
                Status = $"Could not save VSCodex settings: {ex2.Message}";
            }
            finally
            {
                CompleteModelSettingsSave(cancellation);
            }
        }).Task);
    }

    /// <summary>Completes model Settings Save.</summary>
    /// <param name="cancellation">The cancellation.</param>
    private void CompleteModelSettingsSave(CancellationTokenSource cancellation)
    {
        lock (_modelSettingsSaveGate)
        {
            if (_modelSettingsSaveCancellation == cancellation)
            {
                _modelSettingsSaveCancellation = null;
                _hasPendingModelSettingsSave = false;
            }
        }

        cancellation.Dispose();
    }

    /// <summary>Performs the flush Pending Model Settings Save operation.</summary>
    private void FlushPendingModelSettingsSave()
    {
        CancellationTokenSource? pending;
        bool shouldSave;
        lock (_modelSettingsSaveGate)
        {
            pending = _modelSettingsSaveCancellation;
            shouldSave = _hasPendingModelSettingsSave;
            _modelSettingsSaveCancellation = null;
            _hasPendingModelSettingsSave = false;
        }

        pending?.Cancel();
        pending?.Dispose();
        if (!shouldSave)
        {
            return;
        }

        try
        {
            SaveModelSettings();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
        }
    }

    /// <summary>Saves model Settings.</summary>
    private void SaveModelSettings()
    {
        SaveSettingsForWorkspace(CloneWorkspaceIdentity(_workspace.CurrentWorkspaceIdentity), CaptureModelSettingsSnapshot());
    }

    /// <summary>Performs the capture Model Settings Snapshot operation.</summary>
    /// <returns>The capture Model Settings Snapshot result.</returns>
    private ExtensionSettings CaptureModelSettingsSnapshot()
    {
        ExtensionSettings settings = CloneSettings(_settingsStore.Current);
        settings.DefaultModel = (string.IsNullOrWhiteSpace(SelectedModel) ? settings.DefaultModel : SelectedModel);
        settings.DefaultFailoverModel = (string.IsNullOrWhiteSpace(FailoverModel) ? settings.DefaultFailoverModel : FailoverModel);
        settings.DefaultReasoningEffort = CodexModelCatalog.ResolveReasoningEffort(EffectiveMainModel(), SelectedReasoning);
        settings.DefaultVerbosity = (string.IsNullOrWhiteSpace(SelectedVerbosity) ? settings.DefaultVerbosity : SelectedVerbosity);
        settings.DefaultApprovalPolicy = ApprovalPolicy;
        settings.DefaultSandboxMode = SandboxMode;
        settings.DefaultOrchestrationModel = (string.IsNullOrWhiteSpace(OrchestrationModel) ? settings.DefaultModel : OrchestrationModel);
        settings.DefaultBudgetDrivenModelSelection = BudgetDrivenModelSelection;
        settings.DefaultBudgetModel = (string.IsNullOrWhiteSpace(BudgetModel) ? settings.DefaultBudgetModel : BudgetModel);
        EnsureModelOption(settings.CustomModels, settings.DefaultModel);
        EnsureModelOption(settings.CustomModels, settings.DefaultFailoverModel);
        EnsureModelOption(settings.CustomModels, settings.DefaultOrchestrationModel);
        EnsureModelOption(settings.CustomModels, settings.DefaultBudgetModel);
        return settings;
    }

    /// <summary>Saves settings For Current Workspace.</summary>
    /// <param name="settings">The settings.</param>
    private void SaveSettingsForCurrentWorkspace(ExtensionSettings settings)
    {
        SaveSettingsForWorkspace(CloneWorkspaceIdentity(_workspace.CurrentWorkspaceIdentity), settings);
    }

    /// <summary>Saves settings For Workspace.</summary>
    /// <param name="identity">The identity.</param>
    /// <param name="settings">The settings.</param>
    private void SaveSettingsForWorkspace(WorkspaceIdentity? identity, ExtensionSettings settings)
    {
        if (identity is not null && !string.IsNullOrWhiteSpace(identity.Id))
        {
            _settingsStore.SaveForWorkspace(identity, settings);
        }
        else
        {
            _settingsStore.Save(settings);
        }
    }

    /// <summary>Applies recommended Model.</summary>
    private void ApplyRecommendedModel()
    {
        string recommended = ModelEstimate.RecommendedModel;
        if (string.IsNullOrWhiteSpace(recommended))
        {
            return;
        }

        if (recommended.Equals(BudgetModel, StringComparison.OrdinalIgnoreCase))
        {
            BudgetDrivenModelSelection = true;
        }
        else
        {
            SelectedModel = recommended;
            BudgetDrivenModelSelection = false;
        }

        Status = $"Applied model recommendation: {recommended}";
    }

    /// <summary>Updates analytics.</summary>
    /// <param name="prompt">The prompt.</param>
    private void UpdateAnalytics(string prompt)
    {
        try
        {
            string? threadId = ThreadId;
            CodexRunMode mode = Mode;
            string failoverModel = FailoverModel;
            ApprovalPolicy approvalPolicy = ApprovalPolicy;
            SandboxMode sandboxMode = SandboxMode;
            CodexTransportKind transport = Transport;
            bool budgetDrivenModelSelection = BudgetDrivenModelSelection;
            string budgetModel = BudgetModel;
            CodexRunRequest request = new CodexRunRequest
            {
                Prompt = (prompt ?? string.Empty),
                ThreadId = threadId,
                WorkspaceRoot = _workspace.CurrentWorkspaceRoot,
                WorkspaceName = _workspace.CurrentWorkspaceName,
                WorkspaceSolutionPath = _workspace.CurrentSolutionPath,
                WorkspaceMemoryRoot = _workspace.CurrentWorkspaceMemoryRoot,
                WorkspaceIdentity = _workspace.CurrentWorkspaceIdentity,
                Options = new CodexRunOptions
                {
                    Mode = mode,
                    Model = EffectiveMainModel(),
                    FailoverModel = failoverModel,
                    ReasoningEffort = CodexModelCatalog.ResolveReasoningEffort(EffectiveMainModel(), SelectedReasoning),
                    Verbosity = SelectedVerbosity,
                    ApprovalPolicy = approvalPolicy,
                    SandboxMode = sandboxMode,
                    Transport = transport,
                    OrchestrationModel = EffectiveOrchestrationModel(),
                    BudgetDrivenModelSelection = budgetDrivenModelSelection,
                    BudgetModel = budgetModel
                },
                Attachments = Attachments.ToList(),
                Skills = Skills.Where((x) => x.IsEnabled).ToList(),
                Memories = _memoryStore.Search(prompt ?? string.Empty, Numeric10),
                McpServers = McpServers.Where((x) => x.IsEnabled).ToList(),
                WorkspaceFiles = _workspace.ResolveMentions(prompt ?? string.Empty, Numeric12000).Concat(_workspace.ResolveHashReferences(prompt ?? string.Empty, Numeric12000)).ToList(),
                AgentRoles = AgentRoles.Where((x) => x.IsEnabled).ToList()
            };
            ModelEstimate = _modelAnalytics.Estimate(request);
            this.RaisePropertyChanged();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
        }
    }
}
