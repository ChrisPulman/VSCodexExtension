// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI;

namespace VSCodex.ViewModels;

/// <summary>Manages workspace-scoped history, attachments, and agent choices.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Performs the fork Session For Current Workspace operation.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The fork Session For Current Workspace result.</returns>
    private CodexSessionDocument ForkSessionForCurrentWorkspace(CodexSessionDocument source)
    {
        CodexSessionDocument fork = _sessionStore.Create();
        fork.Title = source.Title;
        fork.Messages.AddRange(source.Messages.Where((message) => message?.IsTransient == false).Select(CloneMessage));
        ApplyWorkspaceToSession(fork, _workspace.CurrentWorkspaceIdentity);
        if (fork.Messages.Count > 0)
        {
            fork.Updated = fork.Messages.Max((message) => message.Timestamp);
        }

        return fork;
    }

    /// <summary>Refreshes history.</summary>
    private void RefreshHistory()
    {
        WorkspaceIdentity identity = _workspace.CurrentWorkspaceIdentity;
        List<SessionHistoryItem> items = (from session in _sessionStore.LoadRecent(Numeric100)
                                          where session.Messages.Count > 0 || !string.IsNullOrWhiteSpace(session.ThreadId)
                                          where SessionBelongsToCurrentWorkspace(session, identity)
                                          select session).Select(BuildHistoryItem).ToList();
        Replace(HistoryItems, items);
        ApplyHistoryFilter(items);
    }

    /// <summary>Applies history Filter.</summary>
    private void ApplyHistoryFilter()
    {
        ApplyHistoryFilter(HistoryItems.ToList());
    }

    /// <summary>Applies history Filter.</summary>
    /// <param name="source">The source.</param>
    private void ApplyHistoryFilter(IEnumerable<SessionHistoryItem> source)
    {
        string query = (HistorySearchText ?? string.Empty).Trim();
        IEnumerable<SessionHistoryItem> items = source ?? Enumerable.Empty<SessionHistoryItem>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where((item) => Contains(item.Title, query) || Contains(item.Preview, query) || Contains(item.ThreadId, query));
        }

        Replace(VisibleHistoryItems, items.ToList());
        this.RaisePropertyChanged();
    }

    /// <summary>Loads history Item.</summary>
    /// <param name="item">The item.</param>
    private void LoadHistoryItem(SessionHistoryItem item)
    {
        if (item is null)
        {
            return;
        }

        CodexSessionDocument? loaded = _sessionStore.Load(item.Id);
        if (loaded is null)
        {
            Status = "VSCodex history item could not be loaded";
            RefreshHistory();
            return;
        }

        SaveCurrentSessionIfNeeded();
        bool loadedFromAnotherWorkspace = IsKnownDifferentWorkspace(loaded, _workspace.CurrentWorkspaceIdentity);
        _session = (loadedFromAnotherWorkspace ? ForkSessionForCurrentWorkspace(loaded) : loaded);
        ThreadId = (loadedFromAnotherWorkspace ? null : loaded.ThreadId);
        Prompt = string.Empty;
        Messages.Clear();
        foreach (ChatMessage message in _session.Messages ?? new List<ChatMessage>())
        {
            Messages.Add(message);
        }

        RebuildActivityTreeFromMessages();
        Attachments.Clear();
        SelectedHistoryItem = item;
        IsToolPanelOpen = false;
        Status = (loadedFromAnotherWorkspace ? "Loaded history for the current workspace without reusing the previous Codex thread" : ($"Loaded history: {item.Title}"));
        UpdateAnalytics(Prompt);
    }

    /// <summary>Deletes history Item.</summary>
    /// <param name="item">The item.</param>
    private void DeleteHistoryItem(SessionHistoryItem item)
    {
        if (item is null)
        {
            return;
        }

        bool num = string.Equals(item.Id, _session.Id, StringComparison.OrdinalIgnoreCase);
        _sessionStore.Delete(item.Id);
        if (num)
        {
            _session = _sessionStore.Create();
            Prompt = string.Empty;
            ThreadId = null;
            Messages.Clear();
            RunActivityRoots.Clear();
            _activeRunActivity = null;
            _activeProgressNode = null;
            _pendingUserActivityPromptToSuppress = string.Empty;
            Attachments.Clear();
            UpdateAnalytics(Prompt);
        }

        RefreshHistory();
        Status = "Deleted history item";
    }

    /// <summary>Performs the begin Rename History Item operation.</summary>
    /// <param name="item">The item.</param>
    private void BeginRenameHistoryItem(SessionHistoryItem item)
    {
        if (item is null)
        {
            return;
        }

        item.DraftTitle = item.Title;
        item.IsRenaming = true;
    }

    /// <summary>Saves rename History Item.</summary>
    /// <param name="item">The item.</param>
    private void SaveRenameHistoryItem(SessionHistoryItem item)
    {
        if (item is null)
        {
            return;
        }

        string title = CompactLine(item.DraftTitle, Numeric120);
        if (string.IsNullOrWhiteSpace(title))
        {
            CancelRenameHistoryItem(item);
            return;
        }

        CodexSessionDocument? loaded = _sessionStore.Load(item.Id);
        if (loaded is null)
        {
            Status = "VSCodex history item could not be renamed";
            RefreshHistory();
            return;
        }

        loaded.Title = title;
        if (string.Equals(_session.Id, loaded.Id, StringComparison.OrdinalIgnoreCase))
        {
            _session.Title = title;
        }

        _sessionStore.Save(loaded);
        item.IsRenaming = false;
        RefreshHistory();
        Status = "Renamed history item";
    }

    /// <summary>Saves current Session If Needed.</summary>
    private void SaveCurrentSessionIfNeeded()
    {
        if (_session.Messages.Count == 0 && string.IsNullOrWhiteSpace(_session.ThreadId))
        {
            return;
        }

        ApplyCurrentWorkspaceToSession();
        _session.ThreadId = ThreadId;
        if (string.IsNullOrWhiteSpace(_session.Title))
        {
            _session.Title = DeriveSessionTitle(_session);
        }

        _sessionStore.Save(_session);
        RefreshHistory();
    }

    /// <summary>Performs the expand Assistant Slash Command operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The expand Assistant Slash Command result.</returns>
    private string ExpandAssistantSlashCommand(string value)
    {
        string prompt = value ?? string.Empty;
        if (prompt.StartsWith("/debug", StringComparison.OrdinalIgnoreCase))
        {
            return _assistantContext.BuildDebugPrompt();
        }

        if (prompt.StartsWith("/test", StringComparison.OrdinalIgnoreCase))
        {
            return _assistantContext.BuildTestPrompt();
        }

        if (prompt.StartsWith("/plan", StringComparison.OrdinalIgnoreCase))
        {
            string goal = prompt.Substring(Math.Min(Numeric5, prompt.Length)).Trim();
            RefreshAgentPlanPreview(goal);
            return _assistantContext.BuildPlanPrompt(goal, BuildAgentSummary());
        }

        if (prompt.StartsWith("/explain", StringComparison.OrdinalIgnoreCase))
        {
            return _assistantContext.BuildExplainPrompt();
        }

        if (prompt.StartsWith("/fix", StringComparison.OrdinalIgnoreCase))
        {
            return _assistantContext.BuildFixPrompt();
        }

        if (prompt.StartsWith("/review", StringComparison.OrdinalIgnoreCase))
        {
            return _assistantContext.BuildReviewPrompt();
        }

        if (prompt.StartsWith("/optimize", StringComparison.OrdinalIgnoreCase))
        {
            return _assistantContext.BuildOptimizePrompt();
        }

        return prompt.StartsWith("/docs", StringComparison.OrdinalIgnoreCase) ? _assistantContext.BuildDocumentationPrompt() : prompt;
    }

    /// <summary>Applies model Selection.</summary>
    /// <param name="agents">The agents.</param>
    /// <returns>The apply Model Selection result.</returns>
    private IEnumerable<AgentRoleDefinition> ApplyModelSelection(IEnumerable<AgentRoleDefinition> agents)
    {
        foreach (AgentRoleDefinition item in DistinctAgentRoles(agents))
        {
            AgentRoleDefinition agent = CloneAgentRole(item);
            if (BudgetDrivenModelSelection || agent.ModelSelectionMode == AgentModelSelectionMode.BudgetDriven)
            {
                agent.Model = BudgetModel;
            }

            yield return agent;
        }
    }

    /// <summary>Performs the effective Main Model operation.</summary>
    /// <returns>The effective Main Model result.</returns>
    private string EffectiveMainModel()
    {
        return !BudgetDrivenModelSelection || string.IsNullOrWhiteSpace(BudgetModel) ? SelectedModel : BudgetModel;
    }

    /// <summary>Performs the effective Orchestration Model operation.</summary>
    /// <returns>The effective Orchestration Model result.</returns>
    private string EffectiveOrchestrationModel()
    {
        return !BudgetDrivenModelSelection || string.IsNullOrWhiteSpace(BudgetModel) ? OrchestrationModel : BudgetModel;
    }

    /// <summary>Builds agent Summary.</summary>
    /// <returns>The build Agent Summary result.</returns>
    private string BuildAgentSummary()
    {
        StringBuilder sb = new();
        foreach (AgentRoleDefinition agent in DistinctAgentRoles(AgentRoles.Where((x) => x.IsEnabled)))
        {
            _ = sb.AppendLine($"- {agent.Name} ({agent.Role}) model={agent.Model}; mode={agent.ModelSelectionMode}: {agent.Instructions}");
        }

        return sb.ToString();
    }
}
