using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using ReactiveUI.Extensions;
using VSCodex.Infrastructure;
using VSCodex.Models;
using VSCodex.Services;

namespace VSCodex.ViewModels;

public sealed class VSCodexToolWindowViewModel : ReactiveObject, IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private readonly IMemoryStore _memoryStore;
    private readonly ISkillIndexService _skillIndex;
    private readonly IMcpConfigService _mcpConfig;
    private readonly IMcpToolCatalogService _mcpTools;
    private readonly IReactiveMemoryService _reactiveMemory;
    private readonly IWorkspaceContextService _workspace;
    private readonly ISessionStore _sessionStore;
    private readonly ICodexOrchestrator _codex;
    private readonly ITaskOrchestrationService _taskOrchestrator;
    private readonly ICodingAssistantContextService _assistantContext;
    private readonly IModelAnalyticsService _modelAnalytics;
    private readonly ICodexEnvironmentService _environment;
    private readonly IVoiceInputService _voiceInput;
    private readonly JoinableTaskFactory _joinableTaskFactory;
    private readonly Dispatcher _uiDispatcher;
    private readonly IScheduler _uiScheduler;
    private readonly IDisposable _subscriptions;
    private CodexSessionDocument _session;
    private int _promptChangeRevision;
    private string _lastWorkspaceIdentityId = string.Empty;
    private string _lastWorkspaceSettingsId = string.Empty;
    private readonly object _modelSettingsSaveGate = new object();
    private CancellationTokenSource? _modelSettingsSaveCancellation;
    private bool _hasPendingModelSettingsSave;
    private int _modelSettingsSaveRevision;
    private const int ModelSettingsSaveDebounceMilliseconds = 350;
    private const int AgentsToolTabIndex = 6;

    private string _prompt = string.Empty;
    private string _status = "Ready";
    private bool _isRunning;
    private bool _useMultiAgentOrchestration;
    private bool _budgetDrivenModelSelection;
    private bool _isToolPanelOpen;
    private int _maxAgentConcurrency = 1;
    private int _selectedToolTabIndex;
    private double _inputAreaHeight = 180d;
    private string _historySearchText = string.Empty;
    private AgentExecutionStrategy _agentStrategy = AgentExecutionStrategy.ReviewGate;
    private CodexRunMode _mode = CodexRunMode.Chat;
    private string _selectedModel;
    private string _failoverModel;
    private string _selectedReasoning;
    private string _selectedVerbosity;
    private string _orchestrationModel;
    private string _budgetModel;
    private ModelUsageEstimate _modelEstimate = new ModelUsageEstimate();
    private string _mcpInputPrompt = string.Empty;
    private string _newSkillName = string.Empty;
    private string _newSkillDescription = string.Empty;
    private string _skillRootPathInput = string.Empty;
    private string _rateLimitUpdatedAt = "Waiting for Codex rate-limit telemetry";
    private string _codexSetupSummary = "Checking VSCodex prerequisites...";
    private string _codexSetupInstructions = string.Empty;
    private string _voiceInputStatus = "Voice input ready";
    private ChatMessage? _activeProgressMessage;
    private DateTimeOffset _activeRunStartedAt;
    private string _activeRunStage = string.Empty;
    private CodexEnvironmentReport? _lastEnvironmentReport;
    private ApprovalPolicy _approvalPolicy;
    private SandboxMode _sandboxMode;
    private CodexAccessLevel _accessLevel;
    private CodexTransportKind _transport = CodexTransportKind.SdkBridge;
    private McpServerDefinition? _selectedMcpServer;
    private McpToolDefinition? _selectedMcpTool;
    private SessionHistoryItem? _selectedHistoryItem;
    private PromptSuggestionItem? _selectedPromptSuggestion;
    private bool _isPromptSuggestionOpen;
    private string? _threadId;

    public VSCodexToolWindowViewModel(
        ISettingsStore settingsStore,
        IMemoryStore memoryStore,
        ISkillIndexService skillIndex,
        IMcpConfigService mcpConfig,
        IMcpToolCatalogService mcpTools,
        IReactiveMemoryService reactiveMemory,
        IWorkspaceContextService workspace,
        ISessionStore sessionStore,
        ICodexOrchestrator codex,
        ITaskOrchestrationService taskOrchestrator,
        ICodingAssistantContextService assistantContext,
        IModelAnalyticsService modelAnalytics,
        ICodexEnvironmentService environment,
        IVoiceInputService voiceInput,
        JoinableTaskFactory joinableTaskFactory)
    {
        _settingsStore = settingsStore;
        _memoryStore = memoryStore;
        _skillIndex = skillIndex;
        _mcpConfig = mcpConfig;
        _mcpTools = mcpTools;
        _reactiveMemory = reactiveMemory;
        _workspace = workspace;
        _sessionStore = sessionStore;
        _codex = codex;
        _taskOrchestrator = taskOrchestrator;
        _assistantContext = assistantContext;
        _modelAnalytics = modelAnalytics;
        _environment = environment;
        _voiceInput = voiceInput;
        _joinableTaskFactory = joinableTaskFactory;
        _uiDispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _uiScheduler = new DispatcherScheduler(_uiDispatcher);
        _session = sessionStore.Create();

        var settings = _settingsStore.Current;
        _selectedModel = settings.DefaultModel;
        _failoverModel = string.IsNullOrWhiteSpace(settings.DefaultFailoverModel) ? "gpt-5.3-codex" : settings.DefaultFailoverModel;
        _selectedReasoning = settings.DefaultReasoningEffort;
        _selectedVerbosity = settings.DefaultVerbosity;
        _approvalPolicy = settings.DefaultApprovalPolicy;
        _sandboxMode = settings.DefaultSandboxMode;
        _accessLevel = AccessLevelFromSandbox(settings.DefaultSandboxMode);
        _useMultiAgentOrchestration = settings.DefaultUseMultiAgentOrchestration;
        _maxAgentConcurrency = settings.DefaultMaxAgentConcurrency;
        _agentStrategy = settings.DefaultAgentStrategy;
        _orchestrationModel = string.IsNullOrWhiteSpace(settings.DefaultOrchestrationModel) ? settings.DefaultModel : settings.DefaultOrchestrationModel;
        _budgetDrivenModelSelection = settings.DefaultBudgetDrivenModelSelection;
        _budgetModel = string.IsNullOrWhiteSpace(settings.DefaultBudgetModel) ? settings.DefaultModel : settings.DefaultBudgetModel;
        _inputAreaHeight = ClampInputHeight(settings.DefaultInputAreaHeight);

        Messages = new ObservableCollection<ChatMessage>();
        Attachments = new ObservableCollection<CodexAttachment>();
        Skills = new ObservableCollection<SkillDefinition>();
        Memories = new ObservableCollection<MemoryEntry>();
        McpServers = new ObservableCollection<McpServerDefinition>();
        McpToolSuggestions = new ObservableCollection<McpToolDefinition>();
        McpToolInputFields = new ObservableCollection<McpToolInputField>();
        FileSuggestions = new ObservableCollection<WorkspaceFileReference>();
        ContextSuggestions = new ObservableCollection<WorkspaceFileReference>();
        PromptSuggestions = new ObservableCollection<PromptSuggestionItem>();
        HistoryItems = new ObservableCollection<SessionHistoryItem>();
        VisibleHistoryItems = new ObservableCollection<SessionHistoryItem>();
        OrchestrationSections = new ObservableCollection<OrchestrationTaskSection>();
        AgentRoles = new ObservableCollection<AgentRoleDefinition>(settings.AgentRoles ?? new List<AgentRoleDefinition>());
        RateLimits = new ObservableCollection<RateLimitWindowStatus>(BuildDefaultRateLimits());
        Prerequisites = new ObservableCollection<PrerequisiteStatus>();

        ModelOptions = new ObservableCollection<string>(settings.CustomModels.Distinct(StringComparer.OrdinalIgnoreCase));
        ReasoningOptions = new ObservableCollection<string>(settings.CustomReasoningEfforts);
        VerbosityOptions = new ObservableCollection<string>(settings.CustomVerbosityOptions);
        ModeOptions = new ObservableCollection<CodexRunMode>((CodexRunMode[])Enum.GetValues(typeof(CodexRunMode)));
        ApprovalOptions = new ObservableCollection<ApprovalPolicy>((ApprovalPolicy[])Enum.GetValues(typeof(ApprovalPolicy)));
        SandboxOptions = new ObservableCollection<SandboxMode>((SandboxMode[])Enum.GetValues(typeof(SandboxMode)));
        AccessLevelOptions = new ObservableCollection<CodexAccessLevel>((CodexAccessLevel[])Enum.GetValues(typeof(CodexAccessLevel)));
        TransportOptions = new ObservableCollection<CodexTransportKind>((CodexTransportKind[])Enum.GetValues(typeof(CodexTransportKind)));
        AgentStrategyOptions = new ObservableCollection<AgentExecutionStrategy>((AgentExecutionStrategy[])Enum.GetValues(typeof(AgentExecutionStrategy)));
        AgentModelSelectionModeOptions = new ObservableCollection<AgentModelSelectionMode>((AgentModelSelectionMode[])Enum.GetValues(typeof(AgentModelSelectionMode)));

        var canRun = this.WhenAnyValue(x => x.Prompt, x => x.IsRunning, (p, r) => !string.IsNullOrWhiteSpace(p) && !r).ObserveOn(_uiScheduler);
        var canCancel = this.WhenAnyValue(x => x.IsRunning).ObserveOn(_uiScheduler);
        var canSavePrompt = this.WhenAnyValue(x => x.Prompt, p => !string.IsNullOrWhiteSpace(p)).ObserveOn(_uiScheduler);
        RunCommand = ReactiveCommand.CreateFromTask(RunAsync, canRun, _uiScheduler);
        CancelCommand = ReactiveCommand.Create(() => { _taskOrchestrator.Cancel(); _codex.Cancel(); }, canCancel, _uiScheduler);
        NewThreadCommand = ReactiveCommand.Create(StartNewThread, outputScheduler: _uiScheduler);
        ShowHistoryCommand = ReactiveCommand.Create(ShowHistory, this.WhenAnyValue(x => x.IsRunning, running => !running).ObserveOn(_uiScheduler), _uiScheduler);
        RefreshHistoryCommand = ReactiveCommand.Create(RefreshHistory, outputScheduler: _uiScheduler);
        LoadHistoryCommand = ReactiveCommand.Create<SessionHistoryItem>(LoadHistoryItem, outputScheduler: _uiScheduler);
        DeleteHistoryCommand = ReactiveCommand.Create<SessionHistoryItem>(DeleteHistoryItem, outputScheduler: _uiScheduler);
        BeginRenameHistoryCommand = ReactiveCommand.Create<SessionHistoryItem>(BeginRenameHistoryItem, outputScheduler: _uiScheduler);
        SaveRenameHistoryCommand = ReactiveCommand.Create<SessionHistoryItem>(SaveRenameHistoryItem, outputScheduler: _uiScheduler);
        CancelRenameHistoryCommand = ReactiveCommand.Create<SessionHistoryItem>(CancelRenameHistoryItem, outputScheduler: _uiScheduler);
        CheckPrerequisitesCommand = ReactiveCommand.CreateFromTask(CheckPrerequisitesAsync, null, _uiScheduler);
        RefreshCommand = ReactiveCommand.Create(Refresh, outputScheduler: _uiScheduler);
        RefreshAnalyticsCommand = ReactiveCommand.Create(() => UpdateAnalytics(Prompt), outputScheduler: _uiScheduler);
        ApplyRecommendedModelCommand = ReactiveCommand.Create(ApplyRecommendedModel, outputScheduler: _uiScheduler);
        AddUserMemoryCommand = ReactiveCommand.CreateFromTask(() => AddMemoryAsync("user"), canSavePrompt, _uiScheduler);
        AddWorkspaceMemoryCommand = ReactiveCommand.CreateFromTask(() => AddMemoryAsync("workspace"), canSavePrompt, _uiScheduler);
        ScanProjectMemoryCommand = ReactiveCommand.CreateFromTask(ScanProjectMemoryAsync, this.WhenAnyValue(x => x.CanEditSettings).ObserveOn(_uiScheduler), _uiScheduler);
        AddImageAttachmentCommand = ReactiveCommand.Create(AddImageAttachment, outputScheduler: _uiScheduler);
        ToggleVoiceInputCommand = ReactiveCommand.Create(ToggleVoiceInput, outputScheduler: _uiScheduler);
        ClearAttachmentsCommand = ReactiveCommand.Create(() => Attachments.Clear(), outputScheduler: _uiScheduler);
        SelectMcpServerCommand = ReactiveCommand.CreateFromTask<McpServerDefinition>(SelectMcpServerAsync, null, _uiScheduler);
        SelectMcpToolCommand = ReactiveCommand.Create<McpToolDefinition>(SelectMcpTool, outputScheduler: _uiScheduler);
        InsertMcpToolCommand = ReactiveCommand.Create(InsertMcpToolInvocation, outputScheduler: _uiScheduler);
        AddMcpStdioServerCommand = ReactiveCommand.Create(() => AddMcpServer("stdio"), this.WhenAnyValue(x => x.CanEditSettings).ObserveOn(_uiScheduler), _uiScheduler);
        AddMcpUrlServerCommand = ReactiveCommand.Create(() => AddMcpServer("url"), this.WhenAnyValue(x => x.CanEditSettings).ObserveOn(_uiScheduler), _uiScheduler);
        RemoveMcpServerCommand = ReactiveCommand.Create<McpServerDefinition>(RemoveMcpServer, null, _uiScheduler);
        SaveMcpServersCommand = ReactiveCommand.Create(SaveMcpServers, this.WhenAnyValue(x => x.CanEditSettings).ObserveOn(_uiScheduler), _uiScheduler);
        CreateSkillCommand = ReactiveCommand.Create(CreateSkill, this.WhenAnyValue(x => x.NewSkillName, x => x.CanEditSettings, (name, canEdit) => canEdit && IsValidSkillName(name)).ObserveOn(_uiScheduler), _uiScheduler);
        SaveSkillsCommand = ReactiveCommand.Create(SaveSkillSelection, this.WhenAnyValue(x => x.CanEditSettings).ObserveOn(_uiScheduler), _uiScheduler);
        AddSkillRootCommand = ReactiveCommand.Create(AddSkillRoot, this.WhenAnyValue(x => x.SkillRootPathInput, x => x.CanEditSettings, (path, canEdit) => canEdit && Directory.Exists(path ?? string.Empty)).ObserveOn(_uiScheduler), _uiScheduler);
        OpenSkillsFolderCommand = ReactiveCommand.Create(OpenSkillsFolder, outputScheduler: _uiScheduler);
        OpenCodexConfigCommand = ReactiveCommand.Create(OpenCodexConfig, outputScheduler: _uiScheduler);
        DebugSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildDebugPrompt(); }, outputScheduler: _uiScheduler);
        CreateTestForSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildTestPrompt(); }, outputScheduler: _uiScheduler);
        CreatePlanCommand = ReactiveCommand.Create(CreateAgentPlanPrompt, outputScheduler: _uiScheduler);
        ExplainSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildExplainPrompt(); }, outputScheduler: _uiScheduler);
        FixSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildFixPrompt(); }, outputScheduler: _uiScheduler);
        ReviewSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildReviewPrompt(); }, outputScheduler: _uiScheduler);
        OptimizeSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildOptimizePrompt(); }, outputScheduler: _uiScheduler);
        GenerateDocsCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildDocumentationPrompt(); }, outputScheduler: _uiScheduler);
        CopyMessageCommand = ReactiveCommand.Create<ChatMessage>(CopyMessageToClipboard, outputScheduler: _uiScheduler);
        UseMessageAsPromptCommand = ReactiveCommand.Create<ChatMessage>(UseMessageAsPrompt, outputScheduler: _uiScheduler);

        _subscriptions = new CompositeDisposableLike(
            _codex.Events.ObserveOnSafe(_uiScheduler).Subscribe(OnCodexEvent),
            _taskOrchestrator.Events.ObserveOnSafe(_uiScheduler).Subscribe(OnOrchestrationEvent),
            _skillIndex.Skills.ObserveOnSafe(_uiScheduler).Subscribe(UpdateSkills),
            _memoryStore.Memories.ObserveOnSafe(_uiScheduler).Subscribe(UpdateMemories),
            _mcpConfig.Servers.ObserveOnSafe(_uiScheduler).Subscribe(UpdateMcpServers),
            _settingsStore.SettingsChanged.ObserveOnSafe(_uiScheduler).Subscribe(ApplySettingsFromStore),
            _voiceInput.Transcript.ObserveOnSafe(_uiScheduler).Subscribe(AppendVoiceTranscript),
            _voiceInput.Status.ObserveOnSafe(_uiScheduler).Subscribe(UpdateVoiceInputStatus),
            this.WhenAnyValue(x => x.Prompt).ThrottleDistinct(TimeSpan.FromMilliseconds(180), _uiScheduler).Subscribe(OnPromptChanged),
            _voiceInput);

        Refresh();
        UpdateAnalytics(Prompt);
        _joinableTaskFactory.RunAsync(async () =>
        {
            await CheckPrerequisitesAsync().ConfigureAwait(true);
            await RefreshRateLimitsAsync().ConfigureAwait(true);
        }).Task.FireAndForget();
    }

    public ObservableCollection<ChatMessage> Messages { get; }
    public ObservableCollection<CodexAttachment> Attachments { get; }
    public ObservableCollection<SkillDefinition> Skills { get; }
    public ObservableCollection<MemoryEntry> Memories { get; }
    public ObservableCollection<McpServerDefinition> McpServers { get; }
    public ObservableCollection<McpToolDefinition> McpToolSuggestions { get; }
    public ObservableCollection<McpToolInputField> McpToolInputFields { get; }
    public ObservableCollection<WorkspaceFileReference> FileSuggestions { get; }
    public ObservableCollection<WorkspaceFileReference> ContextSuggestions { get; }
    public ObservableCollection<PromptSuggestionItem> PromptSuggestions { get; }
    public ObservableCollection<SessionHistoryItem> HistoryItems { get; }
    public ObservableCollection<SessionHistoryItem> VisibleHistoryItems { get; }
    public ObservableCollection<OrchestrationTaskSection> OrchestrationSections { get; }
    public ObservableCollection<AgentRoleDefinition> AgentRoles { get; }
    public ObservableCollection<RateLimitWindowStatus> RateLimits { get; }
    public ObservableCollection<PrerequisiteStatus> Prerequisites { get; }
    public ObservableCollection<string> ModelOptions { get; }
    public ObservableCollection<string> ReasoningOptions { get; }
    public ObservableCollection<string> VerbosityOptions { get; }
    public ObservableCollection<CodexRunMode> ModeOptions { get; }
    public ObservableCollection<ApprovalPolicy> ApprovalOptions { get; }
    public ObservableCollection<SandboxMode> SandboxOptions { get; }
    public ObservableCollection<CodexAccessLevel> AccessLevelOptions { get; }
    public ObservableCollection<CodexTransportKind> TransportOptions { get; }
    public ObservableCollection<AgentExecutionStrategy> AgentStrategyOptions { get; }
    public ObservableCollection<AgentModelSelectionMode> AgentModelSelectionModeOptions { get; }

    public ReactiveCommand<Unit, Unit> RunCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> NewThreadCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowHistoryCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshHistoryCommand { get; }
    public ReactiveCommand<SessionHistoryItem, Unit> LoadHistoryCommand { get; }
    public ReactiveCommand<SessionHistoryItem, Unit> DeleteHistoryCommand { get; }
    public ReactiveCommand<SessionHistoryItem, Unit> BeginRenameHistoryCommand { get; }
    public ReactiveCommand<SessionHistoryItem, Unit> SaveRenameHistoryCommand { get; }
    public ReactiveCommand<SessionHistoryItem, Unit> CancelRenameHistoryCommand { get; }
    public ReactiveCommand<Unit, Unit> CheckPrerequisitesCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshAnalyticsCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyRecommendedModelCommand { get; }
    public ReactiveCommand<Unit, Unit> AddUserMemoryCommand { get; }
    public ReactiveCommand<Unit, Unit> AddWorkspaceMemoryCommand { get; }
    public ReactiveCommand<Unit, Unit> ScanProjectMemoryCommand { get; }
    public ReactiveCommand<Unit, Unit> AddImageAttachmentCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleVoiceInputCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearAttachmentsCommand { get; }
    public ReactiveCommand<McpServerDefinition, Unit> SelectMcpServerCommand { get; }
    public ReactiveCommand<McpToolDefinition, Unit> SelectMcpToolCommand { get; }
    public ReactiveCommand<Unit, Unit> InsertMcpToolCommand { get; }
    public ReactiveCommand<Unit, Unit> AddMcpStdioServerCommand { get; }
    public ReactiveCommand<Unit, Unit> AddMcpUrlServerCommand { get; }
    public ReactiveCommand<McpServerDefinition, Unit> RemoveMcpServerCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveMcpServersCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateSkillCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSkillsCommand { get; }
    public ReactiveCommand<Unit, Unit> AddSkillRootCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSkillsFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCodexConfigCommand { get; }
    public ReactiveCommand<Unit, Unit> DebugSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateTestForSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> CreatePlanCommand { get; }
    public ReactiveCommand<Unit, Unit> ExplainSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> FixSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ReviewSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> OptimizeSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> GenerateDocsCommand { get; }
    public ReactiveCommand<ChatMessage, Unit> CopyMessageCommand { get; }
    public ReactiveCommand<ChatMessage, Unit> UseMessageAsPromptCommand { get; }

    public string Prompt { get => _prompt; set => this.RaiseAndSetIfChanged(ref _prompt, value); }
    public string Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value); }
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRunning, value);
            this.RaisePropertyChanged(nameof(CanEditSettings));
        }
    }

    public bool CanEditSettings => !IsRunning;
    public bool IsToolPanelOpen { get => _isToolPanelOpen; set => this.RaiseAndSetIfChanged(ref _isToolPanelOpen, value); }
    public bool UseMultiAgentOrchestration { get => _useMultiAgentOrchestration; set { if (!CanChangeSetting(_useMultiAgentOrchestration, value)) return; this.RaiseAndSetIfChanged(ref _useMultiAgentOrchestration, value); } }
    public bool BudgetDrivenModelSelection { get => _budgetDrivenModelSelection; set => SetModelSetting(ref _budgetDrivenModelSelection, value, nameof(BudgetDrivenModelSelection), refreshAnalytics: true); }
    public int MaxAgentConcurrency { get => _maxAgentConcurrency; set { var clamped = Math.Max(1, value); if (!CanChangeSetting(_maxAgentConcurrency, clamped)) return; this.RaiseAndSetIfChanged(ref _maxAgentConcurrency, clamped); } }
    public int SelectedToolTabIndex { get => _selectedToolTabIndex; set => this.RaiseAndSetIfChanged(ref _selectedToolTabIndex, Math.Max(0, value)); }
    public string HistorySearchText
    {
        get => _historySearchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _historySearchText, value ?? string.Empty);
            ApplyHistoryFilter();
        }
    }
    public bool HasVisibleHistory => VisibleHistoryItems.Count > 0;
    public double InputAreaHeight { get => _inputAreaHeight; set => SetInputAreaHeight(value); }
    public AgentExecutionStrategy AgentStrategy { get => _agentStrategy; set { if (!CanChangeSetting(_agentStrategy, value)) return; this.RaiseAndSetIfChanged(ref _agentStrategy, value); } }
    public CodexRunMode Mode { get => _mode; set { if (!CanChangeSetting(_mode, value)) return; this.RaiseAndSetIfChanged(ref _mode, value); } }
    public string SelectedModel { get => _selectedModel; set => SetModelSetting(ref _selectedModel, value, nameof(SelectedModel), refreshAnalytics: true); }
    public string FailoverModel { get => _failoverModel; set => SetModelSetting(ref _failoverModel, value, nameof(FailoverModel), refreshAnalytics: true); }
    public string SelectedReasoning { get => _selectedReasoning; set => SetModelSetting(ref _selectedReasoning, value, nameof(SelectedReasoning), refreshAnalytics: false); }
    public string SelectedVerbosity { get => _selectedVerbosity; set => SetModelSetting(ref _selectedVerbosity, value, nameof(SelectedVerbosity), refreshAnalytics: false); }
    public string OrchestrationModel { get => _orchestrationModel; set => SetModelSetting(ref _orchestrationModel, value, nameof(OrchestrationModel), refreshAnalytics: false); }
    public string BudgetModel { get => _budgetModel; set => SetModelSetting(ref _budgetModel, value, nameof(BudgetModel), refreshAnalytics: true); }
    public ModelUsageEstimate ModelEstimate
    {
        get => _modelEstimate;
        set
        {
            this.RaiseAndSetIfChanged(ref _modelEstimate, value ?? new ModelUsageEstimate());
            this.RaisePropertyChanged(nameof(AnalyticsSummary));
            this.RaisePropertyChanged(nameof(AnalyticsRecommendation));
            this.RaisePropertyChanged(nameof(ContextWindowSummary));
            this.RaisePropertyChanged(nameof(ContextRemainingSummary));
        }
    }
    public string AnalyticsSummary => ModelEstimate.Summary;
    public string AnalyticsRecommendation => ModelEstimate.RecommendationReason;
    public string ContextWindowSummary => ModelEstimate.ContextWindowTokens <= 0
        ? "Context size unavailable"
        : $"{FormatTokenCount(ModelEstimate.EstimatedInputTokens)} / {FormatTokenCount(ModelEstimate.ContextWindowTokens)} context tokens";
    public string ContextRemainingSummary => ModelEstimate.ContextWindowTokens <= 0
        ? string.Empty
        : $"{ModelEstimate.ContextRemainingPercent}% remaining ({FormatTokenCount(ModelEstimate.ContextRemainingTokens)})";
    public string McpInputPrompt { get => _mcpInputPrompt; set => this.RaiseAndSetIfChanged(ref _mcpInputPrompt, value); }
    public string NewSkillName { get => _newSkillName; set => this.RaiseAndSetIfChanged(ref _newSkillName, value ?? string.Empty); }
    public string NewSkillDescription { get => _newSkillDescription; set => this.RaiseAndSetIfChanged(ref _newSkillDescription, value ?? string.Empty); }
    public string SkillRootPathInput { get => _skillRootPathInput; set => this.RaiseAndSetIfChanged(ref _skillRootPathInput, value ?? string.Empty); }
    public string UserSkillsRoot => LocalPaths.UserSkillsRoot;
    public string CodexConfigPath => LocalPaths.UserCodexConfig;
    public string RateLimitUpdatedAt { get => _rateLimitUpdatedAt; set => this.RaiseAndSetIfChanged(ref _rateLimitUpdatedAt, value); }
    public string CodexSetupSummary { get => _codexSetupSummary; set => this.RaiseAndSetIfChanged(ref _codexSetupSummary, value); }
    public string CodexSetupInstructions { get => _codexSetupInstructions; set => this.RaiseAndSetIfChanged(ref _codexSetupInstructions, value); }
    public string VoiceInputStatus { get => _voiceInputStatus; set => this.RaiseAndSetIfChanged(ref _voiceInputStatus, value); }
    public bool IsVoiceInputAvailable => _voiceInput.IsAvailable;
    public bool IsListeningToVoice => _voiceInput.IsListening;
    public PromptSuggestionItem? SelectedPromptSuggestion { get => _selectedPromptSuggestion; set => this.RaiseAndSetIfChanged(ref _selectedPromptSuggestion, value); }
    public bool IsPromptSuggestionOpen { get => _isPromptSuggestionOpen; set => this.RaiseAndSetIfChanged(ref _isPromptSuggestionOpen, value); }
    public ApprovalPolicy ApprovalPolicy { get => _approvalPolicy; set => SetModelSetting(ref _approvalPolicy, value, nameof(ApprovalPolicy), refreshAnalytics: false); }
    public SandboxMode SandboxMode
    {
        get => _sandboxMode;
        set
        {
            if (EqualityComparer<SandboxMode>.Default.Equals(_sandboxMode, value)) return;
            if (!CanChangeSetting(_sandboxMode, value)) return;
            this.RaiseAndSetIfChanged(ref _sandboxMode, value);
            var accessLevel = AccessLevelFromSandbox(value);
            if (!EqualityComparer<CodexAccessLevel>.Default.Equals(_accessLevel, accessLevel))
            {
                this.RaiseAndSetIfChanged(ref _accessLevel, accessLevel, nameof(AccessLevel));
            }

            ScheduleModelSettingsSave(refreshAnalytics: false);
        }
    }
    public CodexAccessLevel AccessLevel
    {
        get => _accessLevel;
        set
        {
            if (!CanChangeSetting(_accessLevel, value)) return;
            this.RaiseAndSetIfChanged(ref _accessLevel, value);
            var sandbox = SandboxFromAccessLevel(value);
            if (!EqualityComparer<SandboxMode>.Default.Equals(_sandboxMode, sandbox))
            {
                this.RaiseAndSetIfChanged(ref _sandboxMode, sandbox, nameof(SandboxMode));
            }

            ScheduleModelSettingsSave(refreshAnalytics: false);
        }
    }
    public CodexTransportKind Transport { get => _transport; set { if (!CanChangeSetting(_transport, value)) return; this.RaiseAndSetIfChanged(ref _transport, value); } }
    public McpServerDefinition? SelectedMcpServer { get => _selectedMcpServer; set => this.RaiseAndSetIfChanged(ref _selectedMcpServer, value); }
    public McpToolDefinition? SelectedMcpTool { get => _selectedMcpTool; set => this.RaiseAndSetIfChanged(ref _selectedMcpTool, value); }
    public SessionHistoryItem? SelectedHistoryItem { get => _selectedHistoryItem; set => this.RaiseAndSetIfChanged(ref _selectedHistoryItem, value); }
    public string? ThreadId { get => _threadId; set => this.RaiseAndSetIfChanged(ref _threadId, value); }

    private bool CanChangeSetting<T>(T currentValue, T nextValue)
    {
        if (!IsRunning || EqualityComparer<T>.Default.Equals(currentValue, nextValue))
        {
            return true;
        }

        Status = "VSCodex settings are locked while a task is running";
        return false;
    }

    private void SetModelSetting<T>(ref T field, T value, string propertyName, bool refreshAnalytics)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        if (!CanChangeSetting(field, value)) return;
        this.RaiseAndSetIfChanged(ref field, value, propertyName);
        ScheduleModelSettingsSave(refreshAnalytics);
    }

    private void SetModelSetting(ref string field, string? value, string propertyName, bool refreshAnalytics)
    {
        var next = value ?? string.Empty;
        if (StringComparer.Ordinal.Equals(field, next)) return;
        if (!CanChangeSetting(field, next)) return;
        this.RaiseAndSetIfChanged(ref field, next, propertyName);
        ScheduleModelSettingsSave(refreshAnalytics);
    }

    public void SetLiveInputAreaHeight(double value) => SetInputAreaHeight(value);

    public void CommitInputAreaHeight(double value)
    {
        var clamped = SetInputAreaHeight(value);
        SaveInputAreaHeight(clamped);
    }

    private double SetInputAreaHeight(double value)
    {
        var clamped = ClampInputHeight(value);
        this.RaiseAndSetIfChanged(ref _inputAreaHeight, clamped, nameof(InputAreaHeight));
        return clamped;
    }

    private void ApplySettingsFromStore(ExtensionSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        var changed = false;
        changed |= SetPropertyFromSettings(ref _selectedModel, settings.DefaultModel, nameof(SelectedModel));
        changed |= SetPropertyFromSettings(ref _failoverModel, string.IsNullOrWhiteSpace(settings.DefaultFailoverModel) ? "gpt-5.3-codex" : settings.DefaultFailoverModel, nameof(FailoverModel));
        changed |= SetPropertyFromSettings(ref _selectedReasoning, settings.DefaultReasoningEffort, nameof(SelectedReasoning));
        changed |= SetPropertyFromSettings(ref _selectedVerbosity, settings.DefaultVerbosity, nameof(SelectedVerbosity));
        changed |= SetPropertyFromSettings(ref _approvalPolicy, settings.DefaultApprovalPolicy, nameof(ApprovalPolicy));
        changed |= SetPropertyFromSettings(ref _sandboxMode, settings.DefaultSandboxMode, nameof(SandboxMode));
        changed |= SetPropertyFromSettings(ref _accessLevel, AccessLevelFromSandbox(settings.DefaultSandboxMode), nameof(AccessLevel));
        changed |= SetPropertyFromSettings(ref _useMultiAgentOrchestration, settings.DefaultUseMultiAgentOrchestration, nameof(UseMultiAgentOrchestration));
        changed |= SetPropertyFromSettings(ref _maxAgentConcurrency, Math.Max(1, settings.DefaultMaxAgentConcurrency), nameof(MaxAgentConcurrency));
        changed |= SetPropertyFromSettings(ref _agentStrategy, settings.DefaultAgentStrategy, nameof(AgentStrategy));
        changed |= SetPropertyFromSettings(ref _orchestrationModel, string.IsNullOrWhiteSpace(settings.DefaultOrchestrationModel) ? settings.DefaultModel : settings.DefaultOrchestrationModel, nameof(OrchestrationModel));
        changed |= SetPropertyFromSettings(ref _budgetDrivenModelSelection, settings.DefaultBudgetDrivenModelSelection, nameof(BudgetDrivenModelSelection));
        changed |= SetPropertyFromSettings(ref _budgetModel, string.IsNullOrWhiteSpace(settings.DefaultBudgetModel) ? settings.DefaultModel : settings.DefaultBudgetModel, nameof(BudgetModel));
        changed |= SetPropertyFromSettings(ref _inputAreaHeight, ClampInputHeight(settings.DefaultInputAreaHeight), nameof(InputAreaHeight));

        changed |= ReplaceCollection(ModelOptions, settings.CustomModels.Distinct(StringComparer.OrdinalIgnoreCase));
        changed |= ReplaceCollection(ReasoningOptions, settings.CustomReasoningEfforts);
        changed |= ReplaceCollection(VerbosityOptions, settings.CustomVerbosityOptions);
        changed |= ReplaceCollection(AgentRoles, settings.AgentRoles ?? new List<AgentRoleDefinition>());
        if (changed)
        {
            UpdateAnalytics(Prompt);
        }
    }

    private bool SetPropertyFromSettings<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        this.RaisePropertyChanged(propertyName);
        return true;
    }

    private static bool ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        var snapshot = (values ?? Enumerable.Empty<T>()).ToList();
        if (collection.Count == snapshot.Count && collection.Zip(snapshot, EqualityComparer<T>.Default.Equals).All(x => x))
        {
            return false;
        }

        collection.Clear();
        foreach (var value in snapshot) collection.Add(value);
        return true;
    }

    private async Task RunAsync()
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync();
        if (TryHandleLocalSlashCommand(Prompt))
        {
            return;
        }

        if (!await EnsureCodexSdkReadyForRunAsync().ConfigureAwait(true))
        {
            return;
        }

        var userPrompt = ExpandAssistantSlashCommand(Prompt);
        if (IsMcpDiscoveryPrompt(userPrompt))
        {
            ShowMcpServerList();
            SelectedToolTabIndex = 3;
            IsToolPanelOpen = true;
            return;
        }

        Refresh();
        if (!EnsureWorkspaceReadyForRun())
        {
            return;
        }

        Prompt = string.Empty;
        IsRunning = true;
        Status = "Running VSCodex...";
        AddMessage(CodexMessageRole.User, userPrompt);
        var progressSubscription = StartRunProgress("Preparing request for " + _workspace.CurrentWorkspaceRoot);
        try
        {
            SetRunProgress("Updating ReactiveMemory context");
            var memoryReaction = await _reactiveMemory.ReactToPromptAsync(userPrompt, _workspace.CurrentWorkspaceIdentity, ThreadId).ConfigureAwait(false);
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            SetRunProgress(memoryReaction.Success ? memoryReaction.Message : "ReactiveMemory unavailable; continuing with local context");
            SetRunProgress("Resolving VSCodex references and attachments");
            var workspaceFiles = _workspace.ResolveMentions(userPrompt, 12000)
                .Concat(_workspace.ResolveHashReferences(userPrompt, 0))
                .GroupBy(x => string.IsNullOrWhiteSpace(x.ReferenceKey) ? x.Path : x.ReferenceKey, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();
            var selectedAgents = ApplyModelSelection(AgentRoles.Where(x => x.IsEnabled)).ToList();
            var options = new CodexRunOptions
            {
                Mode = Mode,
                Model = EffectiveMainModel(),
                FailoverModel = FailoverModel,
                ReasoningEffort = SelectedReasoning,
                Verbosity = SelectedVerbosity,
                ApprovalPolicy = ApprovalPolicy,
                SandboxMode = SandboxMode,
                Transport = Transport,
                UseMultiAgentOrchestration = UseMultiAgentOrchestration,
                MaxAgentConcurrency = MaxAgentConcurrency,
                AgentStrategy = AgentStrategy,
                OrchestrationModel = EffectiveOrchestrationModel(),
                BudgetDrivenModelSelection = BudgetDrivenModelSelection,
                BudgetModel = BudgetModel
            };
            var request = new CodexRunRequest { Prompt = userPrompt, ThreadId = ThreadId, WorkspaceRoot = _workspace.CurrentWorkspaceRoot, WorkspaceName = _workspace.CurrentWorkspaceName, WorkspaceSolutionPath = _workspace.CurrentSolutionPath, WorkspaceMemoryRoot = _workspace.CurrentWorkspaceMemoryRoot, WorkspaceIdentity = _workspace.CurrentWorkspaceIdentity, Options = options, Attachments = Attachments.ToList(), Skills = Skills.Where(x => x.IsEnabled).ToList(), Memories = _memoryStore.Search(userPrompt, 10), McpServers = McpServers.Where(x => x.IsEnabled).ToList(), WorkspaceFiles = workspaceFiles, AgentRoles = selectedAgents };
            ModelEstimate = _modelAnalytics.Estimate(request);
            this.RaisePropertyChanged(nameof(AnalyticsSummary));
            this.RaisePropertyChanged(nameof(AnalyticsRecommendation));
            SetRunProgress("Sending request to Codex. Longer project analysis can take several minutes.");
            var result = await (UseMultiAgentOrchestration ? _taskOrchestrator.RunAsync(request) : _codex.RunAsync(request)).ConfigureAwait(false);
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            UpdateRateLimitsFromJson(result.RawJson);
            ThreadId = result.ThreadId ?? ThreadId;
            AddMessage(CodexMessageRole.Assistant, result.FinalResponse);
            _ = _reactiveMemory.WriteDiaryAsync(userPrompt, result.FinalResponse, _workspace.CurrentWorkspaceIdentity, ThreadId);
            FinishRunProgress(result.UsedFallback ? "Completed using CLI fallback" : "Completed");
            Status = result.UsedFallback ? "Complete using CLI fallback" : "Complete";
            _session.ThreadId = ThreadId;
            if (string.IsNullOrWhiteSpace(_session.Title))
            {
                _session.Title = DeriveSessionTitle(_session);
            }

            _sessionStore.Save(_session);
            RefreshHistory();
        }
        catch (Exception ex)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            FinishRunProgress("Failed: " + ex.Message);
            AddMessage(CodexMessageRole.Error, ex.ToString());
            Status = "Failed: " + ex.Message;
        }
        finally
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            progressSubscription.Dispose();
            IsRunning = false;
        }
    }

    private void Refresh()
    {
        try
        {
            var previousIdentity = _lastWorkspaceIdentityId;
            _workspace.Refresh();
            var currentIdentity = _workspace.CurrentWorkspaceIdentity.Id;
            if (!string.IsNullOrWhiteSpace(currentIdentity) && !string.Equals(_lastWorkspaceSettingsId, currentIdentity, StringComparison.OrdinalIgnoreCase))
            {
                _lastWorkspaceSettingsId = currentIdentity;
                ApplySettingsFromStore(_settingsStore.LoadForWorkspace(_workspace.CurrentWorkspaceIdentity));
            }

            if (!string.IsNullOrWhiteSpace(previousIdentity) && !string.Equals(previousIdentity, currentIdentity, StringComparison.OrdinalIgnoreCase))
            {
                ThreadId = null;
                _codex.Cancel();
            }

            _lastWorkspaceIdentityId = currentIdentity;
            _memoryStore.LoadWorkspace(_workspace.CurrentWorkspaceRoot);
            _skillIndex.Refresh(_settingsStore.Current.SkillRoots.Concat(new[] { System.IO.Path.Combine(_workspace.CurrentWorkspaceRoot ?? string.Empty, ".codex", "skills") }));
            _mcpConfig.Refresh();
            Status = string.IsNullOrWhiteSpace(_workspace.CurrentWorkspaceRoot) ? "Visual Studio solution context is still loading" : "Refreshed VSCodex context for " + _workspace.CurrentWorkspaceRoot;
            RefreshRateLimitsInBackground();
        }
        catch (Exception ex) { Status = "Refresh failed: " + ex.Message; }
    }

    private async Task ScanProjectMemoryAsync()
    {
        var identity = _workspace.CurrentWorkspaceIdentity;
        if (identity == null || string.IsNullOrWhiteSpace(identity.RootPath))
        {
            Status = "Open a solution before scanning project memory";
            return;
        }

        Status = "Scanning project memory with ReactiveMemory ProjectMiner...";
        var scan = await Task.Run(async () => await _reactiveMemory.ScanWorkspaceAsync(identity, automatic: false).ConfigureAwait(false)).ConfigureAwait(false);
        await _joinableTaskFactory.SwitchToMainThreadAsync();
        Status = scan.Message;
        if (!scan.Success)
        {
            AddMessage(CodexMessageRole.System, scan.Message);
        }
    }

    private bool EnsureWorkspaceReadyForRun()
    {
        var root = _workspace.CurrentWorkspaceRoot;
        if (!string.IsNullOrWhiteSpace(root)
            && Directory.Exists(root)
            && !root.StartsWith(LocalPaths.ExtensionInstallRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var message = "VSCodex cannot run yet because Visual Studio has not provided a solution or project workspace root. Wait for the solution to finish loading, open a solution/project, or use @ references after a workspace is available. The installed VSIX folder will not be used as the execution root.";
        AddMessage(CodexMessageRole.System, message);
        Status = "VSCodex waiting for Visual Studio solution context";
        return false;
    }

    private async Task SelectMcpServerAsync(McpServerDefinition server)
    {
        if (server == null) return;
        await _joinableTaskFactory.SwitchToMainThreadAsync();
        SelectedMcpServer = server;
        Status = "Discovering MCP tools for " + server.Name + "...";
        var tools = await Task.Run(async () => await _mcpTools.DiscoverToolsAsync(server).ConfigureAwait(false)).ConfigureAwait(false);
        await _joinableTaskFactory.SwitchToMainThreadAsync();
        Replace(McpToolSuggestions, tools);
        Replace(McpToolInputFields, Array.Empty<McpToolInputField>());
        SelectedMcpTool = null;
        Status = tools.Count == 0 ? "No MCP tools discovered" : $"Discovered {tools.Count} MCP tool(s) for {server.Name}";
    }

    private void SelectMcpTool(McpToolDefinition tool)
    {
        if (tool == null) return;
        SelectedMcpTool = tool;
        Replace(McpToolInputFields, tool.InputFields.Select(CloneField));
        McpInputPrompt = tool.InputFields.Count == 0 ? "No input required." : "Provide values for the fields below. Optional fields show 'option' after the field name.";
        Status = "Selected MCP tool " + tool.DisplayName;
    }

    private void InsertMcpToolInvocation()
    {
        if (SelectedMcpServer == null || SelectedMcpTool == null) { ShowMcpServerList(); return; }
        SelectedMcpTool.InputFields = McpToolInputFields.Select(CloneField).ToList();
        var invocation = _mcpTools.BuildInvocationPrompt(SelectedMcpServer, SelectedMcpTool);
        Prompt = string.IsNullOrWhiteSpace(Prompt) ? invocation : Prompt.TrimEnd() + Environment.NewLine + invocation;
        Status = "Inserted MCP tool invocation into prompt";
    }

    private void AddMcpServer(string transportType)
    {
        if (!CanEditSettings)
        {
            Status = "VSCodex settings are locked while a task is running";
            return;
        }

        var server = _mcpConfig.CreateTemplate(transportType);
        McpServers.Add(server);
        SelectedMcpServer = server;
        Status = "Added MCP server draft. Fill in the details, then save MCP servers.";
    }

    private void RemoveMcpServer(McpServerDefinition server)
    {
        if (server == null || !CanEditSettings)
        {
            return;
        }

        McpServers.Remove(server);
        if (ReferenceEquals(SelectedMcpServer, server))
        {
            SelectedMcpServer = McpServers.FirstOrDefault();
        }

        Status = "Removed MCP server draft. Save MCP servers to update Codex config.";
    }

    private void SaveMcpServers()
    {
        if (!CanEditSettings)
        {
            Status = "VSCodex settings are locked while a task is running";
            return;
        }

        _mcpConfig.Save(McpServers.ToList());
        Status = "Saved MCP servers to " + LocalPaths.UserCodexConfig;
    }

    private void CreateSkill()
    {
        if (!CanEditSettings)
        {
            Status = "VSCodex settings are locked while a task is running";
            return;
        }

        try
        {
            Directory.CreateDirectory(LocalPaths.UserSkillsRoot);
            var skillPath = _skillIndex.CreateSkill(LocalPaths.UserSkillsRoot, NewSkillName, NewSkillDescription);
            NewSkillName = string.Empty;
            NewSkillDescription = string.Empty;
            Refresh();
            Status = "Created skill " + skillPath;
            OpenPath(skillPath);
        }
        catch (Exception ex)
        {
            Status = "Create skill failed: " + ex.Message;
        }
    }

    private void SaveSkillSelection()
    {
        if (!CanEditSettings)
        {
            Status = "VSCodex settings are locked while a task is running";
            return;
        }

        var settings = _settingsStore.Current;
        settings.EnabledSkillPaths = Skills
            .Where(x => x.IsEnabled)
            .Select(x => x.MarkdownPath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        SaveSettingsForCurrentWorkspace(settings);
        Status = $"Saved {settings.EnabledSkillPaths.Count} enabled skill(s)";
        UpdateAnalytics(Prompt);
    }

    private void AddSkillRoot()
    {
        if (!CanEditSettings)
        {
            Status = "VSCodex settings are locked while a task is running";
            return;
        }

        var path = SkillRootPathInput.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            Status = "Enter an existing folder to add a skill root";
            return;
        }

        var settings = _settingsStore.Current;
        if (!settings.SkillRoots.Any(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase)))
        {
            settings.SkillRoots.Add(path);
            SaveSettingsForCurrentWorkspace(settings);
        }

        SkillRootPathInput = string.Empty;
        Refresh();
        Status = "Added skill root " + path;
    }

    private void OpenSkillsFolder()
    {
        Directory.CreateDirectory(LocalPaths.UserSkillsRoot);
        OpenPath(LocalPaths.UserSkillsRoot);
    }

    private void OpenCodexConfig()
    {
        var directory = Path.GetDirectoryName(LocalPaths.UserCodexConfig);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(LocalPaths.UserCodexConfig))
        {
            File.WriteAllText(LocalPaths.UserCodexConfig, string.Empty);
        }

        OpenPath(LocalPaths.UserCodexConfig);
    }

    private void OnPromptChanged(string prompt)
    {
        var revision = Interlocked.Increment(ref _promptChangeRevision);
        _joinableTaskFactory.RunAsync(async () =>
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            if (revision != Volatile.Read(ref _promptChangeRevision) || !string.Equals(prompt, Prompt, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                UpdateReferenceSuggestions(prompt);
                UpdatePromptSuggestions(prompt);
                UpdateAnalytics(prompt);
                if (IsMcpDiscoveryPrompt(prompt)) ShowMcpServerList();
            }
            catch (Exception ex)
            {
                Status = "Prompt context update failed: " + ex.Message;
            }
        }).Task.FireAndForget();
    }

    private void ShowMcpServerList()
    {
        Replace(McpToolSuggestions, Array.Empty<McpToolDefinition>());
        Replace(McpToolInputFields, Array.Empty<McpToolInputField>());
        McpInputPrompt = "Select an MCP server to list tools. Then select a tool and provide required input fields; optional fields show 'option'.";
        Status = McpServers.Count == 0 ? "No MCP servers are configured in .codex/config.toml" : "Select an MCP server from the MCP tab";
    }

    private bool TryHandleLocalSlashCommand(string value)
    {
        var command = (value ?? string.Empty).Trim();
        if (command.Length == 0 || command[0] != '/')
        {
            return false;
        }

        var name = command.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        switch (name.ToLowerInvariant())
        {
            case "/history":
            case "/threads":
                ShowHistory();
                Prompt = string.Empty;
                return true;
            case "/settings":
            case "/models":
                Status = "Open Tools > Options > VSCodex to change settings";
                Prompt = string.Empty;
                return true;
            case "/context":
            case "/files":
            case "/selection":
                ShowToolPanel(1, "VSCodex context");
                Prompt = string.Empty;
                return true;
            case "/skills":
                ShowToolPanel(2, "VSCodex skills");
                Prompt = string.Empty;
                return true;
            case "/mcp":
            case "/tools":
                ShowMcpServerList();
                ShowToolPanel(3, "VSCodex MCP tools");
                Prompt = string.Empty;
                return true;
            case "/analytics":
                ShowToolPanel(4, "VSCodex analytics");
                Prompt = string.Empty;
                return true;
            case "/memory":
                ShowToolPanel(5, "VSCodex memory");
                Prompt = string.Empty;
                return true;
            case "/agents":
                ShowToolPanel(6, "VSCodex agents");
                Prompt = string.Empty;
                return true;
            case "/attachments":
                ShowToolPanel(7, "VSCodex attachments");
                Prompt = string.Empty;
                return true;
            case "/refresh":
                Refresh();
                Prompt = string.Empty;
                return true;
            default:
                return false;
        }
    }

    private void ShowToolPanel(int tabIndex, string status)
    {
        IsToolPanelOpen = true;
        SelectedToolTabIndex = tabIndex;
        Status = status;
    }

    private async Task AddMemoryAsync(string scope)
    {
        var text = Prompt;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var memory = await _reactiveMemory.AddMemoryAsync(text, scope, _workspace.CurrentWorkspaceIdentity).ConfigureAwait(false);
        await _joinableTaskFactory.SwitchToMainThreadAsync();
        if (memory.Success)
        {
            _memoryStore.Add(text, scope);
            Status = memory.Message;
        }
        else
        {
            Status = "ReactiveMemory did not save memory: " + memory.Message;
        }
    }
    private void AddImageAttachment()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Attach files for VSCodex", Filter = "Supported files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md;*.cs;*.xaml;*.json;*.xml|Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|Documents|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md|All files|*.*", Multiselect = true };
        if (dialog.ShowDialog() == true) AttachFiles(dialog.FileNames);
    }

    private void ToggleVoiceInput()
    {
        if (_voiceInput.IsListening)
        {
            _voiceInput.Stop();
        }
        else
        {
            _voiceInput.Start();
        }

        this.RaisePropertyChanged(nameof(IsListeningToVoice));
        this.RaisePropertyChanged(nameof(IsVoiceInputAvailable));
    }

    private void AppendVoiceTranscript(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Prompt = string.IsNullOrWhiteSpace(Prompt) ? text.Trim() : Prompt.TrimEnd() + " " + text.Trim();
        Status = "Voice transcript added";
    }

    private void UpdateVoiceInputStatus(string status)
    {
        VoiceInputStatus = status;
        this.RaisePropertyChanged(nameof(IsListeningToVoice));
        this.RaisePropertyChanged(nameof(IsVoiceInputAvailable));
    }

    public void ShowHistory()
    {
        if (IsRunning)
        {
            Status = "VSCodex history is locked while a task is running";
            return;
        }

        RefreshHistory();
        IsToolPanelOpen = true;
        SelectedToolTabIndex = 0;
        Status = "VSCodex history";
    }

    private async Task CheckPrerequisitesAsync()
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync();
        Status = "Checking VSCodex prerequisites...";
        CodexSetupSummary = "Checking VSCodex prerequisites...";
        var report = await _environment.CheckAsync(_settingsStore.Current).ConfigureAwait(false);
        await _joinableTaskFactory.SwitchToMainThreadAsync();
        ApplyEnvironmentReport(report, showSystemMessage: !report.CanRunSdkBridge);
    }

    private void RefreshRateLimitsInBackground()
    {
        _joinableTaskFactory.RunAsync(async () => await RefreshRateLimitsAsync().ConfigureAwait(true)).Task.FireAndForget();
    }

    private async Task RefreshRateLimitsAsync()
    {
        var report = _lastEnvironmentReport;
        if (report != null && !report.CanRunSdkBridge)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            SetRateLimitsUnavailable("Codex SDK unavailable");
            return;
        }

        await _joinableTaskFactory.SwitchToMainThreadAsync();
        SetRateLimitRows("Fetching Codex telemetry", 0, string.Empty);
        RateLimitUpdatedAt = "Checking Codex rate-limit telemetry";
        try
        {
            var rateLimits = await _codex.GetRateLimitsAsync().ConfigureAwait(false);
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            if (rateLimits == null)
            {
                SetRateLimitsUnavailable("Codex telemetry unavailable");
                return;
            }

            UpdateRateLimitsFromJson(rateLimits.ToString());
        }
        catch (Exception ex)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            SetRateLimitsUnavailable("Codex telemetry unavailable");
            RateLimitUpdatedAt = "Codex rate-limit check failed: " + ex.Message;
        }
    }

    private void SetRateLimitsUnavailable(string text) => SetRateLimitRows(text, 0, string.Empty);

    private void SetRateLimitRows(string remaining, int usagePercent, string resetText)
    {
        foreach (var row in RateLimits)
        {
            row.Remaining = remaining;
            row.UsagePercent = usagePercent;
            row.ResetText = resetText;
        }
    }

    private async Task<bool> EnsureCodexSdkReadyForRunAsync()
    {
        var report = _lastEnvironmentReport;
        if (report == null || !report.CanRunSdkBridge)
        {
            report = await _environment.CheckAsync(_settingsStore.Current).ConfigureAwait(false);
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            ApplyEnvironmentReport(report, showSystemMessage: !report.CanRunSdkBridge);
        }

        if (report.CanRunSdkBridge)
        {
            return true;
        }

        AddMessage(CodexMessageRole.System, CodexSetupInstructions);
        Status = "VSCodex setup required. Open Tools > Options > VSCodex to adjust runtime paths.";
        return false;
    }

    private void ApplyEnvironmentReport(CodexEnvironmentReport report, bool showSystemMessage)
    {
        _lastEnvironmentReport = report;
        Replace(Prerequisites, report.Items);
        CodexSetupSummary = report.Summary;
        CodexSetupInstructions = report.Instructions;
        Status = report.CanRunSdkBridge ? "VSCodex prerequisites ready" : "VSCodex setup required";
        if (showSystemMessage)
        {
            AddMessage(CodexMessageRole.System, report.Summary + Environment.NewLine + Environment.NewLine + report.Instructions);
        }
    }

    private void StartNewThread()
    {
        SaveCurrentSessionIfNeeded();
        _session = _sessionStore.Create();
        Prompt = string.Empty;
        ThreadId = null;
        Messages.Clear();
        Attachments.Clear();
        OrchestrationSections.Clear();
        Status = "New VSCodex thread";
        RefreshHistory();
        UpdateAnalytics(Prompt);
    }

    private void RefreshHistory()
    {
        var items = _sessionStore.LoadRecent(100)
            .Where(session => session.Messages.Count > 0 || !string.IsNullOrWhiteSpace(session.ThreadId))
            .Select(BuildHistoryItem)
            .ToList();
        Replace(HistoryItems, items);
        ApplyHistoryFilter(items);
    }

    private void ApplyHistoryFilter() => ApplyHistoryFilter(HistoryItems.ToList());

    private void ApplyHistoryFilter(IEnumerable<SessionHistoryItem> source)
    {
        var query = (HistorySearchText ?? string.Empty).Trim();
        var items = source ?? Enumerable.Empty<SessionHistoryItem>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(item =>
                Contains(item.Title, query)
                || Contains(item.Preview, query)
                || Contains(item.ThreadId, query));
        }

        Replace(VisibleHistoryItems, items.ToList());
        this.RaisePropertyChanged(nameof(HasVisibleHistory));
    }

    private void LoadHistoryItem(SessionHistoryItem item)
    {
        if (item == null)
        {
            return;
        }

        var loaded = _sessionStore.Load(item.Id);
        if (loaded == null)
        {
            Status = "VSCodex history item could not be loaded";
            RefreshHistory();
            return;
        }

        SaveCurrentSessionIfNeeded();
        _session = loaded;
        ThreadId = loaded.ThreadId;
        Prompt = string.Empty;
        Messages.Clear();
        foreach (var message in loaded.Messages ?? new List<ChatMessage>())
        {
            Messages.Add(message);
        }

        Attachments.Clear();
        SelectedHistoryItem = item;
        IsToolPanelOpen = false;
        Status = "Loaded history: " + item.Title;
        UpdateAnalytics(Prompt);
    }

    private void DeleteHistoryItem(SessionHistoryItem item)
    {
        if (item == null)
        {
            return;
        }

        var deletingCurrentSession = string.Equals(item.Id, _session.Id, StringComparison.OrdinalIgnoreCase);
        _sessionStore.Delete(item.Id);
        if (deletingCurrentSession)
        {
            _session = _sessionStore.Create();
            Prompt = string.Empty;
            ThreadId = null;
            Messages.Clear();
            Attachments.Clear();
            UpdateAnalytics(Prompt);
        }

        RefreshHistory();
        Status = "Deleted history item";
    }

    private void BeginRenameHistoryItem(SessionHistoryItem item)
    {
        if (item == null)
        {
            return;
        }

        item.DraftTitle = item.Title;
        item.IsRenaming = true;
    }

    private void SaveRenameHistoryItem(SessionHistoryItem item)
    {
        if (item == null)
        {
            return;
        }

        var title = CompactLine(item.DraftTitle, 120);
        if (string.IsNullOrWhiteSpace(title))
        {
            CancelRenameHistoryItem(item);
            return;
        }

        var loaded = _sessionStore.Load(item.Id);
        if (loaded == null)
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

    private static void CancelRenameHistoryItem(SessionHistoryItem item)
    {
        if (item == null)
        {
            return;
        }

        item.DraftTitle = item.Title;
        item.IsRenaming = false;
    }

    private void SaveCurrentSessionIfNeeded()
    {
        if (_session.Messages.Count == 0 && string.IsNullOrWhiteSpace(_session.ThreadId))
        {
            return;
        }

        _session.ThreadId = ThreadId;
        if (string.IsNullOrWhiteSpace(_session.Title))
        {
            _session.Title = DeriveSessionTitle(_session);
        }

        _sessionStore.Save(_session);
        RefreshHistory();
    }

    private static SessionHistoryItem BuildHistoryItem(CodexSessionDocument session) => new SessionHistoryItem
    {
        Id = session.Id,
        ThreadId = session.ThreadId,
        Title = DeriveSessionTitle(session),
        Preview = DeriveSessionPreview(session),
        Updated = session.Updated,
        MessageCount = session.Messages?.Count ?? 0
    };

    private static string DeriveSessionTitle(CodexSessionDocument session)
    {
        if (!string.IsNullOrWhiteSpace(session.Title))
        {
            return CompactLine(session.Title, 90);
        }

        var firstUserMessage = session.Messages?
            .FirstOrDefault(message => message.Role == CodexMessageRole.User && !string.IsNullOrWhiteSpace(message.Content))
            ?.Content;
        if (!string.IsNullOrWhiteSpace(firstUserMessage))
        {
            return CompactLine(firstUserMessage, 90);
        }

        return "VSCodex thread " + session.Created.ToLocalTime().ToString("g");
    }

    private static string DeriveSessionPreview(CodexSessionDocument session)
    {
        var message = session.Messages?
            .LastOrDefault(item => !string.IsNullOrWhiteSpace(item.Content))
            ?.Content;
        return string.IsNullOrWhiteSpace(message) ? "No messages saved" : CompactLine(message, 180);
    }

    private static string CompactLine(string? value, int maxLength)
    {
        var text = string.Join(" ", (value ?? string.Empty).Split(new[] { '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, Math.Max(0, maxLength - 1)).TrimEnd() + "...";
    }

    private static bool Contains(string? value, string query) => (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    public void AttachFiles(IEnumerable<string> fileNames)
    {
        var count = 0;
        foreach (var file in fileNames ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) continue;
            Attachments.Add(new CodexAttachment { Path = file, Kind = InferAttachmentKind(file) });
            count++;
        }
        if (count > 0) Status = $"Attached {count} file(s)";
    }

    public void InsertFileReferencePaths(IEnumerable<string> fileNames)
    {
        var tokens = (fileNames ?? Enumerable.Empty<string>())
            .Where(File.Exists)
            .Select(file => _workspace.SearchFiles(file, 1).FirstOrDefault()?.ReferenceKey ?? FormatPromptFileReference(file))
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (tokens.Count == 0)
        {
            return;
        }

        var prompt = Prompt ?? string.Empty;
        Prompt = string.IsNullOrWhiteSpace(prompt)
            ? string.Join(" ", tokens) + " "
            : prompt.TrimEnd() + " " + string.Join(" ", tokens) + " ";
        ClosePromptSuggestions();
        Status = $"Referenced {tokens.Count} file(s)";
    }

    public void AttachClipboardImage(BitmapSource image)
    {
        if (image == null) return;
        var path = Path.Combine(LocalPaths.AttachmentsRoot, "clipboard-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
        using (var stream = File.Create(path))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
        }
        Attachments.Add(new CodexAttachment { Path = path, Kind = "image" });
        Status = "Attached clipboard image";
    }

    private string ExpandAssistantSlashCommand(string value)
    {
        var prompt = value ?? string.Empty;
        if (prompt.StartsWith("/debug", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildDebugPrompt();
        if (prompt.StartsWith("/test", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildTestPrompt();
        if (prompt.StartsWith("/plan", StringComparison.OrdinalIgnoreCase))
        {
            var goal = prompt.Substring(Math.Min(5, prompt.Length)).Trim();
            RefreshAgentPlanPreview(goal);
            return _assistantContext.BuildPlanPrompt(goal, BuildAgentSummary());
        }
        if (prompt.StartsWith("/explain", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildExplainPrompt();
        if (prompt.StartsWith("/fix", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildFixPrompt();
        if (prompt.StartsWith("/review", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildReviewPrompt();
        if (prompt.StartsWith("/optimize", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildOptimizePrompt();
        if (prompt.StartsWith("/docs", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildDocumentationPrompt();
        return prompt;
    }

    private IEnumerable<AgentRoleDefinition> ApplyModelSelection(IEnumerable<AgentRoleDefinition> agents)
    {
        foreach (var source in DistinctAgentRoles(agents))
        {
            var agent = CloneAgentRole(source);
            if (BudgetDrivenModelSelection || agent.ModelSelectionMode == AgentModelSelectionMode.BudgetDriven)
            {
                agent.Model = BudgetModel;
            }

            yield return agent;
        }
    }

    private static IEnumerable<AgentRoleDefinition> DistinctAgentRoles(IEnumerable<AgentRoleDefinition> agents)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in agents ?? Enumerable.Empty<AgentRoleDefinition>())
        {
            var key = string.IsNullOrWhiteSpace(agent.Name) ? agent.Role : agent.Name;
            key = (key ?? string.Empty).Trim();
            if (key.Length == 0 || !seen.Add(key))
            {
                continue;
            }

            yield return agent;
        }
    }

    private static AgentRoleDefinition CloneAgentRole(AgentRoleDefinition source)
    {
        return new AgentRoleDefinition
        {
            Name = source.Name,
            Role = source.Role,
            Instructions = source.Instructions,
            Model = source.Model,
            ModelSelectionMode = source.ModelSelectionMode,
            IsEnabled = source.IsEnabled
        };
    }

    private static List<AgentRoleDefinition> DefaultAgentRoles() => new ExtensionSettings().AgentRoles.Select(CloneAgentRole).ToList();

    private static string PickAgentName(IReadOnlyList<AgentRoleDefinition> agents, string preferredName, int fallbackIndex)
    {
        if (agents.Count == 0)
        {
            return preferredName;
        }

        var agent = agents.FirstOrDefault(x => x.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase))
            ?? agents.FirstOrDefault(x => x.Role.IndexOf(preferredName, StringComparison.OrdinalIgnoreCase) >= 0)
            ?? agents[Math.Abs(fallbackIndex) % agents.Count];
        return string.IsNullOrWhiteSpace(agent.Name) ? preferredName : agent.Name;
    }

    private string EffectiveMainModel() => BudgetDrivenModelSelection && !string.IsNullOrWhiteSpace(BudgetModel) ? BudgetModel : SelectedModel;
    private string EffectiveOrchestrationModel() => BudgetDrivenModelSelection && !string.IsNullOrWhiteSpace(BudgetModel) ? BudgetModel : OrchestrationModel;

    private static CodexAccessLevel AccessLevelFromSandbox(SandboxMode sandbox)
    {
        if (sandbox == SandboxMode.DangerFullAccess) return CodexAccessLevel.FullAccess;
        if (sandbox == SandboxMode.ReadOnly) return CodexAccessLevel.ReadOnly;
        return CodexAccessLevel.Workspace;
    }

    private static SandboxMode SandboxFromAccessLevel(CodexAccessLevel accessLevel)
    {
        if (accessLevel == CodexAccessLevel.FullAccess) return SandboxMode.DangerFullAccess;
        if (accessLevel == CodexAccessLevel.ReadOnly) return SandboxMode.ReadOnly;
        return SandboxMode.WorkspaceWrite;
    }

    private static bool IsValidSkillName(string? name)
    {
        var value = (name ?? string.Empty).Trim();
        return value.Length > 0
            && char.IsLetterOrDigit(value[0])
            && value.All(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-');
    }

    private static string FormatTokenCount(int tokens)
    {
        if (tokens >= 1000000)
        {
            return (tokens / 1000000d).ToString("0.#M");
        }

        if (tokens >= 1000)
        {
            return (tokens / 1000d).ToString("0.#k");
        }

        return tokens.ToString();
    }

    private static void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private string BuildAgentSummary()
    {
        var sb = new StringBuilder();
        foreach (var agent in DistinctAgentRoles(AgentRoles.Where(x => x.IsEnabled))) sb.AppendLine($"- {agent.Name} ({agent.Role}) model={agent.Model}; mode={agent.ModelSelectionMode}: {agent.Instructions}");
        return sb.ToString();
    }

    private void CreateAgentPlanPrompt()
    {
        var goal = string.IsNullOrWhiteSpace(Prompt)
            ? "Plan the selected coding task from current Visual Studio solution context."
            : Prompt;
        RefreshAgentPlanPreview(goal);
        Prompt = _assistantContext.BuildPlanPrompt(goal, BuildAgentSummary());
        Mode = CodexRunMode.Plan;
        SelectedToolTabIndex = AgentsToolTabIndex;
        IsToolPanelOpen = true;
        Status = "Prepared VSCodex agent plan";
    }

    private void RefreshAgentPlanPreview(string goal)
    {
        var agents = DistinctAgentRoles(AgentRoles.Where(x => x.IsEnabled)).ToList();
        if (agents.Count == 0)
        {
            agents = DefaultAgentRoles();
        }

        var sections = new[]
        {
            new { Preferred = "Planner", Title = "Clarify goal and acceptance criteria", Description = "Use the current Visual Studio solution, selected code, references, memories, and MCP tools to define the work." },
            new { Preferred = "Architect", Title = "Assess architecture and integration risks", Description = "Identify affected projects, services, UI surfaces, threading risks, and compatibility constraints before editing." },
            new { Preferred = "Builder", Title = "Implement focused changes", Description = "Apply the smallest coherent code changes needed for the requested outcome." },
            new { Preferred = "Reviewer", Title = "Review behavior, UX, and safety", Description = "Check correctness, regressions, user-visible behavior, and missing coverage." },
            new { Preferred = "Verifier", Title = "Validate in Visual Studio and command-line tests", Description = "Run the relevant build, test, VSIX, and interactive Visual Studio checks, then summarize evidence." }
        }
        .Select((section, index) => new OrchestrationTaskSection
        {
            Index = index + 1,
            Title = section.Title,
            Description = section.Description + Environment.NewLine + "Goal: " + (string.IsNullOrWhiteSpace(goal) ? "Use current context." : goal),
            AssignedAgent = PickAgentName(agents, section.Preferred, index),
            DependsOnSectionId = index == 0 ? string.Empty : "previous"
        })
        .ToList();

        Replace(OrchestrationSections, sections);
    }

    private void CopyMessageToClipboard(ChatMessage? message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content))
        {
            Status = "No message content to copy";
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(message.Content);
            Status = "Copied VSCodex message";
        }
        catch (Exception ex)
        {
            Status = "Could not copy message: " + ex.Message;
        }
    }

    private void UseMessageAsPrompt(ChatMessage? message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content))
        {
            Status = "No message content to use";
            return;
        }

        Prompt = message.Content;
        Status = message.Role == CodexMessageRole.User ? "Copied user prompt back to input" : "Copied message back to input";
    }

    private void OnCodexEvent(CodexEvent ev)
    {
        RunOnUiThread(() =>
        {
            UpdateRateLimitsFromJson(ev.RawJson);
            if (ev.Type == "stdout" || ev.Type == "message") AddMessage(CodexMessageRole.Assistant, ev.Message);
            else if (ev.Type == "fallback" || ev.Type == "stderr" || ev.Type == "bridge-output") AddMessage(CodexMessageRole.System, $"[{ev.Type}] {ev.Message}");
            else if (ev.Type == "progress") SetRunProgress(ev.Message);
            else Status = ev.Message;
        });
    }

    private void OnOrchestrationEvent(OrchestrationEvent ev)
    {
        RunOnUiThread(() =>
        {
            Status = ev.Message;
            if (ev.Type == "plan-created" && _taskOrchestrator.CurrentPlan != null) Replace(OrchestrationSections, _taskOrchestrator.CurrentPlan.Sections);
            if (ev.Section != null && !OrchestrationSections.Any(x => x.Id == ev.Section.Id)) OrchestrationSections.Add(ev.Section);
            AddMessage(CodexMessageRole.System, $"[orchestration:{ev.Type}] {ev.Message}");
        });
    }

    private ChatMessage AddMessage(CodexMessageRole role, string content)
    {
        var message = new ChatMessage { Role = role, Content = content ?? string.Empty };
        RunOnUiThread(() =>
        {
            Messages.Add(message);
            _session.Messages.Add(message);
            _session.Updated = message.Timestamp;
            if (role == CodexMessageRole.User && string.IsNullOrWhiteSpace(_session.Title))
            {
                _session.Title = CompactLine(content, 90);
            }
        });
        return message;
    }

    private IDisposable StartRunProgress(string stage)
    {
        _activeRunStartedAt = DateTimeOffset.Now;
        _activeRunStage = stage;
        _activeProgressMessage = AddMessage(CodexMessageRole.System, BuildRunProgressMessage(stage));
        return Observable.Interval(TimeSpan.FromSeconds(15), _uiScheduler)
            .Subscribe(_ => RefreshRunProgress());
    }

    private void SetRunProgress(string stage)
    {
        RunOnUiThread(() =>
        {
            _activeRunStage = string.IsNullOrWhiteSpace(stage) ? _activeRunStage : stage;
            Status = _activeRunStage;
            RefreshRunProgress();
        });
    }

    private void RefreshRunProgress()
    {
        if (_activeProgressMessage == null || !IsRunning)
        {
            return;
        }

        _activeProgressMessage.Content = BuildRunProgressMessage(_activeRunStage);
    }

    private void FinishRunProgress(string stage)
    {
        RunOnUiThread(() =>
        {
            if (_activeProgressMessage != null)
            {
                _activeProgressMessage.Content = BuildRunProgressMessage(stage);
            }

            _activeProgressMessage = null;
            _activeRunStage = string.Empty;
        });
    }

    private string BuildRunProgressMessage(string stage)
    {
        var elapsed = _activeRunStartedAt == default ? TimeSpan.Zero : DateTimeOffset.Now - _activeRunStartedAt;
        var workspace = string.IsNullOrWhiteSpace(_workspace.CurrentWorkspaceRoot) ? "Waiting for Visual Studio workspace" : _workspace.CurrentWorkspaceRoot;
        return "**VSCodex is working**"
            + Environment.NewLine
            + Environment.NewLine
            + "- Status: " + (string.IsNullOrWhiteSpace(stage) ? "Preparing request" : stage)
            + Environment.NewLine
            + "- Elapsed: " + FormatElapsed(elapsed)
            + Environment.NewLine
            + "- Workspace: " + workspace;
    }

    private void UpdateSkills(IReadOnlyList<SkillDefinition> items)
    {
        var enabledPaths = new HashSet<string>(_settingsStore.Current.EnabledSkillPaths ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            item.IsEnabled = enabledPaths.Contains(item.MarkdownPath);
        }

        Replace(Skills, items);
    }
    private void UpdateMemories(IReadOnlyList<MemoryEntry> items) => Replace(Memories, items);
    private void UpdateMcpServers(IReadOnlyList<McpServerDefinition> items) => Replace(McpServers, items);
    private void UpdateReferenceSuggestions(string prompt)
    {
        var token = LastReferenceToken(prompt);
        Replace(FileSuggestions, _workspace.SearchFiles(token != null && token.StartsWith("@", StringComparison.Ordinal) ? token : string.Empty, 16));
        Replace(ContextSuggestions, _workspace.SearchContextReferences(token != null && token.StartsWith("#", StringComparison.Ordinal) ? token : string.Empty, 12));
    }

    private void UpdatePromptSuggestions(string prompt)
    {
        var token = LastPromptToken(prompt);
        if (string.IsNullOrWhiteSpace(token))
        {
            Replace(PromptSuggestions, Array.Empty<PromptSuggestionItem>());
            SelectedPromptSuggestion = null;
            IsPromptSuggestionOpen = false;
            return;
        }

        IReadOnlyList<PromptSuggestionItem> suggestions;
        var activeToken = token!;
        if (activeToken.StartsWith("@", StringComparison.Ordinal))
        {
            var fileSuggestions = _workspace.SearchFiles(activeToken, 24)
                .Select(x => new PromptSuggestionItem { Kind = "File", DisplayText = x.ReferenceKey, Detail = x.RelativePath, InsertText = x.ReferenceKey + " " })
                .ToList();
            var browseSuggestion = new PromptSuggestionItem
            {
                Kind = "Disk",
                DisplayText = "Browse files...",
                Detail = "Choose one or more files from the repository or elsewhere on disk",
                TargetTab = "browse-files"
            };
            suggestions = fileSuggestions.Count > 0
                ? fileSuggestions.Concat(new[] { browseSuggestion }).ToList()
                : new[] { browseSuggestion };
        }
        else if (activeToken.StartsWith("#", StringComparison.Ordinal))
        {
            suggestions = ContextSuggestions
                .Select(x => new PromptSuggestionItem { Kind = x.ReferenceKind == "selection" ? "Selected code" : "Reference", DisplayText = x.ReferenceKey, Detail = x.ReferenceKind == "selection" ? $"{x.RelativePath} lines {x.StartLine}-{x.EndLine}" : x.RelativePath, InsertText = x.ReferenceKey + " " })
                .ToList();
        }
        else if (activeToken.StartsWith("/", StringComparison.Ordinal))
        {
            suggestions = BuildSlashCommandSuggestions(activeToken).ToList();
        }
        else
        {
            suggestions = Array.Empty<PromptSuggestionItem>();
        }

        Replace(PromptSuggestions, suggestions);
        SelectedPromptSuggestion = suggestions.FirstOrDefault();
        IsPromptSuggestionOpen = suggestions.Count > 0;
    }

    public void InsertPromptSuggestion(PromptSuggestionItem? suggestion)
    {
        if (suggestion == null || string.IsNullOrWhiteSpace(suggestion.InsertText))
        {
            return;
        }

        var prompt = Prompt ?? string.Empty;
        var tokenStart = LastPromptTokenStart(prompt);
        Prompt = tokenStart >= 0
            ? prompt.Substring(0, tokenStart) + suggestion.InsertText
            : (string.IsNullOrWhiteSpace(prompt) ? suggestion.InsertText : prompt.TrimEnd() + " " + suggestion.InsertText);
        IsPromptSuggestionOpen = false;
        Status = "Inserted " + suggestion.DisplayText;
    }

    public void ClosePromptSuggestions()
    {
        IsPromptSuggestionOpen = false;
    }

    private IEnumerable<PromptSuggestionItem> BuildSlashCommandSuggestions(string token)
    {
        var query = token.TrimStart('/').Trim();
        return SlashCommandSuggestions()
            .Where(x => string.IsNullOrWhiteSpace(query) || x.DisplayText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || x.Detail.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(12);
    }

    private IEnumerable<PromptSuggestionItem> SlashCommandSuggestions()
    {
        yield return new PromptSuggestionItem { Kind = "Action", DisplayText = "/explain", Detail = "Explain selected code or active editor context", InsertText = "/explain " };
        yield return new PromptSuggestionItem { Kind = "Action", DisplayText = "/fix", Detail = "Fix selected code with the smallest safe change", InsertText = "/fix " };
        yield return new PromptSuggestionItem { Kind = "Action", DisplayText = "/review", Detail = "Review selected code for bugs and risks", InsertText = "/review " };
        yield return new PromptSuggestionItem { Kind = "Action", DisplayText = "/optimize", Detail = "Optimize selected code without changing behavior", InsertText = "/optimize " };
        yield return new PromptSuggestionItem { Kind = "Action", DisplayText = "/docs", Detail = "Generate or improve comments and documentation", InsertText = "/docs " };
        yield return new PromptSuggestionItem { Kind = "Action", DisplayText = "/test", Detail = "Create focused tests for selected code", InsertText = "/test " };
        yield return new PromptSuggestionItem { Kind = "Debug", DisplayText = "/debug", Detail = "Debug current exception, break mode, stack, or selected code", InsertText = "/debug " };
        yield return new PromptSuggestionItem { Kind = "Plan", DisplayText = "/plan", Detail = "Create an agent-oriented implementation plan", InsertText = "/plan " };
        yield return new PromptSuggestionItem { Kind = "History", DisplayText = "/history", Detail = "Open saved VSCodex conversation history", InsertText = "/history " };
        yield return new PromptSuggestionItem { Kind = "Tools", DisplayText = "/mcp", Detail = "Open VSCodex MCP server and tool selection", InsertText = "/mcp " };
        yield return new PromptSuggestionItem { Kind = "Options", DisplayText = "/settings", Detail = "Use Tools > Options > VSCodex for model, sandbox, and runtime settings", InsertText = "/settings " };
        yield return new PromptSuggestionItem { Kind = "Context", DisplayText = "/context", Detail = "Open selected-code and repository file context", InsertText = "/context " };
        yield return new PromptSuggestionItem { Kind = "Tools", DisplayText = "/analytics", Detail = "Open model cost and complexity analytics", InsertText = "/analytics " };
        yield return new PromptSuggestionItem { Kind = "Tools", DisplayText = "/memory", Detail = "Open ReactiveMemory controls and saved context", InsertText = "/memory " };
        yield return new PromptSuggestionItem { Kind = "Tools", DisplayText = "/agents", Detail = "Open multi-agent roles and orchestration controls", InsertText = "/agents " };
        yield return new PromptSuggestionItem { Kind = "Tools", DisplayText = "/skills", Detail = "Open Codex skills controls", InsertText = "/skills " };
        yield return new PromptSuggestionItem { Kind = "Files", DisplayText = "/attachments", Detail = "Open prompt attachments", InsertText = "/attachments " };
    }

    private void SaveInputAreaHeight(double value)
    {
        var settings = _settingsStore.Current;
        if (Math.Abs(settings.DefaultInputAreaHeight - value) < 0.1d) return;
        settings.DefaultInputAreaHeight = value;
        SaveSettingsForCurrentWorkspace(settings);
    }

    private void ScheduleModelSettingsSave(bool refreshAnalytics)
    {
        var settings = CaptureModelSettingsSnapshot();
        var workspaceIdentity = CloneWorkspaceIdentity(_workspace.CurrentWorkspaceIdentity);
        var revision = Interlocked.Increment(ref _modelSettingsSaveRevision);
        var cancellation = new CancellationTokenSource();
        CancellationTokenSource? previous;
        lock (_modelSettingsSaveGate)
        {
            previous = _modelSettingsSaveCancellation;
            _modelSettingsSaveCancellation = cancellation;
            _hasPendingModelSettingsSave = true;
        }

        previous?.Cancel();

        _joinableTaskFactory.RunAsync(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(ModelSettingsSaveDebounceMilliseconds), cancellation.Token).ConfigureAwait(false);
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
            catch (Exception ex)
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                Status = "Could not save VSCodex settings: " + ex.Message;
            }
            finally
            {
                CompleteModelSettingsSave(cancellation);
            }
        }).Task.FireAndForget();
    }

    private void CompleteModelSettingsSave(CancellationTokenSource cancellation)
    {
        lock (_modelSettingsSaveGate)
        {
            if (ReferenceEquals(_modelSettingsSaveCancellation, cancellation))
            {
                _modelSettingsSaveCancellation = null;
                _hasPendingModelSettingsSave = false;
            }
        }

        cancellation.Dispose();
    }

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
        catch (Exception ex)
        {
            Debug.WriteLine("Could not flush VSCodex settings: " + ex);
        }
    }

    private void SaveModelSettings()
    {
        SaveSettingsForWorkspace(CloneWorkspaceIdentity(_workspace.CurrentWorkspaceIdentity), CaptureModelSettingsSnapshot());
    }

    private ExtensionSettings CaptureModelSettingsSnapshot()
    {
        var settings = CloneSettings(_settingsStore.Current);
        settings.DefaultModel = string.IsNullOrWhiteSpace(SelectedModel) ? settings.DefaultModel : SelectedModel;
        settings.DefaultFailoverModel = string.IsNullOrWhiteSpace(FailoverModel) ? settings.DefaultFailoverModel : FailoverModel;
        settings.DefaultReasoningEffort = string.IsNullOrWhiteSpace(SelectedReasoning) ? settings.DefaultReasoningEffort : SelectedReasoning;
        settings.DefaultVerbosity = string.IsNullOrWhiteSpace(SelectedVerbosity) ? settings.DefaultVerbosity : SelectedVerbosity;
        settings.DefaultApprovalPolicy = ApprovalPolicy;
        settings.DefaultSandboxMode = SandboxMode;
        settings.DefaultOrchestrationModel = string.IsNullOrWhiteSpace(OrchestrationModel) ? settings.DefaultModel : OrchestrationModel;
        settings.DefaultBudgetDrivenModelSelection = BudgetDrivenModelSelection;
        settings.DefaultBudgetModel = string.IsNullOrWhiteSpace(BudgetModel) ? settings.DefaultBudgetModel : BudgetModel;
        EnsureModelOption(settings.CustomModels, settings.DefaultModel);
        EnsureModelOption(settings.CustomModels, settings.DefaultFailoverModel);
        EnsureModelOption(settings.CustomModels, settings.DefaultOrchestrationModel);
        EnsureModelOption(settings.CustomModels, settings.DefaultBudgetModel);
        return settings;
    }

    private static ExtensionSettings CloneSettings(ExtensionSettings source)
    {
        return new ExtensionSettings
        {
            CodexCliPath = source.CodexCliPath,
            NodePath = source.NodePath,
            BridgeScriptPath = source.BridgeScriptPath,
            DefaultModel = source.DefaultModel,
            DefaultFailoverModel = source.DefaultFailoverModel,
            DefaultReasoningEffort = source.DefaultReasoningEffort,
            DefaultVerbosity = source.DefaultVerbosity,
            DefaultServiceTier = source.DefaultServiceTier,
            DefaultProfile = source.DefaultProfile,
            DefaultApprovalPolicy = source.DefaultApprovalPolicy,
            DefaultSandboxMode = source.DefaultSandboxMode,
            CustomModels = source.CustomModels?.ToList() ?? new List<string>(),
            CustomReasoningEfforts = source.CustomReasoningEfforts?.ToList() ?? new List<string>(),
            CustomVerbosityOptions = source.CustomVerbosityOptions?.ToList() ?? new List<string>(),
            SkillRoots = source.SkillRoots?.ToList() ?? new List<string>(),
            EnabledSkillPaths = source.EnabledSkillPaths?.ToList() ?? new List<string>(),
            DefaultUseMultiAgentOrchestration = source.DefaultUseMultiAgentOrchestration,
            DefaultMaxAgentConcurrency = source.DefaultMaxAgentConcurrency,
            DefaultAgentStrategy = source.DefaultAgentStrategy,
            DefaultOrchestrationModel = source.DefaultOrchestrationModel,
            DefaultBudgetDrivenModelSelection = source.DefaultBudgetDrivenModelSelection,
            DefaultBudgetModel = source.DefaultBudgetModel,
            DefaultInputAreaHeight = source.DefaultInputAreaHeight,
            AgentRoles = DistinctAgentRoles(source.AgentRoles ?? new List<AgentRoleDefinition>()).Select(CloneAgentRole).ToList()
        };
    }

    private static WorkspaceIdentity? CloneWorkspaceIdentity(WorkspaceIdentity? source)
    {
        if (source == null)
        {
            return null;
        }

        return new WorkspaceIdentity
        {
            Id = source.Id,
            Name = source.Name,
            RootPath = source.RootPath,
            SolutionPath = source.SolutionPath,
            SolutionRelativePath = source.SolutionRelativePath,
            RepositoryRemote = source.RepositoryRemote,
            MemoryRoot = source.MemoryRoot
        };
    }

    private static void EnsureModelOption(List<string> models, string model)
    {
        if (string.IsNullOrWhiteSpace(model) || models.Any(x => x.Equals(model, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        models.Add(model.Trim());
    }

    private void SaveSettingsForCurrentWorkspace(ExtensionSettings settings)
    {
        SaveSettingsForWorkspace(CloneWorkspaceIdentity(_workspace.CurrentWorkspaceIdentity), settings);
    }

    private void SaveSettingsForWorkspace(WorkspaceIdentity? identity, ExtensionSettings settings)
    {
        if (identity != null && !string.IsNullOrWhiteSpace(identity.Id))
        {
            _settingsStore.SaveForWorkspace(identity, settings);
            return;
        }

        _settingsStore.Save(settings);
    }

    private void ApplyRecommendedModel()
    {
        var recommended = ModelEstimate.RecommendedModel;
        if (string.IsNullOrWhiteSpace(recommended)) return;
        if (recommended.Equals(BudgetModel, StringComparison.OrdinalIgnoreCase))
        {
            BudgetDrivenModelSelection = true;
        }
        else
        {
            SelectedModel = recommended;
            BudgetDrivenModelSelection = false;
        }
        Status = "Applied model recommendation: " + recommended;
    }

    private void UpdateAnalytics(string prompt)
    {
        try
        {
            var request = new CodexRunRequest
            {
                Prompt = prompt ?? string.Empty,
                ThreadId = ThreadId,
                WorkspaceRoot = _workspace.CurrentWorkspaceRoot,
                WorkspaceName = _workspace.CurrentWorkspaceName,
                WorkspaceSolutionPath = _workspace.CurrentSolutionPath,
                WorkspaceMemoryRoot = _workspace.CurrentWorkspaceMemoryRoot,
                WorkspaceIdentity = _workspace.CurrentWorkspaceIdentity,
                Options = new CodexRunOptions
                {
                    Mode = Mode,
                    Model = EffectiveMainModel(),
                    FailoverModel = FailoverModel,
                    ReasoningEffort = SelectedReasoning,
                    Verbosity = SelectedVerbosity,
                    ApprovalPolicy = ApprovalPolicy,
                    SandboxMode = SandboxMode,
                    Transport = Transport,
                    OrchestrationModel = EffectiveOrchestrationModel(),
                    BudgetDrivenModelSelection = BudgetDrivenModelSelection,
                    BudgetModel = BudgetModel
                },
                Attachments = Attachments.ToList(),
                Skills = Skills.Where(x => x.IsEnabled).ToList(),
                Memories = _memoryStore.Search(prompt ?? string.Empty, 10),
                McpServers = McpServers.Where(x => x.IsEnabled).ToList(),
                WorkspaceFiles = _workspace.ResolveMentions(prompt ?? string.Empty, 12000)
                    .Concat(_workspace.ResolveHashReferences(prompt ?? string.Empty, 12000))
                    .ToList(),
                AgentRoles = AgentRoles.Where(x => x.IsEnabled).ToList()
            };
            ModelEstimate = _modelAnalytics.Estimate(request);
            this.RaisePropertyChanged(nameof(AnalyticsSummary));
            this.RaisePropertyChanged(nameof(AnalyticsRecommendation));
        }
        catch
        {
            // Analytics must never block prompt editing in the tool window.
        }
    }

    private static string? LastReferenceToken(string prompt) => (prompt ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault(x => x.StartsWith("@", StringComparison.Ordinal) || x.StartsWith("#", StringComparison.Ordinal));
    private static string? LastPromptToken(string prompt)
    {
        var start = LastPromptTokenStart(prompt);
        return start < 0 ? null : prompt.Substring(start);
    }

    private static int LastPromptTokenStart(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return -1;
        }

        if (char.IsWhiteSpace(prompt[prompt.Length - 1]))
        {
            return -1;
        }

        var trimmedEnd = prompt.TrimEnd();
        var index = trimmedEnd.Length - 1;
        while (index >= 0 && !char.IsWhiteSpace(trimmedEnd[index]))
        {
            index--;
        }

        var start = index + 1;
        if (start >= trimmedEnd.Length)
        {
            return -1;
        }

        var marker = trimmedEnd[start];
        return marker == '@' || marker == '#' || marker == '/' ? start : -1;
    }

    private static bool IsMcpDiscoveryPrompt(string prompt) => (prompt ?? string.Empty).Trim().StartsWith("/MCP", StringComparison.OrdinalIgnoreCase);
    private static double ClampInputHeight(double value) => Math.Max(32d, Math.Min(600d, value <= 0d ? 180d : value));
    private static string FormatElapsed(TimeSpan elapsed) => elapsed.TotalHours >= 1d ? elapsed.ToString(@"h\:mm\:ss") : elapsed.ToString(@"m\:ss");
    private static IReadOnlyList<RateLimitWindowStatus> BuildDefaultRateLimits()
    {
        return new[]
        {
            new RateLimitWindowStatus { Label = "5h", Remaining = "Waiting for Codex telemetry", UsagePercent = 0, ResetText = string.Empty },
            new RateLimitWindowStatus { Label = "Weekly", Remaining = "Waiting for Codex telemetry", UsagePercent = 0, ResetText = string.Empty }
        };
    }

    private void UpdateRateLimitsFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var root = JToken.Parse(json);
            var limits = FindRateLimitToken(root);
            var changed = false;
            if (limits != null)
            {
                changed = UpdateRateLimitFromToken("5h", SelectFirstToken(limits, "primary", "fiveHour", "five_hour", "5h", "hourly", "hour", "requests.primary", "requests.fiveHour", "requests.five_hour", "requests.hourly", "requests.hour"));
                changed |= UpdateRateLimitFromToken("Weekly", SelectFirstToken(limits, "secondary", "weekly", "week", "requests.secondary", "requests.weekly", "requests.week"));
            }

            if (changed)
            {
                RateLimitUpdatedAt = "Codex telemetry " + DateTimeOffset.Now.ToString("HH:mm");
            }
        }
        catch
        {
            // Rate-limit telemetry is optional and must never interrupt streaming output.
        }
    }

    private bool UpdateRateLimitFromToken(string label, JToken? token)
    {
        if (token == null)
        {
            return false;
        }

        var status = RateLimits.FirstOrDefault(x => string.Equals(x.Label, label, StringComparison.OrdinalIgnoreCase));
        if (status == null)
        {
            return false;
        }

        var updated = false;
        var remainingPercent = TokenInt(token, "remaining_percent", "remainingPercent", "remaining_pct", "remainingPct");
        var usedPercent = TokenInt(token, "used_percent", "usedPercent", "usage_percent", "usagePercent");
        if (!remainingPercent.HasValue && usedPercent.HasValue)
        {
            remainingPercent = 100 - usedPercent.Value;
        }

        var remaining = TokenString(token, "remaining", "remainingText", "remaining_text", "available");
        var limit = TokenString(token, "limit", "total", "quota");
        if (remainingPercent.HasValue)
        {
            var percent = ClampPercent(remainingPercent.Value);
            status.Remaining = percent + "%";
            status.UsagePercent = percent;
            updated = true;
        }
        else if (!string.IsNullOrWhiteSpace(remaining))
        {
            status.Remaining = string.IsNullOrWhiteSpace(limit) ? remaining! : remaining + " / " + limit;
            updated = true;
        }

        var remainingValue = TokenInt(token, "remaining", "available");
        var limitValue = TokenInt(token, "limit", "total", "quota");
        if (!remainingPercent.HasValue && remainingValue.HasValue && limitValue.HasValue && limitValue.Value > 0)
        {
            var percent = ClampPercent((int)Math.Round((double)remainingValue.Value / limitValue.Value * 100d));
            status.UsagePercent = percent;
            if (string.IsNullOrWhiteSpace(remaining))
            {
                status.Remaining = percent + "%";
            }

            updated = true;
        }

        var resetAt = TokenResetAt(token);
        if (resetAt.HasValue)
        {
            status.ResetText = FormatRateLimitReset(label, resetAt.Value);
            updated = true;
        }
        else
        {
            var reset = TokenString(token, "reset", "resetText", "reset_text", "resets");
            if (!string.IsNullOrWhiteSpace(reset))
            {
                status.ResetText = reset!;
                updated = true;
            }
        }

        return updated;
    }

    private static JToken? FindRateLimitToken(JToken root)
    {
        var candidates = new[]
        {
            root,
            root.SelectToken("rateLimits", false),
            root.SelectToken("rate_limits", false),
            root.SelectToken("result.rateLimits", false),
            root.SelectToken("result.rate_limits", false),
            root.SelectToken("result.rateLimits.rate_limits", false),
            root.SelectToken("result.rateLimits.rateLimits", false),
            root.SelectToken("rateLimitsByLimitId.codex", false),
            root.SelectToken("result.rateLimitsByLimitId.codex", false),
            root.SelectToken("result.result.rateLimits", false),
            root.SelectToken("result.result.rate_limits", false),
            root.SelectToken("result.result.rateLimitsByLimitId.codex", false),
            root.SelectToken("usage.rateLimits", false),
            root.SelectToken("usage.rate_limits", false),
            root.SelectToken("result.usage.rateLimits", false),
            root.SelectToken("result.usage.rate_limits", false)
        };

        foreach (var candidate in candidates)
        {
            var unwrapped = UnwrapRateLimitToken(candidate);
            if (LooksLikeRateLimitToken(unwrapped))
            {
                return unwrapped;
            }
        }

        foreach (var candidate in root.SelectTokens("$..rate_limits").Concat(root.SelectTokens("$..rateLimits")))
        {
            var unwrapped = UnwrapRateLimitToken(candidate);
            if (LooksLikeRateLimitToken(unwrapped))
            {
                return unwrapped;
            }
        }

        return null;
    }

    private static JToken? UnwrapRateLimitToken(JToken? token)
    {
        if (token == null)
        {
            return null;
        }

        return SelectFirstToken(token, "rate_limits", "rateLimits") ?? token;
    }

    private static bool LooksLikeRateLimitToken(JToken? token)
    {
        return SelectFirstToken(token, "primary", "secondary", "fiveHour", "five_hour", "weekly", "week", "requests.primary", "requests.secondary") != null;
    }

    private static JToken? SelectFirstToken(JToken? token, params string[] paths)
    {
        if (token == null)
        {
            return null;
        }

        foreach (var path in paths)
        {
            var value = token.SelectToken(path, false);
            if (value != null && value.Type != JTokenType.Null)
            {
                return value;
            }
        }

        return null;
    }

    private static DateTimeOffset? TokenResetAt(JToken token)
    {
        var absolute = TokenLong(token, "reset_at", "resetAt", "resets_at", "resetsAt");
        if (absolute.HasValue)
        {
            if (absolute.Value > 100000000000)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(absolute.Value).ToLocalTime();
            }

            if (absolute.Value > 1000000000)
            {
                return DateTimeOffset.FromUnixTimeSeconds(absolute.Value).ToLocalTime();
            }
        }

        var relative = TokenLong(token, "reset_after_seconds", "resetAfterSeconds", "resets_after_seconds", "resetsAfterSeconds");
        if (relative.HasValue)
        {
            return DateTimeOffset.Now.AddSeconds(relative.Value);
        }

        var text = TokenString(token, "reset", "resetAt", "reset_at", "resetsAt", "resets_at");
        if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out var parsed))
        {
            return parsed.ToLocalTime();
        }

        return null;
    }

    private static string FormatRateLimitReset(string label, DateTimeOffset resetAt)
    {
        var local = resetAt.ToLocalTime();
        return string.Equals(label, "Weekly", StringComparison.OrdinalIgnoreCase) ? local.ToString("d MMM") : local.ToString("HH:mm");
    }

    private static int ClampPercent(int value)
    {
        return Math.Max(0, Math.Min(100, value));
    }

    private static string? TokenString(JToken token, params string[] names)
    {
        foreach (var name in names)
        {
            var value = token[name];
            if (value != null && value.Type != JTokenType.Null)
            {
                return value.Value<string>() ?? value.ToString();
            }
        }

        return null;
    }

    private static int? TokenInt(JToken token, params string[] names)
    {
        var value = TokenString(token, names);
        return int.TryParse(value, out var parsed) ? parsed : (int?)null;
    }

    private static long? TokenLong(JToken token, params string[] names)
    {
        var value = TokenString(token, names);
        return long.TryParse(value, out var parsed) ? parsed : (long?)null;
    }

    private static McpToolInputField CloneField(McpToolInputField field) => new McpToolInputField { Name = field.Name, Type = field.Type, Description = field.Description, IsRequired = field.IsRequired, Value = field.Value };
    private static string InferAttachmentKind(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" }.Contains(ext)) return "image";
        if (new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md" }.Contains(ext)) return "document";
        return "file";
    }

    private static string FormatPromptFileReference(string path)
    {
        var value = path ?? string.Empty;
        if (value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
        {
            value = "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        return "@" + value;
    }

    private void RunOnUiThread(Action action)
    {
        if (_uiDispatcher.CheckAccess())
        {
            action();
            return;
        }

        _joinableTaskFactory.RunAsync(async () =>
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            action();
        }).Task.FireAndForget();
    }

    private void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        var snapshot = items.ToList();
        RunOnUiThread(() =>
        {
            target.Clear();
            foreach (var item in snapshot) target.Add(item);
        });
    }

    public void Dispose()
    {
        FlushPendingModelSettingsSave();
        _subscriptions.Dispose();
    }
    private sealed class CompositeDisposableLike : IDisposable { private readonly IDisposable[] _items; public CompositeDisposableLike(params IDisposable[] items) => _items = items; public void Dispose() { foreach (var item in _items) item.Dispose(); } }
}
