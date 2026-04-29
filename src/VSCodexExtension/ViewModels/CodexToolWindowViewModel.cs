using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Extensions;
using VSCodexExtension.Models;
using VSCodexExtension.Services;
namespace VSCodexExtension.ViewModels
{
    public sealed class CodexToolWindowViewModel : ReactiveObject, IDisposable
    {
        private readonly ISettingsStore _settingsStore; private readonly IMemoryStore _memoryStore; private readonly ISkillIndexService _skillIndex; private readonly IMcpConfigService _mcpConfig; private readonly IWorkspaceContextService _workspace; private readonly ISessionStore _sessionStore; private readonly ICodexOrchestrator _codex; private readonly IDisposable _subscriptions; private readonly CodexSessionDocument _session;
        private string _prompt = string.Empty; private string _status = "Ready"; private bool _isRunning; private CodexRunMode _mode = CodexRunMode.Chat; private string _selectedModel; private string _selectedReasoning; private string _selectedVerbosity; private ApprovalPolicy _approvalPolicy; private SandboxMode _sandboxMode; private CodexTransportKind _transport = CodexTransportKind.SdkBridge; private string? _threadId;
        public CodexToolWindowViewModel(ISettingsStore settingsStore, IMemoryStore memoryStore, ISkillIndexService skillIndex, IMcpConfigService mcpConfig, IWorkspaceContextService workspace, ISessionStore sessionStore, ICodexOrchestrator codex)
        {
            _settingsStore = settingsStore; _memoryStore = memoryStore; _skillIndex = skillIndex; _mcpConfig = mcpConfig; _workspace = workspace; _sessionStore = sessionStore; _codex = codex; _session = sessionStore.Create();
            var settings = _settingsStore.Current; _selectedModel = settings.DefaultModel; _selectedReasoning = settings.DefaultReasoningEffort; _selectedVerbosity = settings.DefaultVerbosity; _approvalPolicy = settings.DefaultApprovalPolicy; _sandboxMode = settings.DefaultSandboxMode;
            Messages = new ObservableCollection<ChatMessage>(); Attachments = new ObservableCollection<CodexAttachment>(); Skills = new ObservableCollection<SkillDefinition>(); Memories = new ObservableCollection<MemoryEntry>(); McpServers = new ObservableCollection<McpServerDefinition>(); FileSuggestions = new ObservableCollection<WorkspaceFileReference>();
            ModelOptions = new ObservableCollection<string>(settings.CustomModels); ReasoningOptions = new ObservableCollection<string>(settings.CustomReasoningEfforts); VerbosityOptions = new ObservableCollection<string>(settings.CustomVerbosityOptions);
            ModeOptions = new ObservableCollection<CodexRunMode>((CodexRunMode[])Enum.GetValues(typeof(CodexRunMode))); ApprovalOptions = new ObservableCollection<ApprovalPolicy>((ApprovalPolicy[])Enum.GetValues(typeof(ApprovalPolicy))); SandboxOptions = new ObservableCollection<SandboxMode>((SandboxMode[])Enum.GetValues(typeof(SandboxMode))); TransportOptions = new ObservableCollection<CodexTransportKind>((CodexTransportKind[])Enum.GetValues(typeof(CodexTransportKind)));
            var canRun = this.WhenAnyValue(x => x.Prompt, x => x.IsRunning, (p, r) => !string.IsNullOrWhiteSpace(p) && !r);
            RunCommand = ReactiveCommand.CreateFromTask(RunAsync, canRun); CancelCommand = ReactiveCommand.Create(() => _codex.Cancel(), this.WhenAnyValue(x => x.IsRunning)); RefreshCommand = ReactiveCommand.Create(Refresh);
            AddUserMemoryCommand = ReactiveCommand.Create(() => AddMemory("user"), this.WhenAnyValue(x => x.Prompt, p => !string.IsNullOrWhiteSpace(p))); AddWorkspaceMemoryCommand = ReactiveCommand.Create(() => AddMemory("workspace"), this.WhenAnyValue(x => x.Prompt, p => !string.IsNullOrWhiteSpace(p)));
            AddImageAttachmentCommand = ReactiveCommand.Create(AddImageAttachment); ClearAttachmentsCommand = ReactiveCommand.Create(() => Attachments.Clear());
            _subscriptions = new CompositeDisposableLike(_codex.Events.ObserveOnSafe(RxSchedulers.MainThreadScheduler).Subscribe(OnCodexEvent), _skillIndex.Skills.ObserveOnSafe(RxSchedulers.MainThreadScheduler).Subscribe(UpdateSkills), _memoryStore.Memories.ObserveOnSafe(RxSchedulers.MainThreadScheduler).Subscribe(UpdateMemories), _mcpConfig.Servers.ObserveOnSafe(RxSchedulers.MainThreadScheduler).Subscribe(UpdateMcpServers), this.WhenAnyValue(x => x.Prompt).ThrottleDistinct(TimeSpan.FromMilliseconds(180), RxSchedulers.MainThreadScheduler).Subscribe(UpdateFileSuggestions));
            Refresh();
        }
        public ObservableCollection<ChatMessage> Messages { get; }
        public ObservableCollection<CodexAttachment> Attachments { get; }
        public ObservableCollection<SkillDefinition> Skills { get; }
        public ObservableCollection<MemoryEntry> Memories { get; }
        public ObservableCollection<McpServerDefinition> McpServers { get; }
        public ObservableCollection<WorkspaceFileReference> FileSuggestions { get; }
        public ObservableCollection<string> ModelOptions { get; }
        public ObservableCollection<string> ReasoningOptions { get; }
        public ObservableCollection<string> VerbosityOptions { get; }
        public ObservableCollection<CodexRunMode> ModeOptions { get; }
        public ObservableCollection<ApprovalPolicy> ApprovalOptions { get; }
        public ObservableCollection<SandboxMode> SandboxOptions { get; }
        public ObservableCollection<CodexTransportKind> TransportOptions { get; }
        public ReactiveCommand<Unit, Unit> RunCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> AddUserMemoryCommand { get; }
        public ReactiveCommand<Unit, Unit> AddWorkspaceMemoryCommand { get; }
        public ReactiveCommand<Unit, Unit> AddImageAttachmentCommand { get; }
        public ReactiveCommand<Unit, Unit> ClearAttachmentsCommand { get; }
        public string Prompt { get => _prompt; set => this.RaiseAndSetIfChanged(ref _prompt, value); }
        public string Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value); }
        public bool IsRunning { get => _isRunning; set => this.RaiseAndSetIfChanged(ref _isRunning, value); }
        public CodexRunMode Mode { get => _mode; set => this.RaiseAndSetIfChanged(ref _mode, value); }
        public string SelectedModel { get => _selectedModel; set => this.RaiseAndSetIfChanged(ref _selectedModel, value); }
        public string SelectedReasoning { get => _selectedReasoning; set => this.RaiseAndSetIfChanged(ref _selectedReasoning, value); }
        public string SelectedVerbosity { get => _selectedVerbosity; set => this.RaiseAndSetIfChanged(ref _selectedVerbosity, value); }
        public ApprovalPolicy ApprovalPolicy { get => _approvalPolicy; set => this.RaiseAndSetIfChanged(ref _approvalPolicy, value); }
        public SandboxMode SandboxMode { get => _sandboxMode; set => this.RaiseAndSetIfChanged(ref _sandboxMode, value); }
        public CodexTransportKind Transport { get => _transport; set => this.RaiseAndSetIfChanged(ref _transport, value); }
        public string? ThreadId { get => _threadId; set => this.RaiseAndSetIfChanged(ref _threadId, value); }
        private async Task RunAsync()
        {
            var userPrompt = Prompt; Prompt = string.Empty; IsRunning = true; Status = "Running Codex..."; AddMessage(CodexMessageRole.User, userPrompt);
            try
            {
                var request = new CodexRunRequest { Prompt = userPrompt, ThreadId = ThreadId, WorkspaceRoot = _workspace.CurrentWorkspaceRoot, Options = new CodexRunOptions { Mode = Mode, Model = SelectedModel, ReasoningEffort = SelectedReasoning, Verbosity = SelectedVerbosity, ApprovalPolicy = ApprovalPolicy, SandboxMode = SandboxMode, Transport = Transport }, Attachments = Attachments.ToList(), Skills = Skills.Where(x => x.IsEnabled).ToList(), Memories = _memoryStore.Search(userPrompt, 10), McpServers = McpServers.Where(x => x.IsEnabled).ToList(), WorkspaceFiles = _workspace.ResolveMentions(userPrompt, 12000) };
                var result = await _codex.RunAsync(request).ConfigureAwait(true); ThreadId = result.ThreadId ?? ThreadId; AddMessage(CodexMessageRole.Assistant, result.FinalResponse); Status = result.UsedFallback ? "Complete using CLI fallback" : "Complete"; _session.ThreadId = ThreadId; _sessionStore.Save(_session);
            }
            catch (Exception ex) { AddMessage(CodexMessageRole.Error, ex.ToString()); Status = "Failed: " + ex.Message; }
            finally { IsRunning = false; }
        }
        private void Refresh()
        {
            try { _workspace.Refresh(); _memoryStore.LoadWorkspace(_workspace.CurrentWorkspaceRoot); _skillIndex.Refresh(_settingsStore.Current.SkillRoots.Concat(new[] { System.IO.Path.Combine(_workspace.CurrentWorkspaceRoot ?? string.Empty, ".codex", "skills") })); _mcpConfig.Refresh(); Status = "Refreshed Codex context"; }
            catch (Exception ex) { Status = "Refresh failed: " + ex.Message; }
        }
        private void AddMemory(string scope) { _memoryStore.Add(Prompt, scope); Status = $"Saved {scope} memory"; }
        private void AddImageAttachment()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Attach image for Codex", Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All files|*.*", Multiselect = true };
            if (dialog.ShowDialog() == true) { foreach (var file in dialog.FileNames) Attachments.Add(new CodexAttachment { Path = file, Kind = "image" }); Status = $"Attached {dialog.FileNames.Length} image(s)"; }
        }
        private void OnCodexEvent(CodexEvent ev) { if (ev.Type == "stdout" || ev.Type == "message") AddMessage(CodexMessageRole.Assistant, ev.Message); else if (ev.Type == "fallback" || ev.Type == "stderr" || ev.Type == "bridge-output") AddMessage(CodexMessageRole.System, $"[{ev.Type}] {ev.Message}"); else Status = ev.Message; }
        private void AddMessage(CodexMessageRole role, string content) { var message = new ChatMessage { Role = role, Content = content }; Messages.Add(message); _session.Messages.Add(message); }
        private void UpdateSkills(System.Collections.Generic.IReadOnlyList<SkillDefinition> items) => Replace(Skills, items);
        private void UpdateMemories(System.Collections.Generic.IReadOnlyList<MemoryEntry> items) => Replace(Memories, items);
        private void UpdateMcpServers(System.Collections.Generic.IReadOnlyList<McpServerDefinition> items) => Replace(McpServers, items);
        private void UpdateFileSuggestions(string prompt) => Replace(FileSuggestions, _workspace.SearchFiles(prompt?.Split(' ').LastOrDefault(x => x.StartsWith("@")) ?? string.Empty, 8));
        private static void Replace<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> items) { target.Clear(); foreach (var item in items) target.Add(item); }
        public void Dispose() => _subscriptions.Dispose();
        private sealed class CompositeDisposableLike : IDisposable { private readonly IDisposable[] _items; public CompositeDisposableLike(params IDisposable[] items) => _items = items; public void Dispose() { foreach (var item in _items) item.Dispose(); } }
    }
}
