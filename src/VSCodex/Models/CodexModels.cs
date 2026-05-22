using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ReactiveUI;

namespace VSCodex.Models;

public enum CodexRunMode { Chat, Plan, Build }
public enum CodexMessageRole { System, User, Assistant, Tool, Approval, Error, Memory, Skill, Mcp }
public enum ApprovalPolicy { Untrusted, OnFailure, OnRequest, Never }
public enum SandboxMode { ReadOnly, WorkspaceWrite, DangerFullAccess }
public enum CodexAccessLevel { ReadOnly, Workspace, FullAccess }
public enum CodexTransportKind { SdkBridge, CliFallback }
public enum AgentExecutionStrategy { Sequential, PlannerThenParallel, ReviewGate }
public enum AgentModelSelectionMode { Explicit, BudgetDriven }
public enum OrchestrationSectionStatus { Pending, Running, Completed, Failed, Cancelled }
public enum ModelTaskComplexity { Low, Medium, High }
public enum PrerequisiteState { Ready, Warning, Missing, Error }

public sealed class ModelProfile
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double InputPricePerMillion { get; set; }
    public double OutputPricePerMillion { get; set; }
    public int ContextWindowTokens { get; set; }
    public ModelTaskComplexity BestForComplexity { get; set; } = ModelTaskComplexity.Medium;
    public bool IsCodexOptimized { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class ModelUsageEstimate : ReactiveObject
{
    public int EstimatedInputTokens { get; set; }
    public int EstimatedOutputTokens { get; set; }
    public int ContextWindowTokens { get; set; }
    public int ContextRemainingTokens { get; set; }
    public int ContextUsedPercent { get; set; }
    public int ContextRemainingPercent { get; set; }
    public string PrimaryModel { get; set; } = string.Empty;
    public string FailoverModel { get; set; } = string.Empty;
    public string BudgetModel { get; set; } = string.Empty;
    public string RecommendedModel { get; set; } = string.Empty;
    public double PrimaryEstimatedCost { get; set; }
    public double BudgetEstimatedCost { get; set; }
    public double EstimatedSavingsPercent { get; set; }
    public ModelTaskComplexity Complexity { get; set; } = ModelTaskComplexity.Medium;
    public string RecommendationReason { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class RateLimitWindowStatus : ReactiveObject
{
    private string _remaining = "Waiting for Codex telemetry";
    private int _usagePercent;
    private string _resetText = string.Empty;

    public string Label { get; set; } = string.Empty;
    public string Remaining { get => _remaining; set => this.RaiseAndSetIfChanged(ref _remaining, value ?? string.Empty); }
    public int UsagePercent { get => _usagePercent; set => this.RaiseAndSetIfChanged(ref _usagePercent, Math.Max(0, Math.Min(100, value))); }
    public string ResetText { get => _resetText; set => this.RaiseAndSetIfChanged(ref _resetText, value ?? string.Empty); }
}

public sealed class PrerequisiteStatus : ReactiveObject
{
    private PrerequisiteState _state = PrerequisiteState.Missing;
    private string _status = string.Empty;
    private string _details = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PrerequisiteState State
    {
        get => _state;
        set
        {
            this.RaiseAndSetIfChanged(ref _state, value);
            this.RaisePropertyChanged(nameof(CanCopyCommand));
            this.RaisePropertyChanged(nameof(CanUpdate));
            this.RaisePropertyChanged(nameof(ActionButtonText));
        }
    }
    public string Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value ?? string.Empty); }
    public string Details { get => _details; set => this.RaiseAndSetIfChanged(ref _details, value ?? string.Empty); }
    public string InstallCommand { get; set; } = string.Empty;
    public string UpdateCommand { get; set; } = string.Empty;
    public bool IsBlocking { get; set; }
    public bool CanCopyCommand => State != PrerequisiteState.Ready && !string.IsNullOrWhiteSpace(ActionCommand);
    public bool CanUpdate => State != PrerequisiteState.Ready && !string.IsNullOrWhiteSpace(ActionCommand);
    public string ActionCommand => string.IsNullOrWhiteSpace(UpdateCommand) ? InstallCommand : UpdateCommand;
    public string ActionButtonText => State == PrerequisiteState.Missing || Status.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 ? "Install" : "Update";
}

public sealed class CodexEnvironmentReport
{
    public IReadOnlyList<PrerequisiteStatus> Items { get; set; } = Array.Empty<PrerequisiteStatus>();
    public bool IsSdkReady { get; set; }
    public bool IsCliReady { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public bool CanRunSdkBridge => IsSdkReady;
}

[JsonObject(MemberSerialization.OptOut)]
public sealed class AgentRoleDefinition : ReactiveObject
{
    private bool _isEnabled = true;
    private string _model = string.Empty;
    private AgentModelSelectionMode _modelSelectionMode = AgentModelSelectionMode.Explicit;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string Model { get => _model; set => this.RaiseAndSetIfChanged(ref _model, value ?? string.Empty); }
    public AgentModelSelectionMode ModelSelectionMode { get => _modelSelectionMode; set => this.RaiseAndSetIfChanged(ref _modelSelectionMode, value); }
    public bool IsEnabled { get => _isEnabled; set => this.RaiseAndSetIfChanged(ref _isEnabled, value); }
}

public sealed class OrchestrationTaskSection : ReactiveObject
{
    private OrchestrationSectionStatus _status = OrchestrationSectionStatus.Pending;
    private string _result = string.Empty;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AssignedAgent { get; set; } = string.Empty;
    public string DependsOnSectionId { get; set; } = string.Empty;
    public OrchestrationSectionStatus Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value); }
    public string Result { get => _result; set => this.RaiseAndSetIfChanged(ref _result, value); }
}

