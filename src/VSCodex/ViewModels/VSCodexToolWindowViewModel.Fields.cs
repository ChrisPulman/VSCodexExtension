// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace VSCodex.ViewModels;

/// <summary>Declares state shared by the VSCodex tool-window view model.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Named number used by this type.</summary>
    private const double Numeric0Point1 = 0.1;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric10 = 10;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric100 = 100;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric1000 = 1000;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric1000000 = 1_000_000;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric1000000000 = 1_000_000_000;

    /// <summary>Named number used by this type.</summary>
    private const long Numeric100000000000L = 100_000_000_000L;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric12 = 12;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric120 = 120;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric12000 = 12_000;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric15Point0 = 15.0;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric16 = 16;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric180 = 180;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric2 = 2;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric24 = 24;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric3 = 3;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric32Point0 = 32.0;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric4 = 4;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric40 = 40;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric400 = 400;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric5 = 5;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric5000 = 5000;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric6 = 6;

    /// <summary>Named number used by this type.</summary>
    private const double Numeric600Point0 = 600.0;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric7 = 7;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric90 = 90;

    /// <summary>Named string used by this type.</summary>
    private const string ActionText = "Action";

    /// <summary>Named string used by this type.</summary>
    private const string CheckingVSCodexPrerequisitesText = "Checking VSCodex prerequisites...";

    /// <summary>Named string used by this type.</summary>
    private const string ToolsText = "Tools";

    /// <summary>Named string used by this type.</summary>
    private const string VSCodexSettingsAreLockedWhileATaskIsRunnText = "VSCodex settings are locked while a task is running";

    /// <summary>Named string used by this type.</summary>
    private const string WeeklyText = "Weekly";

    /// <summary>Defines the model Settings Save Debounce Milliseconds.</summary>
    private const int ModelSettingsSaveDebounceMilliseconds = 350;

    /// <summary>Defines the history Tool Tab Index.</summary>
    private const int HistoryToolTabIndex = 0;

    /// <summary>Defines the setup Tool Tab Index.</summary>
    private const int SetupToolTabIndex = 1;

    /// <summary>Defines the context Tool Tab Index.</summary>
    private const int ContextToolTabIndex = Numeric2;

    /// <summary>Defines the skills Tool Tab Index.</summary>
    private const int SkillsToolTabIndex = Numeric3;

    /// <summary>Defines the mcp Tool Tab Index.</summary>
    private const int McpToolTabIndex = Numeric4;

    /// <summary>Defines the memory Tool Tab Index.</summary>
    private const int MemoryToolTabIndex = Numeric5;

    /// <summary>Defines the agents Tool Tab Index.</summary>
    private const int AgentsToolTabIndex = Numeric6;

    /// <summary>Defines the attachments Tool Tab Index.</summary>
    private const int AttachmentsToolTabIndex = Numeric7;

    /// <summary>Stores the voice Submit Only Commands.</summary>
    private static readonly string[] VoiceSubmitOnlyCommands = ["send", "send it", "submit", "submit it", "run", "run it", "send request", "submit request"];

    /// <summary>Stores the voice Submit Suffixes.</summary>
    private static readonly string[] VoiceSubmitSuffixes = [" and send", " then send", " and submit", " then submit", " and run", " then run"];

    /// <summary>Stores the settings Store.</summary>
    private readonly ISettingsStore _settingsStore;

    /// <summary>Stores the memory Store.</summary>
    private readonly IMemoryStore _memoryStore;

    /// <summary>Stores the skill Index.</summary>
    private readonly ISkillIndexService _skillIndex;

    /// <summary>Stores the mcp Config.</summary>
    private readonly IMcpConfigService _mcpConfig;

    /// <summary>Stores the mcp Tools.</summary>
    private readonly IMcpToolCatalogService _mcpTools;

    /// <summary>Stores the reactive Memory.</summary>
    private readonly IReactiveMemoryService _reactiveMemory;

    /// <summary>Stores the workspace.</summary>
    private readonly IWorkspaceContextService _workspace;

    /// <summary>Stores the session Store.</summary>
    private readonly ISessionStore _sessionStore;

    /// <summary>Stores the codex.</summary>
    private readonly ICodexOrchestrator _codex;

    /// <summary>Stores the task Orchestrator.</summary>
    private readonly ITaskOrchestrationService _taskOrchestrator;

    /// <summary>Stores the assistant Context.</summary>
    private readonly ICodingAssistantContextService _assistantContext;

    /// <summary>Stores the model Analytics.</summary>
    private readonly IModelAnalyticsService _modelAnalytics;

    /// <summary>Stores the environment.</summary>
    private readonly ICodexEnvironmentService _environment;

    /// <summary>Stores the voice Input.</summary>
    private readonly IVoiceInputService _voiceInput;

    /// <summary>Stores the joinable Task Factory.</summary>
    private readonly JoinableTaskFactory _joinableTaskFactory;

    /// <summary>Provides testable access to the system clock.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Stores the ui Dispatcher.</summary>
    private readonly Dispatcher _uiDispatcher;

    /// <summary>Stores the ui Scheduler.</summary>
    private readonly IScheduler _uiScheduler;

    /// <summary>Stores the subscriptions.</summary>
    private readonly IDisposable _subscriptions;

    /// <summary>Stores the lifetime.</summary>
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>Stores the model Settings Save Gate.</summary>
    private readonly object _modelSettingsSaveGate = new();

    /// <summary>Stores the queued Prompts.</summary>
    private readonly Queue<string> _queuedPrompts = new();

    /// <summary>Stores the session.</summary>
    private CodexSessionDocument _session;

    /// <summary>Stores the prompt Change Revision.</summary>
    private int _promptChangeRevision;

    /// <summary>Stores the last Workspace Identity Id.</summary>
    private string _lastWorkspaceIdentityId = string.Empty;

    /// <summary>Stores the last Workspace Settings Id.</summary>
    private string _lastWorkspaceSettingsId = string.Empty;

    /// <summary>Stores the model Settings Save Cancellation.</summary>
    private CancellationTokenSource? _modelSettingsSaveCancellation;

    /// <summary>Stores the has Pending Model Settings Save.</summary>
    private bool _hasPendingModelSettingsSave;

    /// <summary>Stores the is Processing Run Queue.</summary>
    private bool _isProcessingRunQueue;

    /// <summary>Stores the pause Requested.</summary>
    private bool _pauseRequested;

    /// <summary>Stores the stop Requested.</summary>
    private bool _stopRequested;

    /// <summary>Stores the model Settings Save Revision.</summary>
    private int _modelSettingsSaveRevision;

    /// <summary>Stores the prompt.</summary>
    private string _prompt = string.Empty;

    /// <summary>Stores whether a run is active.</summary>
    private bool _isRunning;

    /// <summary>Stores the queued prompt count.</summary>
    private int _queuedPromptCount;

    /// <summary>Stores whether the run is paused.</summary>
    private bool _isPaused;

    /// <summary>Stores whether the tool panel is open.</summary>
    private bool _isToolPanelOpen;

    /// <summary>Stores the selected tool tab index.</summary>
    private int _selectedToolTabIndex;

    /// <summary>Stores the history search text.</summary>
    private string _historySearchText = string.Empty;

    /// <summary>Stores the run mode.</summary>
    private CodexRunMode _mode = CodexRunMode.Chat;

    /// <summary>Stores the model estimate.</summary>
    private ModelUsageEstimate _modelEstimate = new();

    /// <summary>Stores the MCP input prompt.</summary>
    private string _mcpInputPrompt = string.Empty;

    /// <summary>Stores the new skill description.</summary>
    private string _newSkillDescription = string.Empty;

    /// <summary>Stores the rate-limit update text.</summary>
    private string _rateLimitUpdatedAt = "Waiting for Codex rate-limit telemetry";

    /// <summary>Stores the Codex setup instructions.</summary>
    private string _codexSetupInstructions = string.Empty;

    /// <summary>Stores the voice input status.</summary>
    private string _voiceInputStatus = "Voice input ready";

    /// <summary>Stores the voice transcript revision.</summary>
    private int _voiceTranscriptRevision;

    /// <summary>Stores the selected prompt suggestion.</summary>
    private PromptSuggestionItem? _selectedPromptSuggestion;

    /// <summary>Stores whether the prompt suggestion panel is open.</summary>
    private bool _isPromptSuggestionOpen;

    /// <summary>Stores the transport.</summary>
    private CodexTransportKind _transport = CodexTransportKind.SdkBridge;

    /// <summary>Stores the selected MCP server.</summary>
    private McpServerDefinition? _selectedMcpServer;

    /// <summary>Stores the selected MCP tool.</summary>
    private McpToolDefinition? _selectedMcpTool;

    /// <summary>Stores the selected history item.</summary>
    private SessionHistoryItem? _selectedHistoryItem;

    /// <summary>Stores the thread identifier.</summary>
    private string? _threadId;

    /// <summary>Stores the status.</summary>
    private string _status = "Ready";

    /// <summary>Stores the use Multi Agent Orchestration.</summary>
    private bool _useMultiAgentOrchestration;

    /// <summary>Stores the budget Driven Model Selection.</summary>
    private bool _budgetDrivenModelSelection;

    /// <summary>Stores the max Agent Concurrency.</summary>
    private int _maxAgentConcurrency = 1;

    /// <summary>Stores the input Area Height.</summary>
    private double _inputAreaHeight = Numeric180;

    /// <summary>Stores the agent Strategy.</summary>
    private AgentExecutionStrategy _agentStrategy = AgentExecutionStrategy.ReviewGate;

    /// <summary>Stores the selected Model.</summary>
    private string _selectedModel = CodexModelCatalog.DefaultModel;

    /// <summary>Stores the failover Model.</summary>
    private string _failoverModel = CodexModelCatalog.DefaultFailoverModel;

    /// <summary>Stores the selected Reasoning.</summary>
    private string _selectedReasoning = CodexModelCatalog.DefaultReasoningEffort;

    /// <summary>Stores the selected Verbosity.</summary>
    private string _selectedVerbosity = string.Empty;

    /// <summary>Stores the orchestration Model.</summary>
    private string _orchestrationModel = CodexModelCatalog.DefaultModel;

    /// <summary>Stores the budget Model.</summary>
    private string _budgetModel = CodexModelCatalog.DefaultBudgetModel;

    /// <summary>Stores the new Skill Name.</summary>
    private string _newSkillName = string.Empty;

    /// <summary>Stores the skill Root Path Input.</summary>
    private string _skillRootPathInput = string.Empty;

    /// <summary>Stores the codex Setup Summary.</summary>
    private string _codexSetupSummary = CheckingVSCodexPrerequisitesText;

    /// <summary>Stores the active Run Activity.</summary>
    private RunActivityNode? _activeRunActivity;

    /// <summary>Stores the active Progress Node.</summary>
    private RunActivityNode? _activeProgressNode;

    /// <summary>Stores the active Streaming Response.</summary>
    private RunActivityNode? _activeStreamingResponse;

    /// <summary>Stores the pending User Activity Prompt To Suppress.</summary>
    private string _pendingUserActivityPromptToSuppress = string.Empty;

    /// <summary>Stores the active Run Started At.</summary>
    private DateTimeOffset _activeRunStartedAt;

    /// <summary>Stores the active Run Stage.</summary>
    private string _activeRunStage = string.Empty;

    /// <summary>Stores the last Environment Report.</summary>
    private CodexEnvironmentReport? _lastEnvironmentReport;

    /// <summary>Stores the approval Policy.</summary>
    private ApprovalPolicy _approvalPolicy;

    /// <summary>Stores the sandbox Mode.</summary>
    private SandboxMode _sandboxMode;

    /// <summary>Stores the access Level.</summary>
    private CodexAccessLevel _accessLevel;

    /// <summary>Stores the active Turn Id.</summary>
    private string _activeTurnId = string.Empty;

    /// <summary>Stores the active Operation Id.</summary>
    private string _activeOperationId = string.Empty;

    /// <summary>Stores the active Prompt.</summary>
    private string _activePrompt = string.Empty;

    /// <summary>Stores the paused Checkpoint.</summary>
    private ReactiveMemoryPauseCheckpoint? _pausedCheckpoint;
}
