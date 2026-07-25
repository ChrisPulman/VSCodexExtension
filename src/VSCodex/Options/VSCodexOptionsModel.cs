// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using VSCodex.Core.Models;
using VSCodex.Models;
using VSCodex.Services;

namespace VSCodex.Options;

/// <summary>Provides the vS Codex Options Model implementation.</summary>
public sealed class VSCodexOptionsModel
{
    /// <summary>Named number used by this type.</summary>
    private const double Numeric180D = 180D;

    /// <summary>Named string used by this type.</summary>
    private const string MediumText = "medium";

    /// <summary>Gets or sets the codex Cli Path.</summary>
    [Category("Runtime")]
    [DisplayName("Codex CLI path")]
    [Description("Path or command used for the local Codex CLI backend.")]
    [DefaultValue("codex")]
    public string CodexCliPath { get; set; } = "codex";

    /// <summary>Gets or sets the node Path.</summary>
    [Category("Runtime")]
    [DisplayName("Node path")]
    [Description("Path or command used to run the Codex SDK bridge.")]
    [DefaultValue("node")]
    public string NodePath { get; set; } = "node";

    /// <summary>Gets or sets the bridge Script Path.</summary>
    [Category("Runtime")]
    [DisplayName("Bridge script path")]
    [Description("Optional override for Resources\\codex-bridge.mjs.")]
    [DefaultValue("")]
    public string BridgeScriptPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the default Model.</summary>
    [Category("Models")]
    [DisplayName("Default model")]
    [Description("Primary interactive coding model.")]
    [DefaultValue("gpt-5.6-sol")]
    [TypeConverter(typeof(AvailableModelTypeConverter))]
    public string DefaultModel { get; set; } = CodexModelCatalog.DefaultModel;

    /// <summary>Gets or sets the default Failover Model.</summary>
    [Category("Models")]
    [DisplayName("Default failover model")]
    [Description("Fallback model used when the primary model is unavailable.")]
    [DefaultValue("gpt-5.6-terra")]
    [TypeConverter(typeof(AvailableModelTypeConverter))]
    public string DefaultFailoverModel { get; set; } = CodexModelCatalog.DefaultFailoverModel;

    /// <summary>Gets or sets the default Reasoning Effort.</summary>
    [Category("Models")]
    [DisplayName("Reasoning effort")]
    [Description("Default reasoning effort for model requests. Codex exposes low, medium, high, xhigh, and model-dependent max or ultra choices.")]
    [DefaultValue(MediumText)]
    [TypeConverter(typeof(ReasoningEffortTypeConverter))]
    public string DefaultReasoningEffort { get; set; } = MediumText;

    /// <summary>Gets or sets the default Verbosity.</summary>
    [Category("Models")]
    [DisplayName("Verbosity")]
    [Description("Default response verbosity. Typical values: low, medium, high.")]
    [DefaultValue(MediumText)]
    [TypeConverter(typeof(VerbosityTypeConverter))]
    public string DefaultVerbosity { get; set; } = MediumText;

    /// <summary>Gets or sets the default Service Tier.</summary>
    [Category("Models")]
    [DisplayName("Service tier")]
    [Description("Provider service tier, for example auto.")]
    [DefaultValue("auto")]
    public string DefaultServiceTier { get; set; } = "auto";

    /// <summary>Gets or sets the default Profile.</summary>
    [Category("Models")]
    [DisplayName("Profile")]
    [Description("Codex profile name.")]
    [DefaultValue("default")]
    public string DefaultProfile { get; set; } = "default";

    /// <summary>Gets or sets the custom Models.</summary>
    [Category("Models")]
    [DisplayName("Custom models")]
    [Description("One model id per line for the model picker.")]
    [RefreshProperties(RefreshProperties.All)]
    public string CustomModels { get; set; } = string.Empty;

    /// <summary>Gets or sets the default Approval Policy.</summary>
    [Category("Approvals and sandbox")]
    [DisplayName("Default approval policy")]
    [Description("Controls when VSCodex asks before edits or tool execution.")]
    [DefaultValue(ApprovalPolicy.OnRequest)]
    public ApprovalPolicy DefaultApprovalPolicy { get; set; } = ApprovalPolicy.OnRequest;

    /// <summary>Gets or sets the default Sandbox Mode.</summary>
    [Category("Approvals and sandbox")]
    [DisplayName("Default sandbox mode")]
    [Description("Controls filesystem/network access for agent runs.")]
    [DefaultValue(SandboxMode.WorkspaceWrite)]
    public SandboxMode DefaultSandboxMode { get; set; } = SandboxMode.WorkspaceWrite;

