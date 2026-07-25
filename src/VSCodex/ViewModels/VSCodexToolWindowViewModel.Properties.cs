// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI;

namespace VSCodex.ViewModels;

/// <summary>Exposes the bindable state and commands of the VSCodex tool-window view model.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Gets the messages.</summary>
    public ObservableCollection<ChatMessage> Messages { get; private set; }

    /// <summary>Gets the run Activity Roots.</summary>
    public ObservableCollection<RunActivityNode> RunActivityRoots { get; private set; }

    /// <summary>Gets the attachments.</summary>
    public ObservableCollection<CodexAttachment> Attachments { get; private set; }

    /// <summary>Gets the skills.</summary>
    public ObservableCollection<SkillDefinition> Skills { get; private set; }

    /// <summary>Gets the memories.</summary>
    public ObservableCollection<MemoryEntry> Memories { get; private set; }

    /// <summary>Gets the mcp Servers.</summary>
    public ObservableCollection<McpServerDefinition> McpServers { get; private set; }

    /// <summary>Gets the mcp Tool Suggestions.</summary>
    public ObservableCollection<McpToolDefinition> McpToolSuggestions { get; private set; }

    /// <summary>Gets the mcp Tool Input Fields.</summary>
    public ObservableCollection<McpToolInputField> McpToolInputFields { get; private set; }

    /// <summary>Gets the file Suggestions.</summary>
    public ObservableCollection<WorkspaceFileReference> FileSuggestions { get; private set; }

    /// <summary>Gets the context Suggestions.</summary>
    public ObservableCollection<WorkspaceFileReference> ContextSuggestions { get; private set; }

    /// <summary>Gets the prompt Suggestions.</summary>
    public ObservableCollection<PromptSuggestionItem> PromptSuggestions { get; private set; }

    /// <summary>Gets the history Items.</summary>
    public ObservableCollection<SessionHistoryItem> HistoryItems { get; private set; }

    /// <summary>Gets the visible History Items.</summary>
    public ObservableCollection<SessionHistoryItem> VisibleHistoryItems { get; private set; }

    /// <summary>Gets the orchestration Sections.</summary>
    public ObservableCollection<OrchestrationTaskSection> OrchestrationSections { get; private set; }

    /// <summary>Gets the agent Roles.</summary>
    public ObservableCollection<AgentRoleDefinition> AgentRoles { get; private set; }

    /// <summary>Gets the rate Limits.</summary>
    public ObservableCollection<RateLimitWindowStatus> RateLimits { get; private set; }

    /// <summary>Gets the prerequisites.</summary>
    public ObservableCollection<PrerequisiteStatus> Prerequisites { get; private set; }

    /// <summary>Gets the approval Requests.</summary>
    public ObservableCollection<ApprovalRequest> ApprovalRequests { get; private set; }

    /// <summary>Gets the model Options.</summary>
    public ObservableCollection<string> ModelOptions { get; private set; }

    /// <summary>Gets the reasoning Options.</summary>
    public ObservableCollection<string> ReasoningOptions { get; private set; }

    /// <summary>Gets the verbosity Options.</summary>
    public ObservableCollection<string> VerbosityOptions { get; private set; }

    /// <summary>Gets the mode Options.</summary>
    public ObservableCollection<CodexRunMode> ModeOptions { get; private set; }

    /// <summary>Gets the approval Options.</summary>
    public ObservableCollection<ApprovalPolicy> ApprovalOptions { get; private set; }

    /// <summary>Gets the sandbox Options.</summary>
    public ObservableCollection<SandboxMode> SandboxOptions { get; private set; }

    /// <summary>Gets the access Level Options.</summary>
    public ObservableCollection<CodexAccessLevel> AccessLevelOptions { get; private set; }

    /// <summary>Gets the transport Options.</summary>
    public ObservableCollection<CodexTransportKind> TransportOptions { get; private set; }

    /// <summary>Gets the agent Strategy Options.</summary>
    public ObservableCollection<AgentExecutionStrategy> AgentStrategyOptions { get; private set; }

    /// <summary>Gets the agent Model Selection Mode Options.</summary>
    public ObservableCollection<AgentModelSelectionMode> AgentModelSelectionModeOptions { get; private set; }

    /// <summary>Gets the run Command.</summary>
    public ReactiveCommand<Unit, Unit> RunCommand { get; private set; }

    /// <summary>Gets the cancel Command.</summary>
    public ReactiveCommand<Unit, Unit> CancelCommand { get; private set; }

    /// <summary>Gets the steer Command.</summary>
    public ReactiveCommand<Unit, Unit> SteerCommand { get; private set; }

    /// <summary>Gets the queue Command.</summary>
    public ReactiveCommand<Unit, Unit> QueueCommand { get; private set; }

    /// <summary>Gets the alternate Follow Up Command.</summary>
    public ReactiveCommand<Unit, Unit> AlternateFollowUpCommand { get; private set; }

    /// <summary>Gets the pause Command.</summary>
    public ReactiveCommand<Unit, Unit> PauseCommand { get; private set; }

    /// <summary>Gets the resume Command.</summary>
    public ReactiveCommand<Unit, Unit> ResumeCommand { get; private set; }

    /// <summary>Gets the approve Request Command.</summary>
    public ReactiveCommand<ApprovalRequest, Unit> ApproveRequestCommand { get; private set; }

    /// <summary>Gets the decline Request Command.</summary>
    public ReactiveCommand<ApprovalRequest, Unit> DeclineRequestCommand { get; private set; }

    /// <summary>Gets the new Thread Command.</summary>
    public ReactiveCommand<Unit, Unit> NewThreadCommand { get; private set; }

    /// <summary>Gets the show History Command.</summary>
    public ReactiveCommand<Unit, Unit> ShowHistoryCommand { get; private set; }

    /// <summary>Gets the refresh History Command.</summary>
    public ReactiveCommand<Unit, Unit> RefreshHistoryCommand { get; private set; }

    /// <summary>Gets the load History Command.</summary>
    public ReactiveCommand<SessionHistoryItem, Unit> LoadHistoryCommand { get; private set; }

    /// <summary>Gets the delete History Command.</summary>
    public ReactiveCommand<SessionHistoryItem, Unit> DeleteHistoryCommand { get; private set; }

    /// <summary>Gets the begin Rename History Command.</summary>
    public ReactiveCommand<SessionHistoryItem, Unit> BeginRenameHistoryCommand { get; private set; }

    /// <summary>Gets the save Rename History Command.</summary>
    public ReactiveCommand<SessionHistoryItem, Unit> SaveRenameHistoryCommand { get; private set; }

    /// <summary>Gets the cancel Rename History Command.</summary>
    public ReactiveCommand<SessionHistoryItem, Unit> CancelRenameHistoryCommand { get; private set; }

    /// <summary>Gets the check Prerequisites Command.</summary>
    public ReactiveCommand<Unit, Unit> CheckPrerequisitesCommand { get; private set; }

    /// <summary>Gets the copy Prerequisite Command.</summary>
    public ReactiveCommand<PrerequisiteStatus, Unit> CopyPrerequisiteCommand { get; private set; }

    /// <summary>Gets the update Prerequisite Command.</summary>
    public ReactiveCommand<PrerequisiteStatus, Unit> UpdatePrerequisiteCommand { get; private set; }

    /// <summary>Gets the refresh Command.</summary>
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; private set; }

    /// <summary>Gets the refresh Analytics Command.</summary>
    public ReactiveCommand<Unit, Unit> RefreshAnalyticsCommand { get; private set; }

    /// <summary>Gets the apply Recommended Model Command.</summary>
    public ReactiveCommand<Unit, Unit> ApplyRecommendedModelCommand { get; private set; }

    /// <summary>Gets the add User Memory Command.</summary>
    public ReactiveCommand<Unit, Unit> AddUserMemoryCommand { get; private set; }

    /// <summary>Gets the add Workspace Memory Command.</summary>
    public ReactiveCommand<Unit, Unit> AddWorkspaceMemoryCommand { get; private set; }

    /// <summary>Gets the scan Project Memory Command.</summary>
    public ReactiveCommand<Unit, Unit> ScanProjectMemoryCommand { get; private set; }

    /// <summary>Gets the add Image Attachment Command.</summary>
    public ReactiveCommand<Unit, Unit> AddImageAttachmentCommand { get; private set; }

    /// <summary>Gets the toggle Voice Input Command.</summary>
    public ReactiveCommand<Unit, Unit> ToggleVoiceInputCommand { get; private set; }

    /// <summary>Gets the clear Attachments Command.</summary>
    public ReactiveCommand<Unit, Unit> ClearAttachmentsCommand { get; private set; }

    /// <summary>Gets the select Mcp Server Command.</summary>
    public ReactiveCommand<McpServerDefinition, Unit> SelectMcpServerCommand { get; private set; }

    /// <summary>Gets the select Mcp Tool Command.</summary>
    public ReactiveCommand<McpToolDefinition, Unit> SelectMcpToolCommand { get; private set; }

    /// <summary>Gets the insert Mcp Tool Command.</summary>
    public ReactiveCommand<Unit, Unit> InsertMcpToolCommand { get; private set; }

    /// <summary>Gets the add Mcp Stdio Server Command.</summary>
    public ReactiveCommand<Unit, Unit> AddMcpStdioServerCommand { get; private set; }

    /// <summary>Gets the add Mcp Url Server Command.</summary>
    public ReactiveCommand<Unit, Unit> AddMcpUrlServerCommand { get; private set; }

    /// <summary>Gets the remove Mcp Server Command.</summary>
    public ReactiveCommand<McpServerDefinition, Unit> RemoveMcpServerCommand { get; private set; }

    /// <summary>Gets the save Mcp Servers Command.</summary>
    public ReactiveCommand<Unit, Unit> SaveMcpServersCommand { get; private set; }

    /// <summary>Gets the create Skill Command.</summary>
    public ReactiveCommand<Unit, Unit> CreateSkillCommand { get; private set; }

    /// <summary>Gets the save Skills Command.</summary>
    public ReactiveCommand<Unit, Unit> SaveSkillsCommand { get; private set; }

    /// <summary>Gets the add Skill Root Command.</summary>
    public ReactiveCommand<Unit, Unit> AddSkillRootCommand { get; private set; }

    /// <summary>Gets the open Skills Folder Command.</summary>
    public ReactiveCommand<Unit, Unit> OpenSkillsFolderCommand { get; private set; }

    /// <summary>Gets the open Codex Config Command.</summary>
    public ReactiveCommand<Unit, Unit> OpenCodexConfigCommand { get; private set; }

    /// <summary>Gets the debug Selection Command.</summary>
    public ReactiveCommand<Unit, Unit> DebugSelectionCommand { get; private set; }

    /// <summary>Gets the create Test For Selection Command.</summary>
    public ReactiveCommand<Unit, Unit> CreateTestForSelectionCommand { get; private set; }

    /// <summary>Gets the create Plan Command.</summary>
    public ReactiveCommand<Unit, Unit> CreatePlanCommand { get; private set; }

    /// <summary>Gets the explain Selection Command.</summary>
    public ReactiveCommand<Unit, Unit> ExplainSelectionCommand { get; private set; }

    /// <summary>Gets the fix Selection Command.</summary>
    public ReactiveCommand<Unit, Unit> FixSelectionCommand { get; private set; }

    /// <summary>Gets the review Selection Command.</summary>
    public ReactiveCommand<Unit, Unit> ReviewSelectionCommand { get; private set; }

    /// <summary>Gets the optimize Selection Command.</summary>
    public ReactiveCommand<Unit, Unit> OptimizeSelectionCommand { get; private set; }

    /// <summary>Gets the generate Docs Command.</summary>
    public ReactiveCommand<Unit, Unit> GenerateDocsCommand { get; private set; }

    /// <summary>Gets the copy Message Command.</summary>
    public ReactiveCommand<ChatMessage, Unit> CopyMessageCommand { get; private set; }

    /// <summary>Gets the copy Activity Detail Command.</summary>
    public ReactiveCommand<RunActivityNode, Unit> CopyActivityDetailCommand { get; private set; }

    /// <summary>Gets the use Message As Prompt Command.</summary>
    public ReactiveCommand<ChatMessage, Unit> UseMessageAsPromptCommand { get; private set; }

    /// <summary>Gets the open Activity File Command.</summary>
    public ReactiveCommand<RunActivityNode, Unit> OpenActivityFileCommand { get; private set; }

    /// <summary>Gets or sets the prompt.</summary>
    public string Prompt
    {
        get => _prompt;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _prompt, value);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets or sets the status.</summary>
    public string Status
    {
        get => _status;
        set => _ = this.RaiseAndSetIfChanged(ref _status, value);
    }

    /// <summary>Gets or sets the is Running.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _isRunning, value);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets the queued Prompt Count.</summary>
    public int QueuedPromptCount
    {
        get => _queuedPromptCount;
        private set
        {
            _ = this.RaiseAndSetIfChanged(ref _queuedPromptCount, Math.Max(0, value));
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets the has Prompt Text.</summary>
    public bool HasPromptText => !string.IsNullOrWhiteSpace(Prompt);

    /// <summary>Gets the has Queued Prompts.</summary>
    public bool HasQueuedPrompts => QueuedPromptCount > 0;

    /// <summary>Gets the is Paused.</summary>
    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            _ = this.RaiseAndSetIfChanged(ref _isPaused, value);
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets the is Pause Control Visible.</summary>
    public bool IsPauseControlVisible
    {
        get => IsRunning ? !IsPaused : false;
    }

    /// <summary>Gets the is Resume Control Visible.</summary>
    public bool IsResumeControlVisible => IsPaused;

    /// <summary>Gets the can Steer.</summary>
    public bool CanSteer
    {
        get => IsRunning ? !string.IsNullOrWhiteSpace(ThreadId) : false;
    }

    /// <summary>Gets the is Run Control In Stop Mode.</summary>
    public bool IsRunControlInStopMode
    {
        get => IsRunning ? !HasPromptText : false;
    }

    /// <summary>Gets the is Persistent Stop Control Visible.</summary>
    public bool IsPersistentStopControlVisible
    {
        get => IsRunning ? HasPromptText : false;
    }

    /// <summary>Gets the queue Status Display.</summary>
    public string QueueStatusDisplay
    {
        get
        {
            if (QueuedPromptCount > 0)
            {
                return QueuedPromptCount != 1 ? $"{QueuedPromptCount} queued" : "1 queued";
            }

            return string.Empty;
        }
    }

    /// <summary>Gets the can Edit Settings.</summary>
    public bool CanEditSettings => !IsRunning;

    /// <summary>Gets or sets the is Tool Panel Open.</summary>
    public bool IsToolPanelOpen
    {
        get => _isToolPanelOpen;
        set => _ = this.RaiseAndSetIfChanged(ref _isToolPanelOpen, value);
    }

    /// <summary>Gets or sets the use Multi Agent Orchestration.</summary>
    public bool UseMultiAgentOrchestration
    {
        get => _useMultiAgentOrchestration;
        set
        {
            if (!CanChangeSetting(_useMultiAgentOrchestration, value))
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref _useMultiAgentOrchestration, value);
        }
    }

    /// <summary>Gets or sets the budget Driven Model Selection.</summary>
    public bool BudgetDrivenModelSelection
    {
        get => _budgetDrivenModelSelection;
        set
        {
            bool previous = _budgetDrivenModelSelection;
            SetModelSetting(ref _budgetDrivenModelSelection, value, nameof(BudgetDrivenModelSelection), refreshAnalytics: true);
            RefreshReasoningOptionsIfChanged(previous, _budgetDrivenModelSelection);
        }
    }

    /// <summary>Gets or sets the max Agent Concurrency.</summary>
    public int MaxAgentConcurrency
    {
        get => _maxAgentConcurrency;
        set
        {
            int clamped = Math.Max(1, value);
            if (!CanChangeSetting(_maxAgentConcurrency, clamped))
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref _maxAgentConcurrency, clamped);
        }
    }

    /// <summary>Gets or sets the selected Tool Tab Index.</summary>
    public int SelectedToolTabIndex
    {
        get => _selectedToolTabIndex;
        set => _ = this.RaiseAndSetIfChanged(ref _selectedToolTabIndex, Math.Max(0, value));
    }

    /// <summary>Gets or sets the history Search Text.</summary>
    public string HistorySearchText
    {
        get => _historySearchText;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _historySearchText, value ?? string.Empty);
            ApplyHistoryFilter();
        }
    }

    /// <summary>Gets the has Visible History.</summary>
    public bool HasVisibleHistory => VisibleHistoryItems.Count > 0;

    /// <summary>Gets or sets the input Area Height.</summary>
    public double InputAreaHeight
    {
        get => _inputAreaHeight;
        set => _ = SetInputAreaHeight(value);
    }

    /// <summary>Gets or sets the agent Strategy.</summary>
    public AgentExecutionStrategy AgentStrategy
    {
        get => _agentStrategy;
        set
        {
            if (!CanChangeSetting(_agentStrategy, value))
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref _agentStrategy, value);
        }
    }

    /// <summary>Gets or sets the mode.</summary>
    public CodexRunMode Mode
    {
        get => _mode;
        set
        {
            if (!CanChangeSetting(_mode, value))
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref _mode, value);
        }
    }

    /// <summary>Gets or sets the selected Model.</summary>
    public string SelectedModel
    {
        get => _selectedModel;
        set
        {
            string previous = _selectedModel;
            SetModelSetting(ref _selectedModel, value, nameof(SelectedModel), refreshAnalytics: true);
            RefreshReasoningOptionsIfChanged(previous, _selectedModel);
        }
    }

    /// <summary>Gets or sets the failover Model.</summary>
    public string FailoverModel
    {
        get => _failoverModel;
        set
        {
            SetModelSetting(ref _failoverModel, value, nameof(FailoverModel), refreshAnalytics: true);
        }
    }

    /// <summary>Gets or sets the selected Reasoning.</summary>
    public string SelectedReasoning
    {
        get => _selectedReasoning;
        set
        {
            string effort = CodexModelCatalog.ResolveReasoningEffort(EffectiveMainModel(), value);
            SetModelSetting(ref _selectedReasoning, effort, nameof(SelectedReasoning), refreshAnalytics: false);
        }
    }

    /// <summary>Gets or sets the selected Verbosity.</summary>
    public string SelectedVerbosity
    {
        get => _selectedVerbosity;
        set
        {
            SetModelSetting(ref _selectedVerbosity, value, nameof(SelectedVerbosity), refreshAnalytics: false);
        }
    }

    /// <summary>Gets or sets the orchestration Model.</summary>
    public string OrchestrationModel
    {
        get => _orchestrationModel;
        set
        {
            SetModelSetting(ref _orchestrationModel, value, nameof(OrchestrationModel), refreshAnalytics: false);
        }
    }

    /// <summary>Gets or sets the budget Model.</summary>
    public string BudgetModel
    {
        get => _budgetModel;
        set
        {
            string previous = _budgetModel;
            SetModelSetting(ref _budgetModel, value, nameof(BudgetModel), refreshAnalytics: true);
            RefreshReasoningOptionsIfChanged(previous, _budgetModel);
        }
    }

    /// <summary>Gets or sets the model Estimate.</summary>
    public ModelUsageEstimate ModelEstimate
    {
        get => _modelEstimate;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _modelEstimate, value ?? new ModelUsageEstimate());
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets the analytics Summary.</summary>
    public string AnalyticsSummary => ModelEstimate.Summary;

    /// <summary>Gets the analytics Recommendation.</summary>
    public string AnalyticsRecommendation => ModelEstimate.RecommendationReason;

    /// <summary>Gets the context Window Summary.</summary>
    public string ContextWindowSummary
    {
        get => ModelEstimate.ContextWindowTokens > 0
            ? $"{FormatTokenCount(ModelEstimate.EstimatedInputTokens)} / {FormatTokenCount(ModelEstimate.ContextWindowTokens)} context tokens"
            : "Context size unavailable";
    }

    /// <summary>Gets the context Remaining Summary.</summary>
    public string ContextRemainingSummary
    {
        get => ModelEstimate.ContextWindowTokens > 0 ? $"{ModelEstimate.ContextRemainingPercent}% remaining ({FormatTokenCount(ModelEstimate.ContextRemainingTokens)})" : string.Empty;
    }

    /// <summary>Gets or sets the mcp Input Prompt.</summary>
    public string McpInputPrompt
    {
        get => _mcpInputPrompt;
        set => _ = this.RaiseAndSetIfChanged(ref _mcpInputPrompt, value);
    }

    /// <summary>Gets or sets the new Skill Name.</summary>
    public string NewSkillName
    {
        get => _newSkillName;
        set => _ = this.RaiseAndSetIfChanged(ref _newSkillName, value ?? string.Empty);
    }

    /// <summary>Gets or sets the new Skill Description.</summary>
    public string NewSkillDescription
    {
        get => _newSkillDescription;
        set => _ = this.RaiseAndSetIfChanged(ref _newSkillDescription, value ?? string.Empty);
    }

    /// <summary>Gets or sets the skill Root Path Input.</summary>
    public string SkillRootPathInput
    {
        get => _skillRootPathInput;
        set => _ = this.RaiseAndSetIfChanged(ref _skillRootPathInput, value ?? string.Empty);
    }

    /// <summary>Gets the user Skills Root.</summary>
    public string UserSkillsRoot => LocalPaths.UserSkillsRoot;

    /// <summary>Gets the codex Config Path.</summary>
    public string CodexConfigPath => LocalPaths.UserCodexConfig;

    /// <summary>Gets or sets the rate Limit Updated At.</summary>
    public string RateLimitUpdatedAt
    {
        get => _rateLimitUpdatedAt;
        set => _ = this.RaiseAndSetIfChanged(ref _rateLimitUpdatedAt, value);
    }

    /// <summary>Gets or sets the codex Setup Summary.</summary>
    public string CodexSetupSummary
    {
        get => _codexSetupSummary;
        set => _ = this.RaiseAndSetIfChanged(ref _codexSetupSummary, value);
    }

    /// <summary>Gets or sets the codex Setup Instructions.</summary>
    public string CodexSetupInstructions
    {
        get => _codexSetupInstructions;
        set => _ = this.RaiseAndSetIfChanged(ref _codexSetupInstructions, value);
    }

    /// <summary>Gets the current Workspace Display.</summary>
    public string CurrentWorkspaceDisplay
    {
        get => FormatWorkspaceDisplay(_workspace.CurrentWorkspaceIdentity, _workspace.CurrentWorkspaceName);
    }

    /// <summary>Gets or sets the voice Input Status.</summary>
    public string VoiceInputStatus
    {
        get => _voiceInputStatus;
        set => _ = this.RaiseAndSetIfChanged(ref _voiceInputStatus, value);
    }

    /// <summary>Gets the is Voice Input Available.</summary>
    public bool IsVoiceInputAvailable => _voiceInput.IsAvailable;

    /// <summary>Gets the voice Transcript Revision.</summary>
    public int VoiceTranscriptRevision
    {
        get => _voiceTranscriptRevision;
        private set => _ = this.RaiseAndSetIfChanged(ref _voiceTranscriptRevision, value);
    }

    /// <summary>Gets the is Listening To Voice.</summary>
    public bool IsListeningToVoice => _voiceInput.IsListening;

    /// <summary>Gets or sets the selected Prompt Suggestion.</summary>
    public PromptSuggestionItem? SelectedPromptSuggestion
    {
        get => _selectedPromptSuggestion;
        set => _ = this.RaiseAndSetIfChanged(ref _selectedPromptSuggestion, value);
    }

    /// <summary>Gets or sets the is Prompt Suggestion Open.</summary>
    public bool IsPromptSuggestionOpen
    {
        get => _isPromptSuggestionOpen;
        set => _ = this.RaiseAndSetIfChanged(ref _isPromptSuggestionOpen, value);
    }

    /// <summary>Gets or sets the approval Policy.</summary>
    public ApprovalPolicy ApprovalPolicy
    {
        get => _approvalPolicy;
        set
        {
            SetModelSetting(ref _approvalPolicy, value, nameof(ApprovalPolicy), refreshAnalytics: false);
        }
    }

    /// <summary>Gets or sets the sandbox Mode.</summary>
    public SandboxMode SandboxMode
    {
        get => _sandboxMode;
        set
        {
            if (EqualityComparer<SandboxMode>.Default.Equals(_sandboxMode, value) || !CanChangeSetting(_sandboxMode, value))
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref _sandboxMode, value);
            CodexAccessLevel accessLevel = AccessLevelFromSandbox(value);
            if (!EqualityComparer<CodexAccessLevel>.Default.Equals(_accessLevel, accessLevel))
            {
                _ = this.RaiseAndSetIfChanged(ref _accessLevel, accessLevel);
            }

            ScheduleModelSettingsSave(refreshAnalytics: false);
        }
    }

    /// <summary>Gets or sets the access Level.</summary>
    public CodexAccessLevel AccessLevel
    {
        get => _accessLevel;
        set
        {
            if (!CanChangeSetting(_accessLevel, value))
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref _accessLevel, value);
            SandboxMode sandbox = SandboxFromAccessLevel(value);
            if (!EqualityComparer<SandboxMode>.Default.Equals(_sandboxMode, sandbox))
            {
                _ = this.RaiseAndSetIfChanged(ref _sandboxMode, sandbox);
            }

            ScheduleModelSettingsSave(refreshAnalytics: false);
        }
    }

    /// <summary>Gets or sets the transport.</summary>
    public CodexTransportKind Transport
    {
        get => _transport;
        set
        {
            if (!CanChangeSetting(_transport, value))
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref _transport, value);
        }
    }

    /// <summary>Gets or sets the selected Mcp Server.</summary>
    public McpServerDefinition? SelectedMcpServer
    {
        get => _selectedMcpServer;
        set => _ = this.RaiseAndSetIfChanged(ref _selectedMcpServer, value);
    }

    /// <summary>Gets or sets the selected Mcp Tool.</summary>
    public McpToolDefinition? SelectedMcpTool
    {
        get => _selectedMcpTool;
        set => _ = this.RaiseAndSetIfChanged(ref _selectedMcpTool, value);
    }

    /// <summary>Gets or sets the selected History Item.</summary>
    public SessionHistoryItem? SelectedHistoryItem
    {
        get => _selectedHistoryItem;
        set => _ = this.RaiseAndSetIfChanged(ref _selectedHistoryItem, value);
    }

    /// <summary>Gets or sets the thread Id.</summary>
    public string? ThreadId
    {
        get => _threadId;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _threadId, value);
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Refreshes reasoning options when a Boolean setting changes.</summary>
    /// <param name="previous">The previous value.</param>
    /// <param name="current">The current value.</param>
    private void RefreshReasoningOptionsIfChanged(bool previous, bool current)
    {
        if (previous == current)
        {
            return;
        }

        RefreshReasoningOptions();
    }

    /// <summary>Refreshes reasoning options when a model selection changes.</summary>
    /// <param name="previous">The previous model.</param>
    /// <param name="current">The current model.</param>
    private void RefreshReasoningOptionsIfChanged(string previous, string current)
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(previous, current))
        {
            return;
        }

        RefreshReasoningOptions();
    }

    /// <summary>Formats the current workspace display value.</summary>
    /// <param name="identity">The workspace identity.</param>
    /// <param name="workspaceName">The current workspace name.</param>
    /// <returns>The formatted workspace display.</returns>
    private string FormatWorkspaceDisplay(WorkspaceIdentity? identity, string workspaceName)
    {
        if (identity is null || string.IsNullOrWhiteSpace(identity.RootPath))
        {
            return "Project: open a Visual Studio solution or repository folder";
        }

        string name = string.IsNullOrWhiteSpace(identity.Name) ? workspaceName : identity.Name;
        string solution = string.IsNullOrWhiteSpace(identity.SolutionRelativePath)
            ? string.Empty
            : $" ({identity.SolutionRelativePath})";
        return $"Project: {name}{solution}";
    }
}
