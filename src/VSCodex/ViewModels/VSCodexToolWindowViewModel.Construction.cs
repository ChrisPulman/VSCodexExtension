// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using ReactiveUI;

namespace VSCodex.ViewModels;

/// <summary>Constructs and initializes the VSCodex tool-window view model.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Initializes a new instance of the <see cref="VSCodexToolWindowViewModel"/> class.</summary>
    /// <param name="dependencyValues">The ordered package-composition dependencies.</param>
    public VSCodexToolWindowViewModel(params object[] dependencyValues)
    {
        ViewModelDependencies dependencies = new(dependencyValues);
        _settingsStore = dependencies.SettingsStore;
        _memoryStore = dependencies.MemoryStore;
        _skillIndex = dependencies.SkillIndex;
        _mcpConfig = dependencies.McpConfig;
        _mcpTools = dependencies.McpTools;
        _reactiveMemory = dependencies.ReactiveMemory;
        _workspace = dependencies.Workspace;
        _sessionStore = dependencies.SessionStore;
        _codex = dependencies.Codex;
        _taskOrchestrator = dependencies.TaskOrchestrator;
        _assistantContext = dependencies.AssistantContext;
        _modelAnalytics = dependencies.ModelAnalytics;
        _environment = dependencies.Environment;
        _voiceInput = dependencies.VoiceInput;
        _timeProvider = dependencies.TimeProvider;
        _joinableTaskFactory = dependencies.JoinableTaskFactory;
        _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _uiScheduler = new DispatcherScheduler(_uiDispatcher);
        _session = _sessionStore.Create();
        ExtensionSettings settings = _settingsStore.Current;
        InitializeSettingState(settings);
        InitializeCollections(settings);
        InitializeRunCommands();
        InitializeHistoryAndSetupCommands();
        InitializeMemoryMcpAndSkillCommands();
        InitializeAssistantCommands();
        _subscriptions = CreateSubscriptions();
        RefreshWorkspaceIdentityForStartup();
        UpdateAnalytics(Prompt);
        UpdateReferenceSuggestions(Prompt);
        UpdatePromptSuggestions(Prompt);
        Refresh();
        RefreshAgentPlanPreview(Prompt);
        ScheduleStartupChecksInBackground();
    }

    /// <summary>Initializes settings-backed fields.</summary>
    /// <param name="settings">The current extension settings.</param>
    private void InitializeSettingState(ExtensionSettings settings)
    {
        _selectedModel = settings.DefaultModel;
        _failoverModel = string.IsNullOrWhiteSpace(settings.DefaultFailoverModel)
            ? CodexModelCatalog.DefaultFailoverModel
            : settings.DefaultFailoverModel;
        _selectedVerbosity = settings.DefaultVerbosity;
        _approvalPolicy = settings.DefaultApprovalPolicy;
        _sandboxMode = settings.DefaultSandboxMode;
        _accessLevel = AccessLevelFromSandbox(settings.DefaultSandboxMode);
        _useMultiAgentOrchestration = settings.DefaultUseMultiAgentOrchestration;
        _maxAgentConcurrency = settings.DefaultMaxAgentConcurrency;
        _agentStrategy = settings.DefaultAgentStrategy;
        _orchestrationModel = string.IsNullOrWhiteSpace(settings.DefaultOrchestrationModel)
            ? settings.DefaultModel
            : settings.DefaultOrchestrationModel;
        _budgetDrivenModelSelection = settings.DefaultBudgetDrivenModelSelection;
        _budgetModel = string.IsNullOrWhiteSpace(settings.DefaultBudgetModel)
            ? CodexModelCatalog.DefaultBudgetModel
            : settings.DefaultBudgetModel;
        _selectedReasoning = CodexModelCatalog.ResolveReasoningEffort(
            EffectiveMainModel(),
            settings.DefaultReasoningEffort);
        _inputAreaHeight = ClampInputHeight(settings.DefaultInputAreaHeight);
    }

    /// <summary>Initializes stable collection instances used by WPF bindings.</summary>
    /// <param name="settings">The current extension settings.</param>
    [MemberNotNull(nameof(Messages), nameof(RunActivityRoots), nameof(Attachments))]
    [MemberNotNull(nameof(Skills), nameof(Memories), nameof(McpServers))]
    [MemberNotNull(nameof(McpToolSuggestions), nameof(McpToolInputFields), nameof(FileSuggestions))]
    [MemberNotNull(nameof(ContextSuggestions), nameof(PromptSuggestions), nameof(HistoryItems))]
    [MemberNotNull(nameof(VisibleHistoryItems), nameof(OrchestrationSections), nameof(AgentRoles))]
    [MemberNotNull(nameof(RateLimits), nameof(Prerequisites), nameof(ApprovalRequests))]
    [MemberNotNull(nameof(ModelOptions), nameof(ReasoningOptions), nameof(VerbosityOptions))]
    [MemberNotNull(nameof(ModeOptions), nameof(ApprovalOptions), nameof(SandboxOptions))]
    [MemberNotNull(nameof(AccessLevelOptions), nameof(TransportOptions))]
    [MemberNotNull(nameof(AgentStrategyOptions), nameof(AgentModelSelectionModeOptions))]
    private void InitializeCollections(ExtensionSettings settings)
    {
        Messages = new();
        RunActivityRoots = new();
        Attachments = new();
        Skills = new();
        Memories = new();
        McpServers = new();
        McpToolSuggestions = new();
        McpToolInputFields = new();
        FileSuggestions = new();
        ContextSuggestions = new();
        PromptSuggestions = new();
        HistoryItems = new();
        VisibleHistoryItems = new();
        OrchestrationSections = new();
        AgentRoles = new(settings.AgentRoles ?? new List<AgentRoleDefinition>());
        RateLimits = new(BuildDefaultRateLimits());
        Prerequisites = new();
        ApprovalRequests = new();
        ModelOptions = new(CodexModelCatalog.SupportedModels
            .Concat(settings.CustomModels ?? new List<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase));
        ReasoningOptions = new(CodexModelCatalog.GetReasoningEfforts(EffectiveMainModel()));
        VerbosityOptions = new(DistinctOptions(settings.CustomVerbosityOptions));
        ModeOptions = new((CodexRunMode[])Enum.GetValues(typeof(CodexRunMode)));
        ApprovalOptions = new((ApprovalPolicy[])Enum.GetValues(typeof(ApprovalPolicy)));
        SandboxOptions = new((SandboxMode[])Enum.GetValues(typeof(SandboxMode)));
        AccessLevelOptions = new((CodexAccessLevel[])Enum.GetValues(typeof(CodexAccessLevel)));
        TransportOptions = new((CodexTransportKind[])Enum.GetValues(typeof(CodexTransportKind)));
        AgentStrategyOptions = new(
            (AgentExecutionStrategy[])Enum.GetValues(typeof(AgentExecutionStrategy)));
        AgentModelSelectionModeOptions = new(
            (AgentModelSelectionMode[])Enum.GetValues(typeof(AgentModelSelectionMode)));
    }

    /// <summary>Initializes run-control commands and their observable availability.</summary>
    [MemberNotNull(
        nameof(RunCommand),
        nameof(CancelCommand),
        nameof(SteerCommand),
        nameof(QueueCommand),
        nameof(AlternateFollowUpCommand),
        nameof(PauseCommand),
        nameof(ResumeCommand),
        nameof(ApproveRequestCommand),
        nameof(DeclineRequestCommand))]
    private void InitializeRunCommands()
    {
        IObservable<bool> canRun = this.WhenAnyValue(
            (model) => model.Prompt,
            (prompt) => !string.IsNullOrWhiteSpace(prompt)).ObserveOn(_uiScheduler);
        IObservable<bool> canCancel =
            this.WhenAnyValue((model) => model.IsRunning).ObserveOn(_uiScheduler);
        IObservable<bool> canFollowUp = this.WhenAnyValue(
            (model) => model.Prompt,
            (model) => model.IsRunning,
            (prompt, running) => running && !string.IsNullOrWhiteSpace(prompt))
            .ObserveOn(_uiScheduler);
        IObservable<bool> canPause = this.WhenAnyValue(
            (model) => model.IsRunning,
            (model) => model.IsPaused,
            (running, paused) => running && !paused).ObserveOn(_uiScheduler);
        IObservable<bool> canResume =
            this.WhenAnyValue((model) => model.IsPaused).ObserveOn(_uiScheduler);
        RunCommand = ReactiveCommand.CreateFromTask(SubmitPromptAsync, canRun, _uiScheduler);
        CancelCommand = ReactiveCommand.CreateFromTask(StopActiveRunAsync, canCancel, _uiScheduler);
        SteerCommand = ReactiveCommand.CreateFromTask(SteerPromptAsync, canFollowUp, _uiScheduler);
        QueueCommand = ReactiveCommand.Create(QueuePrompt, canFollowUp, _uiScheduler);
        AlternateFollowUpCommand = ReactiveCommand.CreateFromTask(
            SubmitAlternateFollowUpAsync,
            canFollowUp,
            _uiScheduler);
        PauseCommand = ReactiveCommand.CreateFromTask(PauseActiveRunAsync, canPause, _uiScheduler);
        ResumeCommand = ReactiveCommand.CreateFromTask(ResumePausedRunAsync, canResume, _uiScheduler);
        ApproveRequestCommand = ReactiveCommand.CreateFromTask(
            (ApprovalRequest request) => RespondToApprovalAsync(request, approve: true),
            outputScheduler: _uiScheduler);
        DeclineRequestCommand = ReactiveCommand.CreateFromTask(
            (ApprovalRequest request) => RespondToApprovalAsync(request, approve: false),
            outputScheduler: _uiScheduler);
    }

    /// <summary>Initializes history, prerequisite, and analytics commands.</summary>
    [MemberNotNull(
        nameof(NewThreadCommand),
        nameof(ShowHistoryCommand),
        nameof(RefreshHistoryCommand),
        nameof(LoadHistoryCommand),
        nameof(DeleteHistoryCommand),
        nameof(BeginRenameHistoryCommand),
        nameof(SaveRenameHistoryCommand),
        nameof(CancelRenameHistoryCommand),
        nameof(CheckPrerequisitesCommand),
        nameof(CopyPrerequisiteCommand),
        nameof(UpdatePrerequisiteCommand),
        nameof(RefreshCommand),
        nameof(RefreshAnalyticsCommand),
        nameof(ApplyRecommendedModelCommand))]
    private void InitializeHistoryAndSetupCommands()
    {
        IObservable<bool> canShowHistory = this.WhenAnyValue(
            (model) => model.IsRunning,
            (running) => !running).ObserveOn(_uiScheduler);
        NewThreadCommand = ReactiveCommand.Create(StartNewThread, outputScheduler: _uiScheduler);
        ShowHistoryCommand = ReactiveCommand.Create(ShowHistory, canShowHistory, _uiScheduler);
        RefreshHistoryCommand = ReactiveCommand.Create(RefreshHistory, outputScheduler: _uiScheduler);
        LoadHistoryCommand = ReactiveCommand.Create<SessionHistoryItem>(
            LoadHistoryItem,
            outputScheduler: _uiScheduler);
        DeleteHistoryCommand = ReactiveCommand.Create<SessionHistoryItem>(
            DeleteHistoryItem,
            outputScheduler: _uiScheduler);
        BeginRenameHistoryCommand = ReactiveCommand.Create<SessionHistoryItem>(
            BeginRenameHistoryItem,
            outputScheduler: _uiScheduler);
        SaveRenameHistoryCommand = ReactiveCommand.Create<SessionHistoryItem>(
            SaveRenameHistoryItem,
            outputScheduler: _uiScheduler);
        CancelRenameHistoryCommand = ReactiveCommand.Create<SessionHistoryItem>(
            CancelRenameHistoryItem,
            outputScheduler: _uiScheduler);
        CheckPrerequisitesCommand = ReactiveCommand.CreateFromTask(
            CheckPrerequisitesAsync,
            outputScheduler: _uiScheduler);
        CopyPrerequisiteCommand = ReactiveCommand.Create<PrerequisiteStatus>(
            CopyPrerequisiteCommandToClipboard,
            outputScheduler: _uiScheduler);
        UpdatePrerequisiteCommand = ReactiveCommand.Create<PrerequisiteStatus>(
            StartPrerequisiteUpdate,
            outputScheduler: _uiScheduler);
        RefreshCommand = ReactiveCommand.Create(Refresh, outputScheduler: _uiScheduler);
        RefreshAnalyticsCommand = ReactiveCommand.Create(
            () => UpdateAnalytics(Prompt),
            outputScheduler: _uiScheduler);
        ApplyRecommendedModelCommand = ReactiveCommand.Create(
            ApplyRecommendedModel,
            outputScheduler: _uiScheduler);
    }

    /// <summary>Initializes memory, MCP, attachment, and skill commands.</summary>
    [MemberNotNull(
        nameof(AddUserMemoryCommand),
        nameof(AddWorkspaceMemoryCommand),
        nameof(ScanProjectMemoryCommand),
        nameof(AddImageAttachmentCommand),
        nameof(ToggleVoiceInputCommand),
        nameof(ClearAttachmentsCommand),
        nameof(SelectMcpServerCommand),
        nameof(SelectMcpToolCommand),
        nameof(InsertMcpToolCommand),
        nameof(AddMcpStdioServerCommand),
        nameof(AddMcpUrlServerCommand),
        nameof(RemoveMcpServerCommand),
        nameof(SaveMcpServersCommand),
        nameof(CreateSkillCommand),
        nameof(SaveSkillsCommand),
        nameof(AddSkillRootCommand),
        nameof(OpenSkillsFolderCommand),
        nameof(OpenCodexConfigCommand))]
    private void InitializeMemoryMcpAndSkillCommands()
    {
        InitializeMemoryAndAttachmentCommands();
        InitializeMcpAndSkillCommands();
    }

    /// <summary>Initializes memory and attachment commands.</summary>
    [MemberNotNull(
        nameof(AddUserMemoryCommand),
        nameof(AddWorkspaceMemoryCommand),
        nameof(ScanProjectMemoryCommand),
        nameof(AddImageAttachmentCommand),
        nameof(ToggleVoiceInputCommand),
        nameof(ClearAttachmentsCommand))]
    private void InitializeMemoryAndAttachmentCommands()
    {
        IObservable<bool> canSavePrompt = this.WhenAnyValue(
            (model) => model.Prompt,
            (prompt) => !string.IsNullOrWhiteSpace(prompt)).ObserveOn(_uiScheduler);
        IObservable<bool> canEdit = this.WhenAnyValue(
            (model) => model.CanEditSettings).ObserveOn(_uiScheduler);
        AddUserMemoryCommand = ReactiveCommand.CreateFromTask(
            () => AddMemoryAsync("user"),
            canSavePrompt,
            _uiScheduler);
        AddWorkspaceMemoryCommand = ReactiveCommand.CreateFromTask(
            () => AddMemoryAsync("workspace"),
            canSavePrompt,
            _uiScheduler);
        ScanProjectMemoryCommand = ReactiveCommand.CreateFromTask(
            ScanProjectMemoryAsync,
            canEdit,
            _uiScheduler);
        AddImageAttachmentCommand = ReactiveCommand.Create(
            AddImageAttachment,
            outputScheduler: _uiScheduler);
        ToggleVoiceInputCommand = ReactiveCommand.Create(
            ToggleVoiceInput,
            outputScheduler: _uiScheduler);
        ClearAttachmentsCommand = ReactiveCommand.Create(
            Attachments.Clear,
            outputScheduler: _uiScheduler);
    }

    /// <summary>Initializes MCP and skill commands.</summary>
    [MemberNotNull(nameof(SelectMcpServerCommand), nameof(SelectMcpToolCommand))]
    [MemberNotNull(nameof(InsertMcpToolCommand), nameof(AddMcpStdioServerCommand))]
    [MemberNotNull(nameof(AddMcpUrlServerCommand), nameof(RemoveMcpServerCommand))]
    [MemberNotNull(nameof(SaveMcpServersCommand), nameof(CreateSkillCommand))]
    [MemberNotNull(nameof(SaveSkillsCommand), nameof(AddSkillRootCommand))]
    [MemberNotNull(nameof(OpenSkillsFolderCommand), nameof(OpenCodexConfigCommand))]
    private void InitializeMcpAndSkillCommands()
    {
        IObservable<bool> canEdit = this.WhenAnyValue(
            (model) => model.CanEditSettings).ObserveOn(_uiScheduler);
        SelectMcpServerCommand = ReactiveCommand.CreateFromTask<McpServerDefinition>(
            SelectMcpServerAsync,
            outputScheduler: _uiScheduler);
        SelectMcpToolCommand = ReactiveCommand.Create<McpToolDefinition>(
            SelectMcpTool,
            outputScheduler: _uiScheduler);
        InsertMcpToolCommand = ReactiveCommand.Create(
            InsertMcpToolInvocation,
            outputScheduler: _uiScheduler);
        AddMcpStdioServerCommand = ReactiveCommand.Create(
            () => AddMcpServer("stdio"),
            canEdit,
            _uiScheduler);
        AddMcpUrlServerCommand = ReactiveCommand.Create(
            () => AddMcpServer("url"),
            canEdit,
            _uiScheduler);
        RemoveMcpServerCommand = ReactiveCommand.Create<McpServerDefinition>(
            RemoveMcpServer,
            outputScheduler: _uiScheduler);
        SaveMcpServersCommand = ReactiveCommand.Create(SaveMcpServers, canEdit, _uiScheduler);
        CreateSkillCommand = ReactiveCommand.Create(
            CreateSkill,
            this.WhenAnyValue(
                (model) => model.NewSkillName,
                (model) => model.CanEditSettings,
                (name, editable) => editable && IsValidSkillName(name)).ObserveOn(_uiScheduler),
            _uiScheduler);
        SaveSkillsCommand = ReactiveCommand.Create(SaveSkillSelection, canEdit, _uiScheduler);
        AddSkillRootCommand = ReactiveCommand.Create(
            AddSkillRoot,
            this.WhenAnyValue(
                (model) => model.SkillRootPathInput,
                (model) => model.CanEditSettings,
                (path, editable) => editable && Directory.Exists(path ?? string.Empty))
                .ObserveOn(_uiScheduler),
            _uiScheduler);
        OpenSkillsFolderCommand = ReactiveCommand.Create(
            OpenSkillsFolder,
            outputScheduler: _uiScheduler);
        OpenCodexConfigCommand = ReactiveCommand.Create(
            OpenCodexConfig,
            outputScheduler: _uiScheduler);
    }

    /// <summary>Initializes selection-assistant and message commands.</summary>
    [MemberNotNull(
        nameof(DebugSelectionCommand),
        nameof(CreateTestForSelectionCommand),
        nameof(CreatePlanCommand),
        nameof(ExplainSelectionCommand),
        nameof(FixSelectionCommand),
        nameof(ReviewSelectionCommand),
        nameof(OptimizeSelectionCommand),
        nameof(GenerateDocsCommand),
        nameof(CopyMessageCommand),
        nameof(CopyActivityDetailCommand),
        nameof(UseMessageAsPromptCommand),
        nameof(OpenActivityFileCommand))]
    private void InitializeAssistantCommands()
    {
        DebugSelectionCommand = ReactiveCommand.Create(
            (Action)(() => Prompt = _assistantContext.BuildDebugPrompt()),
            outputScheduler: _uiScheduler);
        CreateTestForSelectionCommand = ReactiveCommand.Create(
            (Action)(() => Prompt = _assistantContext.BuildTestPrompt()),
            outputScheduler: _uiScheduler);
        CreatePlanCommand = ReactiveCommand.Create(
            CreateAgentPlanPrompt,
            outputScheduler: _uiScheduler);
        ExplainSelectionCommand = ReactiveCommand.Create(
            (Action)(() => Prompt = _assistantContext.BuildExplainPrompt()),
            outputScheduler: _uiScheduler);
        FixSelectionCommand = ReactiveCommand.Create(
            (Action)(() => Prompt = _assistantContext.BuildFixPrompt()),
            outputScheduler: _uiScheduler);
        ReviewSelectionCommand = ReactiveCommand.Create(
            (Action)(() => Prompt = _assistantContext.BuildReviewPrompt()),
            outputScheduler: _uiScheduler);
        OptimizeSelectionCommand = ReactiveCommand.Create(
            (Action)(() => Prompt = _assistantContext.BuildOptimizePrompt()),
            outputScheduler: _uiScheduler);
        GenerateDocsCommand = ReactiveCommand.Create(
            (Action)(() => Prompt = _assistantContext.BuildDocumentationPrompt()),
            outputScheduler: _uiScheduler);
        CopyMessageCommand = ReactiveCommand.Create<ChatMessage>(
            CopyMessageToClipboard,
            outputScheduler: _uiScheduler);
        CopyActivityDetailCommand = ReactiveCommand.Create<RunActivityNode>(
            CopyActivityDetailToClipboard,
            outputScheduler: _uiScheduler);
        UseMessageAsPromptCommand = ReactiveCommand.Create<ChatMessage>(
            UseMessageAsPrompt,
            outputScheduler: _uiScheduler);
        OpenActivityFileCommand = ReactiveCommand.Create<RunActivityNode>(
            OpenActivityFile,
            outputScheduler: _uiScheduler);
    }

    /// <summary>Creates the view-model subscription lifetime.</summary>
    /// <returns>The composite subscription.</returns>
    private IDisposable CreateSubscriptions()
    {
        return new CompositeDisposableLike(
            _codex.Events.ObserveOnSafe(_uiScheduler).Subscribe(OnCodexEvent),
            _taskOrchestrator.Events.ObserveOnSafe(_uiScheduler).Subscribe(OnOrchestrationEvent),
            _skillIndex.Skills.ObserveOnSafe(_uiScheduler).Subscribe(UpdateSkills),
            _memoryStore.Memories.ObserveOnSafe(_uiScheduler).Subscribe(UpdateMemories),
            _mcpConfig.Servers.ObserveOnSafe(_uiScheduler).Subscribe(UpdateMcpServers),
            _settingsStore.SettingsChanged.ObserveOnSafe(_uiScheduler).Subscribe(
                ApplySettingsFromStore),
            _voiceInput.Transcript.ObserveOnSafe(_uiScheduler).Subscribe(AppendVoiceTranscript),
            _voiceInput.Status.ObserveOnSafe(_uiScheduler).Subscribe(UpdateVoiceInputStatus),
            this.WhenAnyValue((model) => model.Prompt)
                .ThrottleDistinct(TimeSpan.FromMilliseconds(Numeric180), _uiScheduler)
                .Subscribe(OnPromptChanged));
    }
}
