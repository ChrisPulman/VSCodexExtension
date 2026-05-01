using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Extensions;
using VSCodexExtension.Infrastructure;
using VSCodexExtension.Models;
using VSCodexExtension.Services;

namespace VSCodexExtension.ViewModels
{
    public sealed class CodexToolWindowViewModel : ReactiveObject, IDisposable
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
        private readonly IDisposable _subscriptions;
        private readonly CodexSessionDocument _session;

        private string _prompt = string.Empty;
        private string _status = "Ready";
        private bool _isRunning;
        private bool _useMultiAgentOrchestration;
        private bool _budgetDrivenModelSelection;
        private int _maxAgentConcurrency = 1;
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
        private ApprovalPolicy _approvalPolicy;
        private SandboxMode _sandboxMode;
        private CodexTransportKind _transport = CodexTransportKind.SdkBridge;
        private McpServerDefinition? _selectedMcpServer;
        private McpToolDefinition? _selectedMcpTool;
        private string? _threadId;

        public CodexToolWindowViewModel(
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
            IModelAnalyticsService modelAnalytics)
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
            OrchestrationSections = new ObservableCollection<OrchestrationTaskSection>();
            AgentRoles = new ObservableCollection<AgentRoleDefinition>(settings.AgentRoles ?? new List<AgentRoleDefinition>());

            ModelOptions = new ObservableCollection<string>(settings.CustomModels.Distinct(StringComparer.OrdinalIgnoreCase));
            ReasoningOptions = new ObservableCollection<string>(settings.CustomReasoningEfforts);
            VerbosityOptions = new ObservableCollection<string>(settings.CustomVerbosityOptions);
            ModeOptions = new ObservableCollection<CodexRunMode>((CodexRunMode[])Enum.GetValues(typeof(CodexRunMode)));
            ApprovalOptions = new ObservableCollection<ApprovalPolicy>((ApprovalPolicy[])Enum.GetValues(typeof(ApprovalPolicy)));
            SandboxOptions = new ObservableCollection<SandboxMode>((SandboxMode[])Enum.GetValues(typeof(SandboxMode)));
            TransportOptions = new ObservableCollection<CodexTransportKind>((CodexTransportKind[])Enum.GetValues(typeof(CodexTransportKind)));
            AgentStrategyOptions = new ObservableCollection<AgentExecutionStrategy>((AgentExecutionStrategy[])Enum.GetValues(typeof(AgentExecutionStrategy)));
            AgentModelSelectionModeOptions = new ObservableCollection<AgentModelSelectionMode>((AgentModelSelectionMode[])Enum.GetValues(typeof(AgentModelSelectionMode)));