public sealed class OrchestrationRunPlan : ReactiveObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Goal { get; set; } = string.Empty;
    public AgentExecutionStrategy Strategy { get; set; } = AgentExecutionStrategy.ReviewGate;
    public List<AgentRoleDefinition> Agents { get; set; } = new List<AgentRoleDefinition>();
    public List<OrchestrationTaskSection> Sections { get; set; } = new List<OrchestrationTaskSection>();
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
}

public sealed class OrchestrationEvent
{
    public string Type { get; set; } = "status";
    public string Message { get; set; } = string.Empty;
    public string? PlanId { get; set; }
    public string? SectionId { get; set; }
    public OrchestrationTaskSection? Section { get; set; }
}

[JsonObject(MemberSerialization.OptOut)]
public sealed class ChatMessage : ReactiveObject
{
    private string _content = string.Empty;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public CodexMessageRole Role { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Content { get => _content; set => this.RaiseAndSetIfChanged(ref _content, value); }
    public string? CorrelationId { get; set; }
    public bool IsTransient { get; set; }
}

public sealed class CodexAttachment : ReactiveObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = string.Empty;
    public string Kind { get; set; } = "file";
    public string DisplayName => System.IO.Path.GetFileName(Path);
}

public sealed class PromptSuggestionItem : ReactiveObject
{
    public string Kind { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string InsertText { get; set; } = string.Empty;
    public string TargetTab { get; set; } = string.Empty;
}

public sealed class CodexRunOptions : ReactiveObject
{
    public string Model { get; set; } = "gpt-5.5";
    public string FailoverModel { get; set; } = "gpt-5.3-codex";
    public string ReasoningEffort { get; set; } = "medium";
    public string Verbosity { get; set; } = "medium";
    public string ServiceTier { get; set; } = "auto";
    public string Profile { get; set; } = "default";
    public ApprovalPolicy ApprovalPolicy { get; set; } = ApprovalPolicy.OnRequest;
    public SandboxMode SandboxMode { get; set; } = SandboxMode.WorkspaceWrite;
    public CodexRunMode Mode { get; set; } = CodexRunMode.Chat;
    public CodexTransportKind Transport { get; set; } = CodexTransportKind.SdkBridge;
    public bool IncludeWorkspaceContext { get; set; } = true;
    public bool IncludeMemory { get; set; } = true;
    public bool IncludeSkills { get; set; } = true;
    public bool IncludeMcpServers { get; set; } = true;
    public bool UseMultiAgentOrchestration { get; set; }
    public int MaxAgentConcurrency { get; set; } = 1;
    public AgentExecutionStrategy AgentStrategy { get; set; } = AgentExecutionStrategy.ReviewGate;
    public string OrchestrationModel { get; set; } = string.Empty;
    public bool BudgetDrivenModelSelection { get; set; }
    public string BudgetModel { get; set; } = string.Empty;
}

public sealed class CodexRunRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public string WorkspaceRoot { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public string WorkspaceSolutionPath { get; set; } = string.Empty;
    public string WorkspaceMemoryRoot { get; set; } = string.Empty;
    public WorkspaceIdentity WorkspaceIdentity { get; set; } = new WorkspaceIdentity();
    public CodexRunOptions Options { get; set; } = new CodexRunOptions();
    public IReadOnlyList<CodexAttachment> Attachments { get; set; } = Array.Empty<CodexAttachment>();
    public IReadOnlyList<SkillDefinition> Skills { get; set; } = Array.Empty<SkillDefinition>();
    public IReadOnlyList<MemoryEntry> Memories { get; set; } = Array.Empty<MemoryEntry>();
    public IReadOnlyList<McpServerDefinition> McpServers { get; set; } = Array.Empty<McpServerDefinition>();
    public IReadOnlyList<WorkspaceFileReference> WorkspaceFiles { get; set; } = Array.Empty<WorkspaceFileReference>();
    public IReadOnlyList<AgentRoleDefinition> AgentRoles { get; set; } = Array.Empty<AgentRoleDefinition>();
}

public sealed class WorkspaceIdentity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string SolutionPath { get; set; } = string.Empty;
    public string SolutionRelativePath { get; set; } = string.Empty;
    public string RepositoryRemote { get; set; } = string.Empty;
    public string MemoryRoot { get; set; } = string.Empty;
}

