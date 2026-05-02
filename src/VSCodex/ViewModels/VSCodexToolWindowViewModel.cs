using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private readonly IWorkspaceContextService _workspace;
    private readonly ISessionStore _sessionStore;
    private readonly ICodexOrchestrator _codex;
    private readonly ITaskOrchestrationService _taskOrchestrator;
    private readonly ICodingAssistantContextService _assistantContext;
    private readonly IModelAnalyticsService _modelAnalytics;
    private readonly ICodexEnvironmentService _environment;
    private readonly JoinableTaskFactory _joinableTaskFactory;
    private readonly Dispatcher _uiDispatcher;
    private readonly IScheduler _uiScheduler;
    private readonly IDisposable _subscriptions;
    private readonly CodexSessionDocument _session;
    private int _promptChangeRevision;

    private string _prompt = string.Empty;
    private string _status = "Ready";
    private bool _isRunning;
    private bool _useMultiAgentOrchestration;
    private bool _budgetDrivenModelSelection;
    private bool _isSettingsPanelOpen;
    private int _maxAgentConcurrency = 1;
    private int _selectedToolTabIndex;
    private double _inputAreaHeight = 180d;
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
    private string _rateLimitUpdatedAt = "Waiting for Codex rate-limit telemetry";
    private string _codexSetupSummary = "Checking VSCodex prerequisites...";
    private string _codexSetupInstructions = string.Empty;
    private CodexEnvironmentReport? _lastEnvironmentReport;
    private ApprovalPolicy _approvalPolicy;
    private SandboxMode _sandboxMode;
    private CodexTransportKind _transport = CodexTransportKind.SdkBridge;
    private McpServerDefinition? _selectedMcpServer;
    private McpToolDefinition? _selectedMcpTool;
    private PromptSuggestionItem? _selectedPromptSuggestion;
    private bool _isPromptSuggestionOpen;
    private string? _threadId;

    public VSCodexToolWindowViewModel(
        ISettingsStore settingsStore,
        IMemoryStore memoryStore,
        ISkillIndexService skillIndex,
        IMcpConfigService mcpConfig,
        IMcpToolCatalogService mcpTools,
        IWorkspaceContextService workspace,
        ISessionStore sessionStore,
        ICodexOrchestrator codex,
        ITaskOrchestrationService taskOrchestrator,
        ICodingAssistantContextService assistantContext,
        IModelAnalyticsService modelAnalytics,
        ICodexEnvironmentService environment,
        JoinableTaskFactory joinableTaskFactory)
    {
        _settingsStore = settingsStore;
        _memoryStore = memoryStore;
        _skillIndex = skillIndex;
        _mcpConfig = mcpConfig;
        _mcpTools = mcpTools;
        _workspace = workspace;
        _sessionStore = sessionStore;
        _codex = codex;
        _taskOrchestrator = taskOrchestrator;
        _assistantContext = assistantContext;
        _modelAnalytics = modelAnalytics;
        _environment = environment;
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
        TransportOptions = new ObservableCollection<CodexTransportKind>((CodexTransportKind[])Enum.GetValues(typeof(CodexTransportKind)));
        AgentStrategyOptions = new ObservableCollection<AgentExecutionStrategy>((AgentExecutionStrategy[])Enum.GetValues(typeof(AgentExecutionStrategy)));
        AgentModelSelectionModeOptions = new ObservableCollection<AgentModelSelectionMode>((AgentModelSelectionMode[])Enum.GetValues(typeof(AgentModelSelectionMode)));

        var canRun = this.WhenAnyValue(x => x.Prompt, x => x.IsRunning, (p, r) => !string.IsNullOrWhiteSpace(p) && !r).ObserveOn(_uiScheduler);
        var canCancel = this.WhenAnyValue(x => x.IsRunning).ObserveOn(_uiScheduler);
        var canSavePrompt = this.WhenAnyValue(x => x.Prompt, p => !string.IsNullOrWhiteSpace(p)).ObserveOn(_uiScheduler);
        RunCommand = ReactiveCommand.CreateFromTask(RunAsync, canRun, _uiScheduler);
        CancelCommand = ReactiveCommand.Create(() => { _taskOrchestrator.Cancel(); _codex.Cancel(); }, canCancel, _uiScheduler);
        NewThreadCommand = ReactiveCommand.Create(StartNewThread, outputScheduler: _uiScheduler);
        ShowSettingsCommand = ReactiveCommand.Create(ShowSettings, this.WhenAnyValue(x => x.IsRunning, running => !running).ObserveOn(_uiScheduler), _uiScheduler);
        CheckPrerequisitesCommand = ReactiveCommand.CreateFromTask(CheckPrerequisitesAsync, null, _uiScheduler);
        RefreshCommand = ReactiveCommand.Create(Refresh, outputScheduler: _uiScheduler);
        RefreshAnalyticsCommand = ReactiveCommand.Create(() => UpdateAnalytics(Prompt), outputScheduler: _uiScheduler);
        ApplyRecommendedModelCommand = ReactiveCommand.Create(ApplyRecommendedModel, outputScheduler: _uiScheduler);
        AddUserMemoryCommand = ReactiveCommand.Create(() => AddMemory("user"), canSavePrompt, _uiScheduler);
        AddWorkspaceMemoryCommand = ReactiveCommand.Create(() => AddMemory("workspace"), canSavePrompt, _uiScheduler);
        AddImageAttachmentCommand = ReactiveCommand.Create(AddImageAttachment, outputScheduler: _uiScheduler);
        ClearAttachmentsCommand = ReactiveCommand.Create(() => Attachments.Clear(), outputScheduler: _uiScheduler);
        SelectMcpServerCommand = ReactiveCommand.CreateFromTask<McpServerDefinition>(SelectMcpServerAsync, null, _uiScheduler);
        SelectMcpToolCommand = ReactiveCommand.Create<McpToolDefinition>(SelectMcpTool, outputScheduler: _uiScheduler);
        InsertMcpToolCommand = ReactiveCommand.Create(InsertMcpToolInvocation, outputScheduler: _uiScheduler);
        DebugSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildDebugPrompt(); }, outputScheduler: _uiScheduler);
        CreateTestForSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildTestPrompt(); }, outputScheduler: _uiScheduler);
        CreatePlanCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildPlanPrompt(Prompt, BuildAgentSummary()); Mode = CodexRunMode.Plan; }, outputScheduler: _uiScheduler);
        ExplainSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildExplainPrompt(); }, outputScheduler: _uiScheduler);
        FixSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildFixPrompt(); }, outputScheduler: _uiScheduler);
        ReviewSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildReviewPrompt(); }, outputScheduler: _uiScheduler);
        OptimizeSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildOptimizePrompt(); }, outputScheduler: _uiScheduler);
        GenerateDocsCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildDocumentationPrompt(); }, outputScheduler: _uiScheduler);

        _subscriptions = new CompositeDisposableLike(
            _codex.Events.ObserveOnSafe(_uiScheduler).Subscribe(OnCodexEvent),
            _taskOrchestrator.Events.ObserveOnSafe(_uiScheduler).Subscribe(OnOrchestrationEvent),
            _skillIndex.Skills.ObserveOnSafe(_uiScheduler).Subscribe(UpdateSkills),
            _memoryStore.Memories.ObserveOnSafe(_uiScheduler).Subscribe(UpdateMemories),
            _mcpConfig.Servers.ObserveOnSafe(_uiScheduler).Subscribe(UpdateMcpServers),
            this.WhenAnyValue(x => x.Prompt).ThrottleDistinct(TimeSpan.FromMilliseconds(180), _uiScheduler).Subscribe(OnPromptChanged));

        Refresh();
        UpdateAnalytics(Prompt);
        _joinableTaskFactory.RunAsync(async () => await CheckPrerequisitesAsync().ConfigureAwait(true)).Task.FireAndForget();
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
    public ObservableCollection<CodexTransportKind> TransportOptions { get; }
    public ObservableCollection<AgentExecutionStrategy> AgentStrategyOptions { get; }
    public ObservableCollection<AgentModelSelectionMode> AgentModelSelectionModeOptions { get; }

    public ReactiveCommand<Unit, Unit> RunCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> NewThreadCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> CheckPrerequisitesCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshAnalyticsCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyRecommendedModelCommand { get; }
    public ReactiveCommand<Unit, Unit> AddUserMemoryCommand { get; }
    public ReactiveCommand<Unit, Unit> AddWorkspaceMemoryCommand { get; }
    public ReactiveCommand<Unit, Unit> AddImageAttachmentCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearAttachmentsCommand { get; }
    public ReactiveCommand<McpServerDefinition, Unit> SelectMcpServerCommand { get; }
    public ReactiveCommand<McpToolDefinition, Unit> SelectMcpToolCommand { get; }
    public ReactiveCommand<Unit, Unit> InsertMcpToolCommand { get; }
    public ReactiveCommand<Unit, Unit> DebugSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateTestForSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> CreatePlanCommand { get; }
    public ReactiveCommand<Unit, Unit> ExplainSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> FixSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ReviewSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> OptimizeSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> GenerateDocsCommand { get; }

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
    public bool IsSettingsPanelOpen { get => _isSettingsPanelOpen; set => this.RaiseAndSetIfChanged(ref _isSettingsPanelOpen, value); }
    public bool UseMultiAgentOrchestration { get => _useMultiAgentOrchestration; set { if (!CanChangeSetting(_useMultiAgentOrchestration, value)) return; this.RaiseAndSetIfChanged(ref _useMultiAgentOrchestration, value); } }
    public bool BudgetDrivenModelSelection { get => _budgetDrivenModelSelection; set { if (!CanChangeSetting(_budgetDrivenModelSelection, value)) return; this.RaiseAndSetIfChanged(ref _budgetDrivenModelSelection, value); SaveModelSettings(); UpdateAnalytics(Prompt); } }
    public int MaxAgentConcurrency { get => _maxAgentConcurrency; set { var clamped = Math.Max(1, value); if (!CanChangeSetting(_maxAgentConcurrency, clamped)) return; this.RaiseAndSetIfChanged(ref _maxAgentConcurrency, clamped); } }
    public int SelectedToolTabIndex { get => _selectedToolTabIndex; set => this.RaiseAndSetIfChanged(ref _selectedToolTabIndex, Math.Max(0, value)); }
    public double InputAreaHeight { get => _inputAreaHeight; set { var clamped = ClampInputHeight(value); if (!CanChangeSetting(_inputAreaHeight, clamped)) return; this.RaiseAndSetIfChanged(ref _inputAreaHeight, clamped); SaveInputAreaHeight(clamped); } }
    public AgentExecutionStrategy AgentStrategy { get => _agentStrategy; set { if (!CanChangeSetting(_agentStrategy, value)) return; this.RaiseAndSetIfChanged(ref _agentStrategy, value); } }
    public CodexRunMode Mode { get => _mode; set { if (!CanChangeSetting(_mode, value)) return; this.RaiseAndSetIfChanged(ref _mode, value); } }
    public string SelectedModel { get => _selectedModel; set { if (!CanChangeSetting(_selectedModel, value)) return; this.RaiseAndSetIfChanged(ref _selectedModel, value); SaveModelSettings(); UpdateAnalytics(Prompt); } }
    public string FailoverModel { get => _failoverModel; set { if (!CanChangeSetting(_failoverModel, value)) return; this.RaiseAndSetIfChanged(ref _failoverModel, value); SaveModelSettings(); UpdateAnalytics(Prompt); } }
    public string SelectedReasoning { get => _selectedReasoning; set { if (!CanChangeSetting(_selectedReasoning, value)) return; this.RaiseAndSetIfChanged(ref _selectedReasoning, value); SaveModelSettings(); } }
    public string SelectedVerbosity { get => _selectedVerbosity; set { if (!CanChangeSetting(_selectedVerbosity, value)) return; this.RaiseAndSetIfChanged(ref _selectedVerbosity, value); SaveModelSettings(); } }
    public string OrchestrationModel { get => _orchestrationModel; set { if (!CanChangeSetting(_orchestrationModel, value)) return; this.RaiseAndSetIfChanged(ref _orchestrationModel, value); SaveModelSettings(); } }
    public string BudgetModel { get => _budgetModel; set { if (!CanChangeSetting(_budgetModel, value)) return; this.RaiseAndSetIfChanged(ref _budgetModel, value); SaveModelSettings(); UpdateAnalytics(Prompt); } }
    public ModelUsageEstimate ModelEstimate { get => _modelEstimate; set => this.RaiseAndSetIfChanged(ref _modelEstimate, value); }
    public string AnalyticsSummary => ModelEstimate.Summary;
    public string AnalyticsRecommendation => ModelEstimate.RecommendationReason;
    public string McpInputPrompt { get => _mcpInputPrompt; set => this.RaiseAndSetIfChanged(ref _mcpInputPrompt, value); }
    public string RateLimitUpdatedAt { get => _rateLimitUpdatedAt; set => this.RaiseAndSetIfChanged(ref _rateLimitUpdatedAt, value); }
    public string CodexSetupSummary { get => _codexSetupSummary; set => this.RaiseAndSetIfChanged(ref _codexSetupSummary, value); }
    public string CodexSetupInstructions { get => _codexSetupInstructions; set => this.RaiseAndSetIfChanged(ref _codexSetupInstructions, value); }
    public PromptSuggestionItem? SelectedPromptSuggestion { get => _selectedPromptSuggestion; set => this.RaiseAndSetIfChanged(ref _selectedPromptSuggestion, value); }
    public bool IsPromptSuggestionOpen { get => _isPromptSuggestionOpen; set => this.RaiseAndSetIfChanged(ref _isPromptSuggestionOpen, value); }
    public ApprovalPolicy ApprovalPolicy { get => _approvalPolicy; set { if (!CanChangeSetting(_approvalPolicy, value)) return; this.RaiseAndSetIfChanged(ref _approvalPolicy, value); SaveModelSettings(); } }
    public SandboxMode SandboxMode { get => _sandboxMode; set { if (!CanChangeSetting(_sandboxMode, value)) return; this.RaiseAndSetIfChanged(ref _sandboxMode, value); SaveModelSettings(); } }
    public CodexTransportKind Transport { get => _transport; set { if (!CanChangeSetting(_transport, value)) return; this.RaiseAndSetIfChanged(ref _transport, value); } }
    public McpServerDefinition? SelectedMcpServer { get => _selectedMcpServer; set => this.RaiseAndSetIfChanged(ref _selectedMcpServer, value); }
    public McpToolDefinition? SelectedMcpTool { get => _selectedMcpTool; set => this.RaiseAndSetIfChanged(ref _selectedMcpTool, value); }
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
            IsSettingsPanelOpen = true;
            return;
        }

        Prompt = string.Empty;
        IsRunning = true;
        Status = "Running VSCodex...";
        AddMessage(CodexMessageRole.User, userPrompt);
        try
        {
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
            var request = new CodexRunRequest { Prompt = userPrompt, ThreadId = ThreadId, WorkspaceRoot = _workspace.CurrentWorkspaceRoot, Options = options, Attachments = Attachments.ToList(), Skills = Skills.Where(x => x.IsEnabled).ToList(), Memories = _memoryStore.Search(userPrompt, 10), McpServers = McpServers.Where(x => x.IsEnabled).ToList(), WorkspaceFiles = workspaceFiles, AgentRoles = selectedAgents };
            ModelEstimate = _modelAnalytics.Estimate(request);
            this.RaisePropertyChanged(nameof(AnalyticsSummary));
            this.RaisePropertyChanged(nameof(AnalyticsRecommendation));
            var result = await (UseMultiAgentOrchestration ? _taskOrchestrator.RunAsync(request) : _codex.RunAsync(request)).ConfigureAwait(false);
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            UpdateRateLimitsFromJson(result.RawJson);
            ThreadId = result.ThreadId ?? ThreadId;
            AddMessage(CodexMessageRole.Assistant, result.FinalResponse);
            Status = result.UsedFallback ? "Complete using CLI fallback" : "Complete";
            _session.ThreadId = ThreadId;
            _sessionStore.Save(_session);
        }
        catch (Exception ex)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            AddMessage(CodexMessageRole.Error, ex.ToString());
            Status = "Failed: " + ex.Message;
        }
        finally
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            IsRunning = false;
        }
    }

    private void Refresh()
    {
        try { _workspace.Refresh(); _memoryStore.LoadWorkspace(_workspace.CurrentWorkspaceRoot); _skillIndex.Refresh(_settingsStore.Current.SkillRoots.Concat(new[] { System.IO.Path.Combine(_workspace.CurrentWorkspaceRoot ?? string.Empty, ".codex", "skills") })); _mcpConfig.Refresh(); Status = "Refreshed VSCodex context"; }
        catch (Exception ex) { Status = "Refresh failed: " + ex.Message; }
    }

    private async Task SelectMcpServerAsync(McpServerDefinition server)
    {
        if (server == null) return;
        await _joinableTaskFactory.SwitchToMainThreadAsync();
        SelectedMcpServer = server;
        Status = "Discovering MCP tools for " + server.Name + "...";
        var tools = await _mcpTools.DiscoverToolsAsync(server).ConfigureAwait(false);
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
            case "/settings":
            case "/models":
                ShowToolPanel(0, "VSCodex settings");
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
        IsSettingsPanelOpen = true;
        SelectedToolTabIndex = tabIndex;
        Status = status;
    }

    private void AddMemory(string scope) { _memoryStore.Add(Prompt, scope); Status = $"Saved {scope} memory"; }
    private void AddImageAttachment()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Attach files for VSCodex", Filter = "Supported files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md;*.cs;*.xaml;*.json;*.xml|Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|Documents|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md|All files|*.*", Multiselect = true };
        if (dialog.ShowDialog() == true) AttachFiles(dialog.FileNames);
    }

    public void ShowSettings()
    {
        if (IsRunning)
        {
            Status = "VSCodex settings are locked while a task is running";
            return;
        }

        IsSettingsPanelOpen = true;
        SelectedToolTabIndex = 0;
        Status = "VSCodex settings";
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

        SelectedToolTabIndex = 0;
        IsSettingsPanelOpen = true;
        AddMessage(CodexMessageRole.System, CodexSetupInstructions);
        Status = "VSCodex setup required: install Codex SDK";
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
        Prompt = string.Empty;
        ThreadId = null;
        Messages.Clear();
        _session.Messages.Clear();
        Attachments.Clear();
        Status = "New VSCodex thread";
        UpdateAnalytics(Prompt);
    }

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
        if (prompt.StartsWith("/plan", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildPlanPrompt(prompt.Substring(Math.Min(5, prompt.Length)).Trim(), BuildAgentSummary());
        if (prompt.StartsWith("/explain", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildExplainPrompt();
        if (prompt.StartsWith("/fix", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildFixPrompt();
        if (prompt.StartsWith("/review", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildReviewPrompt();
        if (prompt.StartsWith("/optimize", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildOptimizePrompt();
        if (prompt.StartsWith("/docs", StringComparison.OrdinalIgnoreCase)) return _assistantContext.BuildDocumentationPrompt();
        return prompt;
    }

    private IEnumerable<AgentRoleDefinition> ApplyModelSelection(IEnumerable<AgentRoleDefinition> agents)
    {
        foreach (var agent in agents)
        {
            if (BudgetDrivenModelSelection || agent.ModelSelectionMode == AgentModelSelectionMode.BudgetDriven)
            {
                agent.Model = BudgetModel;
            }
            yield return agent;
        }
    }

    private string EffectiveMainModel() => BudgetDrivenModelSelection && !string.IsNullOrWhiteSpace(BudgetModel) ? BudgetModel : SelectedModel;
    private string EffectiveOrchestrationModel() => BudgetDrivenModelSelection && !string.IsNullOrWhiteSpace(BudgetModel) ? BudgetModel : OrchestrationModel;

    private string BuildAgentSummary()
    {
        var sb = new StringBuilder();
        foreach (var agent in AgentRoles.Where(x => x.IsEnabled)) sb.AppendLine($"- {agent.Name} ({agent.Role}) model={agent.Model}; mode={agent.ModelSelectionMode}: {agent.Instructions}");
        return sb.ToString();
    }

    private void OnCodexEvent(CodexEvent ev)
    {
        RunOnUiThread(() =>
        {
            UpdateRateLimitsFromJson(ev.RawJson);
            if (ev.Type == "stdout" || ev.Type == "message") AddMessage(CodexMessageRole.Assistant, ev.Message);
            else if (ev.Type == "fallback" || ev.Type == "stderr" || ev.Type == "bridge-output") AddMessage(CodexMessageRole.System, $"[{ev.Type}] {ev.Message}");
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

    private void AddMessage(CodexMessageRole role, string content)
    {
        RunOnUiThread(() =>
        {
            var message = new ChatMessage { Role = role, Content = content ?? string.Empty };
            Messages.Add(message);
            _session.Messages.Add(message);
        });
    }

    private void UpdateSkills(IReadOnlyList<SkillDefinition> items) => Replace(Skills, items);
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
        yield return new PromptSuggestionItem { Kind = "Tools", DisplayText = "/mcp", Detail = "Open VSCodex MCP server and tool selection", InsertText = "/mcp " };
        yield return new PromptSuggestionItem { Kind = "Settings", DisplayText = "/settings", Detail = "Open model, sandbox, reasoning, and failover settings", InsertText = "/settings " };
        yield return new PromptSuggestionItem { Kind = "Context", DisplayText = "/context", Detail = "Open selected-code and repository file context", InsertText = "/context " };
        yield return new PromptSuggestionItem { Kind = "Settings", DisplayText = "/analytics", Detail = "Open model cost and complexity analytics", InsertText = "/analytics " };
        yield return new PromptSuggestionItem { Kind = "Settings", DisplayText = "/memory", Detail = "Open ReactiveMemory controls and saved context", InsertText = "/memory " };
        yield return new PromptSuggestionItem { Kind = "Settings", DisplayText = "/agents", Detail = "Open multi-agent roles and orchestration controls", InsertText = "/agents " };
        yield return new PromptSuggestionItem { Kind = "Settings", DisplayText = "/skills", Detail = "Open Codex skills controls", InsertText = "/skills " };
        yield return new PromptSuggestionItem { Kind = "Files", DisplayText = "/attachments", Detail = "Open prompt attachments", InsertText = "/attachments " };
    }

    private void SaveInputAreaHeight(double value)
    {
        var settings = _settingsStore.Current;
        if (Math.Abs(settings.DefaultInputAreaHeight - value) < 0.1d) return;
        settings.DefaultInputAreaHeight = value;
        _settingsStore.Save(settings);
    }

    private void SaveModelSettings()
    {
        var settings = _settingsStore.Current;
        settings.DefaultModel = string.IsNullOrWhiteSpace(SelectedModel) ? settings.DefaultModel : SelectedModel;
        settings.DefaultFailoverModel = string.IsNullOrWhiteSpace(FailoverModel) ? settings.DefaultFailoverModel : FailoverModel;
        settings.DefaultReasoningEffort = string.IsNullOrWhiteSpace(SelectedReasoning) ? settings.DefaultReasoningEffort : SelectedReasoning;
        settings.DefaultVerbosity = string.IsNullOrWhiteSpace(SelectedVerbosity) ? settings.DefaultVerbosity : SelectedVerbosity;
        settings.DefaultApprovalPolicy = ApprovalPolicy;
        settings.DefaultSandboxMode = SandboxMode;
        settings.DefaultOrchestrationModel = OrchestrationModel;
        settings.DefaultBudgetDrivenModelSelection = BudgetDrivenModelSelection;
        settings.DefaultBudgetModel = BudgetModel;
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
    private static double ClampInputHeight(double value) => Math.Max(80d, Math.Min(600d, value <= 0d ? 180d : value));
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
            root.SelectToken("result.result.rateLimits", false),
            root.SelectToken("result.result.rate_limits", false),
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

    public void Dispose() => _subscriptions.Dispose();
    private sealed class CompositeDisposableLike : IDisposable { private readonly IDisposable[] _items; public CompositeDisposableLike(params IDisposable[] items) => _items = items; public void Dispose() { foreach (var item in _items) item.Dispose(); } }
}