    /// <summary>Gets or sets the default Use Multi Agent Orchestration.</summary>
    [Category("Agents")]
    [DisplayName("Use multi-agent orchestration")]
    [Description("Enable multi-agent orchestration by default.")]
    [DefaultValue(false)]
    public bool DefaultUseMultiAgentOrchestration { get; set; }

    /// <summary>Gets or sets the default Max Agent Concurrency.</summary>
    [Category("Agents")]
    [DisplayName("Max agent concurrency")]
    [Description("Maximum number of VSCodex sub-agents to run concurrently.")]
    [DefaultValue(1)]
    public int DefaultMaxAgentConcurrency { get; set; } = 1;

    /// <summary>Gets or sets the default Agent Strategy.</summary>
    [Category("Agents")]
    [DisplayName("Agent strategy")]
    [Description("Default multi-agent handoff/review strategy.")]
    [DefaultValue(AgentExecutionStrategy.ReviewGate)]
    public AgentExecutionStrategy DefaultAgentStrategy { get; set; } = AgentExecutionStrategy.ReviewGate;

    /// <summary>Gets or sets the default Orchestration Model.</summary>
    [Category("Agents")]
    [DisplayName("Orchestration model")]
    [Description("Model used to split, route, and verify multi-agent tasks.")]
    [DefaultValue("gpt-5.6-sol")]
    [TypeConverter(typeof(AvailableModelTypeConverter))]
    public string DefaultOrchestrationModel { get; set; } = CodexModelCatalog.DefaultModel;

    /// <summary>Gets or sets the default Budget Driven Model Selection.</summary>
    [Category("Agents")]
    [DisplayName("Budget-driven model selection")]
    [Description("Use a lower-cost model when the task can safely run on the budget model.")]
    [DefaultValue(false)]
    public bool DefaultBudgetDrivenModelSelection { get; set; }

    /// <summary>Gets or sets the default Budget Model.</summary>
    [Category("Agents")]
    [DisplayName("Budget model")]
    [Description("Lower-cost model used when budget-driven selection is enabled.")]
    [DefaultValue("gpt-5.6-luna")]
    [TypeConverter(typeof(AvailableModelTypeConverter))]
    public string DefaultBudgetModel { get; set; } = CodexModelCatalog.DefaultBudgetModel;

    /// <summary>Gets or sets the default Follow Up Behavior.</summary>
    [Category("Context, skills, and memory")]
    [DisplayName("Follow-up behavior")]
    [Description("Choose whether Enter queues a follow-up or steers the active turn. Ctrl+Enter uses the opposite behavior.")]
    [DefaultValue(FollowUpBehavior.Queue)]
    public FollowUpBehavior DefaultFollowUpBehavior { get; set; } = FollowUpBehavior.Queue;

    /// <summary>Gets or sets the default Input Area Height.</summary>
    [Category("Context, skills, and memory")]
    [DisplayName("Input area height")]
    [Description("Default prompt editor height in the VSCodex tool window.")]
    [DefaultValue(Numeric180D)]
    public double DefaultInputAreaHeight { get; set; } = Numeric180D;

    /// <summary>Gets or sets the skill Roots.</summary>
    [Category("Context, skills, and memory")]
    [DisplayName("Skill roots")]
    [Description("One directory per line. VSCodex scans these for reusable coding skills.")]
    public string SkillRoots { get; set; } = string.Empty;

    /// <summary>Gets or sets the enabled Skill Paths.</summary>
    [Category("Context, skills, and memory")]
    [DisplayName("Enabled skills")]
    [Description("One enabled skill path per line.")]
    public string EnabledSkillPaths { get; set; } = string.Empty;

    /// <summary>Gets or sets the reactive Memory Server.</summary>
    [Category("Context, skills, and memory")]
    [DisplayName("ReactiveMemory MCP server")]
    [Description(
        "CP.ReactiveMemory.Mcp.Server is provisioned automatically as "
        + "[mcp_servers.cp-reactivememory-mcp-server] and is called before and after each request "
        + "so Visual Studio project context is preserved.")]
    [DefaultValue("cp-reactivememory-mcp-server")]
    [ReadOnly(true)]
    public string ReactiveMemoryServer { get; set; } = "cp-reactivememory-mcp-server";

    /// <summary>Gets the Visual Studio theme integration mode used by the extension.</summary>
    [Category("Appearance")]
    [DisplayName("Color theme")]
    [Description("VSCodex follows the active Visual Studio color theme, including high-contrast themes.")]
    [ReadOnly(true)]
    public string ColorTheme => "Follow Visual Studio";