public sealed class CodexRunResult
{
    public string? ThreadId { get; set; }
    public string FinalResponse { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public bool UsedFallback { get; set; }
}

public sealed class CodexEvent
{
    public string Type { get; set; } = "message";
    public string Message { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public string RawJson { get; set; } = string.Empty;
}

public sealed class ApprovalRequest : ReactiveObject
{
    private bool _isPending = true;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsPreview { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsPending { get => _isPending; set => this.RaiseAndSetIfChanged(ref _isPending, value); }
}

public sealed class SkillDefinition : ReactiveObject
{
    private bool _isEnabled;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string MarkdownPath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsEnabled { get => _isEnabled; set => this.RaiseAndSetIfChanged(ref _isEnabled, value); }
}

public sealed class MemoryEntry : ReactiveObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = string.Empty;
    public string Scope { get; set; } = "user";
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.Now;
}

public sealed class McpServerDefinition : ReactiveObject
{
    private bool _isEnabled = true;
    private string _name = string.Empty;
    private string _transportType = "stdio";
    private string _command = string.Empty;
    private string _url = string.Empty;
    private string _argumentsText = string.Empty;
    private string _health = "unknown";
    public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value ?? string.Empty); }
    public string TransportType { get => _transportType; set => this.RaiseAndSetIfChanged(ref _transportType, string.IsNullOrWhiteSpace(value) ? "stdio" : value.Trim()); }
    public string Command { get => _command; set => this.RaiseAndSetIfChanged(ref _command, value ?? string.Empty); }
    public string Url { get => _url; set => this.RaiseAndSetIfChanged(ref _url, value ?? string.Empty); }
    public List<string> Args { get; set; } = new List<string>();
    public string ArgumentsText
    {
        get => string.IsNullOrWhiteSpace(_argumentsText) ? string.Join(Environment.NewLine, Args ?? new List<string>()) : _argumentsText;
        set
        {
            var text = value ?? string.Empty;
            this.RaiseAndSetIfChanged(ref _argumentsText, text);
            Args = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }
    }
    public Dictionary<string, string> Env { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public bool IsEnabled { get => _isEnabled; set => this.RaiseAndSetIfChanged(ref _isEnabled, value); }
    public string Health { get => _health; set => this.RaiseAndSetIfChanged(ref _health, value ?? string.Empty); }
    public string EndpointSummary => string.Equals(TransportType, "url", StringComparison.OrdinalIgnoreCase) ? Url : Command;
}

public sealed class McpToolInputField : ReactiveObject
{
    private string _value = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Value { get => _value; set => this.RaiseAndSetIfChanged(ref _value, value ?? string.Empty); }
    public string DisplayLabel => IsRequired ? Name : Name + " option";
}

public sealed class McpToolDefinition : ReactiveObject
{
    public string ServerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<McpToolInputField> InputFields { get; set; } = new List<McpToolInputField>();
    public string DisplayName => string.IsNullOrWhiteSpace(ServerName) ? Name : ServerName + "/" + Name;
}

public sealed class McpToolInvocation
{
    public string ToolName { get; set; } = string.Empty;
    public Newtonsoft.Json.Linq.JObject Arguments { get; set; } = new Newtonsoft.Json.Linq.JObject();
}

public sealed class CodingAssistantAction
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PromptTemplate { get; set; } = string.Empty;
}

public sealed class DebugContextSnapshot
{
    public string BreakReason { get; set; } = string.Empty;
    public string ExceptionDescription { get; set; } = string.Empty;
    public string StackSummary { get; set; } = string.Empty;
    public WorkspaceFileReference? Selection { get; set; }
}

