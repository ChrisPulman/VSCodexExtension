// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using ReactiveUI;
using VSCodex.Core.Models;

namespace VSCodex.Models;

/// <summary>Provides the codex Run Options implementation.</summary>
public sealed class CodexRunOptions : ReactiveObject
{
    /// <summary>Gets or sets the model.</summary>
    public string Model { get; set; } = CodexModelCatalog.DefaultModel;

    /// <summary>Gets or sets the failover Model.</summary>
    public string FailoverModel { get; set; } = CodexModelCatalog.DefaultFailoverModel;

    /// <summary>Gets or sets the reasoning Effort.</summary>
    public string ReasoningEffort { get; set; } = CodexModelCatalog.DefaultReasoningEffort;

    /// <summary>Gets or sets the verbosity.</summary>
    public string Verbosity { get; set; } = "medium";

    /// <summary>Gets or sets the service Tier.</summary>
    public string ServiceTier { get; set; } = "auto";

    /// <summary>Gets or sets the profile.</summary>
    public string Profile { get; set; } = "default";

    /// <summary>Gets or sets the approval Policy.</summary>
    public ApprovalPolicy ApprovalPolicy { get; set; } = ApprovalPolicy.OnRequest;

    /// <summary>Gets or sets the sandbox Mode.</summary>
    public SandboxMode SandboxMode { get; set; } = SandboxMode.WorkspaceWrite;

    /// <summary>Gets or sets the mode.</summary>
    public CodexRunMode Mode { get; set; } = CodexRunMode.Chat;

    /// <summary>Gets or sets the transport.</summary>
    public CodexTransportKind Transport { get; set; } = CodexTransportKind.SdkBridge;

    /// <summary>Gets or sets the include Workspace Context.</summary>
    public bool IncludeWorkspaceContext { get; set; } = true;

    /// <summary>Gets or sets the include Memory.</summary>
    public bool IncludeMemory { get; set; } = true;

    /// <summary>Gets or sets the include Skills.</summary>
    public bool IncludeSkills { get; set; } = true;

    /// <summary>Gets or sets the include Mcp Servers.</summary>
    public bool IncludeMcpServers { get; set; } = true;

    /// <summary>Gets or sets the use Multi Agent Orchestration.</summary>
    public bool UseMultiAgentOrchestration { get; set; }

    /// <summary>Gets or sets the max Agent Concurrency.</summary>
    public int MaxAgentConcurrency { get; set; } = 1;

    /// <summary>Gets or sets the agent Strategy.</summary>
    public AgentExecutionStrategy AgentStrategy { get; set; } = AgentExecutionStrategy.ReviewGate;

    /// <summary>Gets or sets the orchestration Model.</summary>
    public string OrchestrationModel { get; set; } = string.Empty;

    /// <summary>Gets or sets the budget Driven Model Selection.</summary>
    public bool BudgetDrivenModelSelection { get; set; }

    /// <summary>Gets or sets the budget Model.</summary>
    public string BudgetModel { get; set; } = string.Empty;
}