    /// <summary>Loads from Settings Store.</summary>
    public void LoadFromSettingsStore()
    {
        var settings = new SettingsStore().Current;
        CodexCliPath = settings.CodexCliPath;
        NodePath = settings.NodePath;
        BridgeScriptPath = settings.BridgeScriptPath;
        DefaultModel = settings.DefaultModel;
        DefaultFailoverModel = settings.DefaultFailoverModel;
        DefaultReasoningEffort = settings.DefaultReasoningEffort;
        DefaultVerbosity = settings.DefaultVerbosity;
        DefaultServiceTier = settings.DefaultServiceTier;
        DefaultProfile = settings.DefaultProfile;
        CustomModels = JoinLines(settings.CustomModels);
        DefaultApprovalPolicy = settings.DefaultApprovalPolicy;
        DefaultSandboxMode = settings.DefaultSandboxMode;
        DefaultUseMultiAgentOrchestration = settings.DefaultUseMultiAgentOrchestration;
        DefaultMaxAgentConcurrency = settings.DefaultMaxAgentConcurrency;
        DefaultAgentStrategy = settings.DefaultAgentStrategy;
        DefaultOrchestrationModel = settings.DefaultOrchestrationModel;
        DefaultBudgetDrivenModelSelection = settings.DefaultBudgetDrivenModelSelection;
        DefaultBudgetModel = settings.DefaultBudgetModel;
        DefaultFollowUpBehavior = settings.DefaultFollowUpBehavior;
        DefaultInputAreaHeight = settings.DefaultInputAreaHeight;
        SkillRoots = JoinLines(settings.SkillRoots);
        EnabledSkillPaths = JoinLines(settings.EnabledSkillPaths);
        ReactiveMemoryServer = "cp-reactivememory-mcp-server";
    }

    /// <summary>Saves to Settings Store.</summary>
    public void SaveToSettingsStore()
    {
        var store = new SettingsStore();
        var settings = store.Current;
        settings.CodexCliPath = TrimOr(CodexCliPath, "codex");
        settings.NodePath = TrimOr(NodePath, "node");
        settings.BridgeScriptPath = TrimOr(BridgeScriptPath, string.Empty);
        settings.DefaultModel = TrimOr(DefaultModel, CodexModelCatalog.DefaultModel);
        settings.DefaultFailoverModel = TrimOr(DefaultFailoverModel, CodexModelCatalog.DefaultFailoverModel);
        settings.DefaultReasoningEffort = CodexModelCatalog.ResolveReasoningEffort(settings.DefaultModel, TrimOr(DefaultReasoningEffort, MediumText));
        settings.DefaultVerbosity = TrimOr(DefaultVerbosity, MediumText);
        settings.DefaultServiceTier = TrimOr(DefaultServiceTier, "auto");
        settings.DefaultProfile = TrimOr(DefaultProfile, "default");
        settings.CustomModels = Lines(CustomModels).ToList();
        settings.DefaultApprovalPolicy = DefaultApprovalPolicy;
        settings.DefaultSandboxMode = DefaultSandboxMode;
        settings.DefaultUseMultiAgentOrchestration = DefaultUseMultiAgentOrchestration;
        settings.DefaultMaxAgentConcurrency = Math.Max(1, DefaultMaxAgentConcurrency);
        settings.DefaultAgentStrategy = DefaultAgentStrategy;
        settings.DefaultOrchestrationModel = TrimOr(DefaultOrchestrationModel, settings.DefaultModel);
        settings.DefaultBudgetDrivenModelSelection = DefaultBudgetDrivenModelSelection;
        settings.DefaultBudgetModel = TrimOr(DefaultBudgetModel, CodexModelCatalog.DefaultBudgetModel);
        settings.DefaultFollowUpBehavior = DefaultFollowUpBehavior;
        settings.DefaultInputAreaHeight = DefaultInputAreaHeight > 0 ? DefaultInputAreaHeight : Numeric180D;
        settings.SkillRoots = Lines(SkillRoots).ToList();
        settings.EnabledSkillPaths = Lines(EnabledSkillPaths).ToList();
        store.Save(settings);
    }

    /// <summary>Performs the join Lines operation.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The join Lines result.</returns>
    private static string JoinLines(IEnumerable<string> values)
        => string.Join(Environment.NewLine, values ?? Enumerable.Empty<string>());

    /// <summary>Performs the lines operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The lines result.</returns>
    private static IEnumerable<string> Lines(string value)
        => (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Performs the trim Or operation.</summary>
    /// <param name="value">The value.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The trim Or result.</returns>
    private static string TrimOr(string? value, string fallback)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }
}
