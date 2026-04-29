using System;
using System.Collections.Generic;
using ReactiveUI;

namespace VSCodexExtension.Models
{
    public enum CodexRunMode { Chat, Plan, Build }
    public enum CodexMessageRole { System, User, Assistant, Tool, Approval, Error, Memory, Skill, Mcp }
    public enum ApprovalPolicy { Untrusted, OnFailure, OnRequest, Never }
    public enum SandboxMode { ReadOnly, WorkspaceWrite, DangerFullAccess }
    public enum CodexTransportKind { SdkBridge, CliFallback }

    public sealed class ChatMessage : ReactiveObject
    {
        private string _content = string.Empty;
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public CodexMessageRole Role { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
        public string Content { get => _content; set => this.RaiseAndSetIfChanged(ref _content, value); }
        public string? CorrelationId { get; set; }
    }

    public sealed class CodexAttachment : ReactiveObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Path { get; set; } = string.Empty;
        public string Kind { get; set; } = "file";
        public string DisplayName => System.IO.Path.GetFileName(Path);
    }

    public sealed class CodexRunOptions : ReactiveObject
    {
        public string Model { get; set; } = "gpt-5.4";
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
    }

    public sealed class CodexRunRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public string? ThreadId { get; set; }
        public string WorkspaceRoot { get; set; } = string.Empty;
        public CodexRunOptions Options { get; set; } = new CodexRunOptions();
        public IReadOnlyList<CodexAttachment> Attachments { get; set; } = Array.Empty<CodexAttachment>();
        public IReadOnlyList<SkillDefinition> Skills { get; set; } = Array.Empty<SkillDefinition>();
        public IReadOnlyList<MemoryEntry> Memories { get; set; } = Array.Empty<MemoryEntry>();
        public IReadOnlyList<McpServerDefinition> McpServers { get; set; } = Array.Empty<McpServerDefinition>();
        public IReadOnlyList<WorkspaceFileReference> WorkspaceFiles { get; set; } = Array.Empty<WorkspaceFileReference>();
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
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public List<string> Args { get; set; } = new List<string>();
        public Dictionary<string, string> Env { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public bool IsEnabled { get => _isEnabled; set => this.RaiseAndSetIfChanged(ref _isEnabled, value); }
        public string Health { get; set; } = "unknown";
    }

    public sealed class WorkspaceFileReference
    {
        public string Path { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
    }

    public sealed class CodexSessionDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string? ThreadId { get; set; }
        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset Updated { get; set; } = DateTimeOffset.Now;
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    public sealed class ExtensionSettings : ReactiveObject
    {
        public string CodexCliPath { get; set; } = "codex";
        public string NodePath { get; set; } = "node";
        public string BridgeScriptPath { get; set; } = string.Empty;
        public string DefaultModel { get; set; } = "gpt-5.4";
        public string DefaultReasoningEffort { get; set; } = "medium";
        public string DefaultVerbosity { get; set; } = "medium";
        public string DefaultServiceTier { get; set; } = "auto";
        public string DefaultProfile { get; set; } = "default";
        public ApprovalPolicy DefaultApprovalPolicy { get; set; } = ApprovalPolicy.OnRequest;
        public SandboxMode DefaultSandboxMode { get; set; } = SandboxMode.WorkspaceWrite;
        public List<string> CustomModels { get; set; } = new List<string> { "gpt-5.4", "gpt-5.4-codex", "gpt-5.1-codex" };
        public List<string> CustomReasoningEfforts { get; set; } = new List<string> { "minimal", "low", "medium", "high", "xhigh" };
        public List<string> CustomVerbosityOptions { get; set; } = new List<string> { "low", "medium", "high" };
        public List<string> SkillRoots { get; set; } = new List<string>();
    }
}