            var canRun = this.WhenAnyValue(x => x.Prompt, x => x.IsRunning, (p, r) => !string.IsNullOrWhiteSpace(p) && !r);
            RunCommand = ReactiveCommand.CreateFromTask(RunAsync, canRun);
            CancelCommand = ReactiveCommand.Create(() => { _taskOrchestrator.Cancel(); _codex.Cancel(); }, this.WhenAnyValue(x => x.IsRunning));
            RefreshCommand = ReactiveCommand.Create(Refresh);
            RefreshAnalyticsCommand = ReactiveCommand.Create(() => UpdateAnalytics(Prompt));
            ApplyRecommendedModelCommand = ReactiveCommand.Create(ApplyRecommendedModel);
            AddUserMemoryCommand = ReactiveCommand.Create(() => AddMemory("user"), this.WhenAnyValue(x => x.Prompt, p => !string.IsNullOrWhiteSpace(p)));
            AddWorkspaceMemoryCommand = ReactiveCommand.Create(() => AddMemory("workspace"), this.WhenAnyValue(x => x.Prompt, p => !string.IsNullOrWhiteSpace(p)));
            AddImageAttachmentCommand = ReactiveCommand.Create(AddImageAttachment);
            ClearAttachmentsCommand = ReactiveCommand.Create(() => Attachments.Clear());
            SelectMcpServerCommand = ReactiveCommand.CreateFromTask<McpServerDefinition>(SelectMcpServerAsync);
            SelectMcpToolCommand = ReactiveCommand.Create<McpToolDefinition>(SelectMcpTool);
            InsertMcpToolCommand = ReactiveCommand.Create(InsertMcpToolInvocation);
            DebugSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildDebugPrompt(); });
            CreateTestForSelectionCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildTestPrompt(); });
            CreatePlanCommand = ReactiveCommand.Create(() => { Prompt = _assistantContext.BuildPlanPrompt(Prompt, BuildAgentSummary()); Mode = CodexRunMode.Plan; });

            _subscriptions = new CompositeDisposableLike(
                _codex.Events.ObserveOnSafe(RxSchedulers.MainThreadScheduler).Subscribe(OnCodexEvent),
                _taskOrchestrator.Events.ObserveOnSafe(RxSchedulers.MainThreadScheduler).Subscribe(OnOrchestrationEvent),
                _skillIndex.Skills.ObserveOnSafe(RxSchedulers.MainThreadScheduler).Subscribe(UpdateSkills),
                _memoryStore.Memories.ObserveOnSafe(RxSchedulers.MainThreadScheduler).Subscribe(UpdateMemories),
                _mcpConfig.Servers.ObserveOnSafe(RxSchedulers.MainThreadScheduler).Subscribe(UpdateMcpServers),
                this.WhenAnyValue(x => x.Prompt).ThrottleDistinct(TimeSpan.FromMilliseconds(180), RxSchedulers.MainThreadScheduler).Subscribe(OnPromptChanged));

            Refresh();
            UpdateAnalytics(Prompt);
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
        public ObservableCollection<OrchestrationTaskSection> OrchestrationSections { get; }
        public ObservableCollection<AgentRoleDefinition> AgentRoles { get; }
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

        public string Prompt { get => _prompt; set => this.RaiseAndSetIfChanged(ref _prompt, value); }
        public string Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value); }
        public bool IsRunning { get => _isRunning; set => this.RaiseAndSetIfChanged(ref _isRunning, value); }
        public bool UseMultiAgentOrchestration { get => _useMultiAgentOrchestration; set => this.RaiseAndSetIfChanged(ref _useMultiAgentOrchestration, value); }
        public bool BudgetDrivenModelSelection { get => _budgetDrivenModelSelection; set { this.RaiseAndSetIfChanged(ref _budgetDrivenModelSelection, value); SaveModelSettings(); UpdateAnalytics(Prompt); } }
        public int MaxAgentConcurrency { get => _maxAgentConcurrency; set => this.RaiseAndSetIfChanged(ref _maxAgentConcurrency, Math.Max(1, value)); }
        public double InputAreaHeight { get => _inputAreaHeight; set { var clamped = ClampInputHeight(value); this.RaiseAndSetIfChanged(ref _inputAreaHeight, clamped); SaveInputAreaHeight(clamped); } }
        public AgentExecutionStrategy AgentStrategy { get => _agentStrategy; set => this.RaiseAndSetIfChanged(ref _agentStrategy, value); }
        public CodexRunMode Mode { get => _mode; set => this.RaiseAndSetIfChanged(ref _mode, value); }
        public string SelectedModel { get => _selectedModel; set { this.RaiseAndSetIfChanged(ref _selectedModel, value); SaveModelSettings(); UpdateAnalytics(Prompt); } }
        public string FailoverModel { get => _failoverModel; set { this.RaiseAndSetIfChanged(ref _failoverModel, value); SaveModelSettings(); UpdateAnalytics(Prompt); } }
        public string SelectedReasoning { get => _selectedReasoning; set { this.RaiseAndSetIfChanged(ref _selectedReasoning, value); SaveModelSettings(); } }
        public string SelectedVerbosity { get => _selectedVerbosity; set { this.RaiseAndSetIfChanged(ref _selectedVerbosity, value); SaveModelSettings(); } }
        public string OrchestrationModel { get => _orchestrationModel; set { this.RaiseAndSetIfChanged(ref _orchestrationModel, value); SaveModelSettings(); } }
        public string BudgetModel { get => _budgetModel; set { this.RaiseAndSetIfChanged(ref _budgetModel, value); SaveModelSettings(); UpdateAnalytics(Prompt); } }
        public ModelUsageEstimate ModelEstimate { get => _modelEstimate; set => this.RaiseAndSetIfChanged(ref _modelEstimate, value); }
        public string AnalyticsSummary => ModelEstimate.Summary;
        public string AnalyticsRecommendation => ModelEstimate.RecommendationReason;
        public string McpInputPrompt { get => _mcpInputPrompt; set => this.RaiseAndSetIfChanged(ref _mcpInputPrompt, value); }
        public ApprovalPolicy ApprovalPolicy { get => _approvalPolicy; set { this.RaiseAndSetIfChanged(ref _approvalPolicy, value); SaveModelSettings(); } }
        public SandboxMode SandboxMode { get => _sandboxMode; set { this.RaiseAndSetIfChanged(ref _sandboxMode, value); SaveModelSettings(); } }
        public CodexTransportKind Transport { get => _transport; set => this.RaiseAndSetIfChanged(ref _transport, value); }
        public McpServerDefinition? SelectedMcpServer { get => _selectedMcpServer; set => this.RaiseAndSetIfChanged(ref _selectedMcpServer, value); }
        public McpToolDefinition? SelectedMcpTool { get => _selectedMcpTool; set => this.RaiseAndSetIfChanged(ref _selectedMcpTool, value); }
        public string? ThreadId { get => _threadId; set => this.RaiseAndSetIfChanged(ref _threadId, value); }

        private async Task RunAsync()
        {
            var userPrompt = ExpandAssistantSlashCommand(Prompt);
            if (IsMcpDiscoveryPrompt(userPrompt))
            {
                ShowMcpServerList();
                return;
            }

            Prompt = string.Empty;
            IsRunning = true;
            Status = "Running Codex...";
            AddMessage(CodexMessageRole.User, userPrompt);
            try
            {
                var workspaceFiles = _workspace.ResolveMentions(userPrompt, 12000)
                    .Concat(_workspace.ResolveHashReferences(userPrompt, 12000))
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
                var result = await (UseMultiAgentOrchestration ? _taskOrchestrator.RunAsync(request) : _codex.RunAsync(request)).ConfigureAwait(true);
                ThreadId = result.ThreadId ?? ThreadId;
                AddMessage(CodexMessageRole.Assistant, result.FinalResponse);
                Status = result.UsedFallback ? "Complete using CLI fallback" : "Complete";
                _session.ThreadId = ThreadId;
                _sessionStore.Save(_session);
            }
            catch (Exception ex) { AddMessage(CodexMessageRole.Error, ex.ToString()); Status = "Failed: " + ex.Message; }
            finally { IsRunning = false; }
        }

        private void Refresh()
        {
            try { _workspace.Refresh(); _memoryStore.LoadWorkspace(_workspace.CurrentWorkspaceRoot); _skillIndex.Refresh(_settingsStore.Current.SkillRoots.Concat(new[] { System.IO.Path.Combine(_workspace.CurrentWorkspaceRoot ?? string.Empty, ".codex", "skills") })); _mcpConfig.Refresh(); Status = "Refreshed Codex context"; }
            catch (Exception ex) { Status = "Refresh failed: " + ex.Message; }
        }

        private async Task SelectMcpServerAsync(McpServerDefinition server)
        {
            if (server == null) return;
            SelectedMcpServer = server;
            Status = "Discovering MCP tools for " + server.Name + "...";
            var tools = await _mcpTools.DiscoverToolsAsync(server).ConfigureAwait(true);
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
            UpdateReferenceSuggestions(prompt);
            UpdateAnalytics(prompt);
            if (IsMcpDiscoveryPrompt(prompt)) ShowMcpServerList();
        }

        private void ShowMcpServerList()
        {
            Replace(McpToolSuggestions, Array.Empty<McpToolDefinition>());
            Replace(McpToolInputFields, Array.Empty<McpToolInputField>());
            McpInputPrompt = "Select an MCP server to list tools. Then select a tool and provide required input fields; optional fields show 'option'.";
            Status = McpServers.Count == 0 ? "No MCP servers are configured in .codex/config.toml" : "Select an MCP server from the MCP tab";
        }

        private void AddMemory(string scope) { _memoryStore.Add(Prompt, scope); Status = $"Saved {scope} memory"; }
        private void AddImageAttachment()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Attach files for Codex", Filter = "Supported files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md;*.cs;*.xaml;*.json;*.xml|Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|Documents|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md|All files|*.*", Multiselect = true };
            if (dialog.ShowDialog() == true) AttachFiles(dialog.FileNames);
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

        private void OnCodexEvent(CodexEvent ev) { if (ev.Type == "stdout" || ev.Type == "message") AddMessage(CodexMessageRole.Assistant, ev.Message); else if (ev.Type == "fallback" || ev.Type == "stderr" || ev.Type == "bridge-output") AddMessage(CodexMessageRole.System, $"[{ev.Type}] {ev.Message}"); else Status = ev.Message; }
        private void OnOrchestrationEvent(OrchestrationEvent ev)
        {
            Status = ev.Message;
            if (ev.Type == "plan-created" && _taskOrchestrator.CurrentPlan != null) Replace(OrchestrationSections, _taskOrchestrator.CurrentPlan.Sections);
            if (ev.Section != null && !OrchestrationSections.Any(x => x.Id == ev.Section.Id)) OrchestrationSections.Add(ev.Section);
            AddMessage(CodexMessageRole.System, $"[orchestration:{ev.Type}] {ev.Message}");
        }

        private void AddMessage(CodexMessageRole role, string content) { var message = new ChatMessage { Role = role, Content = content }; Messages.Add(message); _session.Messages.Add(message); }
        private void UpdateSkills(IReadOnlyList<SkillDefinition> items) => Replace(Skills, items);
        private void UpdateMemories(IReadOnlyList<MemoryEntry> items) => Replace(Memories, items);
        private void UpdateMcpServers(IReadOnlyList<McpServerDefinition> items) => Replace(McpServers, items);
        private void UpdateReferenceSuggestions(string prompt)
        {
            var token = LastReferenceToken(prompt);
            Replace(FileSuggestions, _workspace.SearchFiles(token != null && token.StartsWith("@", StringComparison.Ordinal) ? token : string.Empty, 8));
            Replace(ContextSuggestions, _workspace.SearchContextReferences(token != null && token.StartsWith("#", StringComparison.Ordinal) ? token : string.Empty, 8));
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
        private static bool IsMcpDiscoveryPrompt(string prompt) => (prompt ?? string.Empty).Trim().StartsWith("/MCP", StringComparison.OrdinalIgnoreCase);
        private static double ClampInputHeight(double value) => Math.Max(80d, Math.Min(600d, value <= 0d ? 180d : value));
        private static McpToolInputField CloneField(McpToolInputField field) => new McpToolInputField { Name = field.Name, Type = field.Type, Description = field.Description, IsRequired = field.IsRequired, Value = field.Value };
        private static string InferAttachmentKind(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" }.Contains(ext)) return "image";
            if (new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md" }.Contains(ext)) return "document";
            return "file";
        }
        private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items) { target.Clear(); foreach (var item in items) target.Add(item); }
        public void Dispose() => _subscriptions.Dispose();
        private sealed class CompositeDisposableLike : IDisposable { private readonly IDisposable[] _items; public CompositeDisposableLike(params IDisposable[] items) => _items = items; public void Dispose() { foreach (var item in _items) item.Dispose(); } }
    }
}