public sealed class WorkspaceFileReference
{
    public string Path { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public string ReferenceKind { get; set; } = "file";
    public string ReferenceKey { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(ReferenceKey) ? RelativePath : ReferenceKey;
}

public sealed class CodexSessionDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? ThreadId { get; set; }
    public string WorkspaceIdentityId { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public string WorkspaceRoot { get; set; } = string.Empty;
    public string WorkspaceSolutionPath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.Now;
    public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public sealed class SessionHistoryItem : ReactiveObject
{
    private bool _isRenaming;
    private string _draftTitle = string.Empty;

    public string Id { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public string WorkspaceIdentityId { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public string WorkspaceRoot { get; set; } = string.Empty;
    public string WorkspaceSolutionPath { get; set; } = string.Empty;
    public DateTimeOffset Updated { get; set; }
    public int MessageCount { get; set; }
    public string UpdatedDisplay => Updated.ToLocalTime().ToString("g");
    public string MessageCountDisplay => MessageCount == 1 ? "1 message" : MessageCount + " messages";
    public string WorkspaceDisplay => string.IsNullOrWhiteSpace(WorkspaceName)
        ? string.IsNullOrWhiteSpace(WorkspaceRoot) ? "Current workspace" : WorkspaceRoot
        : WorkspaceName;
    public bool IsRenaming { get => _isRenaming; set => this.RaiseAndSetIfChanged(ref _isRenaming, value); }
    public string DraftTitle { get => _draftTitle; set => this.RaiseAndSetIfChanged(ref _draftTitle, value ?? string.Empty); }
}

[JsonObject(MemberSerialization.OptOut)]
public sealed class ExtensionSettings : ReactiveObject
{
    public string CodexCliPath { get; set; } = "codex";
    public string NodePath { get; set; } = "node";
    public string BridgeScriptPath { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = "gpt-5.5";
    public string DefaultFailoverModel { get; set; } = "gpt-5.3-codex";
    public string DefaultReasoningEffort { get; set; } = "medium";
    public string DefaultVerbosity { get; set; } = "medium";
    public string DefaultServiceTier { get; set; } = "auto";
    public string DefaultProfile { get; set; } = "default";
    public ApprovalPolicy DefaultApprovalPolicy { get; set; } = ApprovalPolicy.OnRequest;
    public SandboxMode DefaultSandboxMode { get; set; } = SandboxMode.WorkspaceWrite;
    public List<string> CustomModels { get; set; } = new List<string> { "gpt-5.5", "gpt-5.4", "gpt-5.4-mini", "gpt-5.3-codex", "gpt-5.2-codex", "gpt-5.1-codex", "gpt-5-codex" };
    public List<string> CustomReasoningEfforts { get; set; } = new List<string> { "minimal", "low", "medium", "high", "xhigh" };
    public List<string> CustomVerbosityOptions { get; set; } = new List<string> { "low", "medium", "high" };
    public List<string> SkillRoots { get; set; } = new List<string>();
    public List<string> EnabledSkillPaths { get; set; } = new List<string>();
    public bool DefaultUseMultiAgentOrchestration { get; set; }
    public int DefaultMaxAgentConcurrency { get; set; } = 1;
    public AgentExecutionStrategy DefaultAgentStrategy { get; set; } = AgentExecutionStrategy.ReviewGate;
    public string DefaultOrchestrationModel { get; set; } = "gpt-5.5";
    public bool DefaultBudgetDrivenModelSelection { get; set; }
    public string DefaultBudgetModel { get; set; } = "gpt-5.4-mini";
    public double DefaultInputAreaHeight { get; set; } = 180d;
    public List<AgentRoleDefinition> AgentRoles { get; set; } = new List<AgentRoleDefinition>
    {
        new AgentRoleDefinition { Name = "Planner", Role = "Planning", Instructions = "Split the request into safe, ordered sections with explicit acceptance criteria." },
        new AgentRoleDefinition { Name = "Architect", Role = "Architecture", Instructions = "Check design boundaries, dependencies, and integration risks before implementation." },
        new AgentRoleDefinition { Name = "Builder", Role = "Implementation", Instructions = "Implement the assigned section only, keep changes scoped, and report changed files." },
        new AgentRoleDefinition { Name = "Reviewer", Role = "Review", Instructions = "Review outputs for correctness, missing tests, safety, and build risk." },
        new AgentRoleDefinition { Name = "Verifier", Role = "Verification", Instructions = "Identify validation commands and summarize pass/fail evidence." }
    };
}
