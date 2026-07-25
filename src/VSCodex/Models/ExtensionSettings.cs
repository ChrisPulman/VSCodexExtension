// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using Newtonsoft.Json;
using ReactiveUI;
using VSCodex.Core.Models;

namespace VSCodex.Models;

/// <summary>Provides the extension Settings implementation.</summary>
[JsonObject(MemberSerialization.OptOut)]
public sealed class ExtensionSettings : ReactiveObject
{
    /// <summary>Named string used by this type.</summary>
    private const string MediumText = "medium";

    /// <summary>Gets or sets the codex Cli Path.</summary>
    public string CodexCliPath { get; set; } = "codex";

    /// <summary>Gets or sets the node Path.</summary>
    public string NodePath { get; set; } = "node";

    /// <summary>Gets or sets the bridge Script Path.</summary>
    public string BridgeScriptPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the default Model.</summary>
    public string DefaultModel { get; set; } = CodexModelCatalog.DefaultModel;

    /// <summary>Gets or sets the default Failover Model.</summary>
    public string DefaultFailoverModel { get; set; } = CodexModelCatalog.DefaultFailoverModel;

    /// <summary>Gets or sets the default Reasoning Effort.</summary>
    public string DefaultReasoningEffort { get; set; } = CodexModelCatalog.DefaultReasoningEffort;

    /// <summary>Gets or sets the default Verbosity.</summary>
    public string DefaultVerbosity { get; set; } = MediumText;

    /// <summary>Gets or sets the default Service Tier.</summary>
    public string DefaultServiceTier { get; set; } = "auto";

    /// <summary>Gets or sets the default Profile.</summary>
    public string DefaultProfile { get; set; } = "default";

    /// <summary>Gets or sets the default Approval Policy.</summary>
    public ApprovalPolicy DefaultApprovalPolicy { get; set; } = ApprovalPolicy.OnRequest;

    /// <summary>Gets or sets the default Sandbox Mode.</summary>
    public SandboxMode DefaultSandboxMode { get; set; } = SandboxMode.WorkspaceWrite;

    /// <summary>Gets or sets the custom Models.</summary>
    public List<string> CustomModels { get; set; } = new(CodexModelCatalog.SupportedModels);

    /// <summary>Gets or sets the custom Reasoning Efforts.</summary>
    public List<string> CustomReasoningEfforts { get; set; } = new(CodexModelCatalog.ReasoningEfforts);

    /// <summary>Gets or sets the custom Verbosity Options.</summary>
    public List<string> CustomVerbosityOptions { get; set; } = new List<string> { "low", MediumText, "high" };

    /// <summary>Gets or sets the skill Roots.</summary>
    public List<string> SkillRoots { get; set; } = new();

    /// <summary>Gets or sets the enabled Skill Paths.</summary>
    public List<string> EnabledSkillPaths { get; set; } = new();

    /// <summary>Gets or sets the default Use Multi Agent Orchestration.</summary>
    public bool DefaultUseMultiAgentOrchestration { get; set; }

    /// <summary>Gets or sets the default Max Agent Concurrency.</summary>
    public int DefaultMaxAgentConcurrency { get; set; } = 1;

    /// <summary>Gets or sets the default Agent Strategy.</summary>
    public AgentExecutionStrategy DefaultAgentStrategy { get; set; } = AgentExecutionStrategy.ReviewGate;

    /// <summary>Gets or sets the default Orchestration Model.</summary>
    public string DefaultOrchestrationModel { get; set; } = CodexModelCatalog.DefaultModel;

    /// <summary>Gets or sets the default Budget Driven Model Selection.</summary>
    public bool DefaultBudgetDrivenModelSelection { get; set; }

    /// <summary>Gets or sets the default Budget Model.</summary>
    public string DefaultBudgetModel { get; set; } = CodexModelCatalog.DefaultBudgetModel;

    /// <summary>Gets or sets the default Follow Up Behavior.</summary>
    public FollowUpBehavior DefaultFollowUpBehavior { get; set; } = FollowUpBehavior.Queue;

    /// <summary>Gets or sets the default Input Area Height.</summary>
    public double DefaultInputAreaHeight { get; set; } = 180D;

    /// <summary>Gets or sets the agent Roles.</summary>
    public List<AgentRoleDefinition> AgentRoles { get; set; } = new List<AgentRoleDefinition>
    {
        new AgentRoleDefinition { Name = "Planner", Role = "Planning", Instructions = "Split the request into safe, ordered sections with explicit acceptance criteria." },
        new AgentRoleDefinition { Name = "Architect", Role = "Architecture", Instructions = "Check design boundaries, dependencies, and integration risks before implementation." },
        new AgentRoleDefinition { Name = "Builder", Role = "Implementation", Instructions = "Implement the assigned section only, keep changes scoped, and report changed files." },
        new AgentRoleDefinition { Name = "Reviewer", Role = "Review", Instructions = "Review outputs for correctness, missing tests, safety, and build risk." },
        new AgentRoleDefinition { Name = "Verifier", Role = "Verification", Instructions = "Identify validation commands and summarize pass/fail evidence." }
    };
}
