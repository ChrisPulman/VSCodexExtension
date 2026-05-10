using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Community.VisualStudio.Toolkit;
using VSCodex.Models;
using VSCodex.Services;

namespace VSCodex.Options;

public partial class OptionsProvider
{
    [ComVisible(true)]
    [Guid("C7A22742-22C0-423B-A0DF-9C2D2F6D7DB1")]
    public sealed class GeneralOptions : BaseOptionPage<VSCodexOptionsModel>
    {
        public override void LoadSettingsFromStorage()
        {
            base.LoadSettingsFromStorage();
            if (AutomationObject is VSCodexOptionsModel model)
            {
                model.LoadFromSettingsStore();
            }
        }

        public override void SaveSettingsToStorage()
        {
            if (AutomationObject is VSCodexOptionsModel model)
            {
                model.SaveToSettingsStore();
            }

            base.SaveSettingsToStorage();
        }
    }
}

public sealed class VSCodexOptionsModel : BaseOptionModel<VSCodexOptionsModel>
{
    [Category("Runtime")]
    [DisplayName("Codex CLI path")]
    [Description("Path or command used for the local Codex CLI backend.")]
    [DefaultValue("codex")]
    public string CodexCliPath { get; set; } = "codex";

    [Category("Runtime")]
    [DisplayName("Node path")]
    [Description("Path or command used to run the Codex SDK bridge.")]
    [DefaultValue("node")]
    public string NodePath { get; set; } = "node";

    [Category("Runtime")]
    [DisplayName("Bridge script path")]
    [Description("Optional override for Resources\\codex-bridge.mjs.")]
    [DefaultValue("")]
    public string BridgeScriptPath { get; set; } = string.Empty;

    [Category("Models")]
    [DisplayName("Default model")]
    [Description("Primary interactive coding model.")]
    [DefaultValue("gpt-5.5")]
    [TypeConverter(typeof(AvailableModelTypeConverter))]
    public string DefaultModel { get; set; } = "gpt-5.5";

    [Category("Models")]
    [DisplayName("Default failover model")]
    [Description("Fallback model used when the primary model is unavailable.")]
    [DefaultValue("gpt-5.3-codex")]
    [TypeConverter(typeof(AvailableModelTypeConverter))]
    public string DefaultFailoverModel { get; set; } = "gpt-5.3-codex";

    [Category("Models")]
    [DisplayName("Reasoning effort")]
    [Description("Default reasoning effort for model requests. Typical values: minimal, low, medium, high, xhigh.")]
    [DefaultValue("medium")]
    [TypeConverter(typeof(ReasoningEffortTypeConverter))]
    public string DefaultReasoningEffort { get; set; } = "medium";

    [Category("Models")]
    [DisplayName("Verbosity")]
    [Description("Default response verbosity. Typical values: low, medium, high.")]
    [DefaultValue("medium")]
    [TypeConverter(typeof(VerbosityTypeConverter))]
    public string DefaultVerbosity { get; set; } = "medium";

    [Category("Models")]
    [DisplayName("Service tier")]
    [Description("Provider service tier, for example auto.")]
    [DefaultValue("auto")]
    public string DefaultServiceTier { get; set; } = "auto";

    [Category("Models")]
    [DisplayName("Profile")]
    [Description("Codex profile name.")]
    [DefaultValue("default")]
    public string DefaultProfile { get; set; } = "default";

    [Category("Models")]
    [DisplayName("Custom models")]
    [Description("One model id per line for the model picker.")]
    [RefreshProperties(RefreshProperties.All)]
    public string CustomModels { get; set; } = string.Empty;

    [Category("Approvals and sandbox")]
    [DisplayName("Default approval policy")]
    [Description("Controls when VSCodex asks before edits or tool execution.")]
    [DefaultValue(ApprovalPolicy.OnRequest)]
    public ApprovalPolicy DefaultApprovalPolicy { get; set; } = ApprovalPolicy.OnRequest;

    [Category("Approvals and sandbox")]
    [DisplayName("Default sandbox mode")]
    [Description("Controls filesystem/network access for agent runs.")]
    [DefaultValue(SandboxMode.WorkspaceWrite)]
    public SandboxMode DefaultSandboxMode { get; set; } = SandboxMode.WorkspaceWrite;

    [Category("Agents")]
    [DisplayName("Use multi-agent orchestration")]
    [Description("Enable multi-agent orchestration by default.")]
    [DefaultValue(false)]
    public bool DefaultUseMultiAgentOrchestration { get; set; }

    [Category("Agents")]
    [DisplayName("Max agent concurrency")]
    [Description("Maximum number of VSCodex sub-agents to run concurrently.")]
    [DefaultValue(1)]
    public int DefaultMaxAgentConcurrency { get; set; } = 1;

    [Category("Agents")]
    [DisplayName("Agent strategy")]
    [Description("Default multi-agent handoff/review strategy.")]
    [DefaultValue(AgentExecutionStrategy.ReviewGate)]
    public AgentExecutionStrategy DefaultAgentStrategy { get; set; } = AgentExecutionStrategy.ReviewGate;

    [Category("Agents")]
    [DisplayName("Orchestration model")]
    [Description("Model used to split, route, and verify multi-agent tasks.")]
    [DefaultValue("gpt-5.5")]
    [TypeConverter(typeof(AvailableModelTypeConverter))]
    public string DefaultOrchestrationModel { get; set; } = "gpt-5.5";

    [Category("Agents")]
    [DisplayName("Budget-driven model selection")]
    [Description("Use a lower-cost model when the task can safely run on the budget model.")]
    [DefaultValue(false)]
    public bool DefaultBudgetDrivenModelSelection { get; set; }

    [Category("Agents")]
    [DisplayName("Budget model")]
    [Description("Lower-cost model used when budget-driven selection is enabled.")]
    [DefaultValue("gpt-5.4-mini")]
    [TypeConverter(typeof(AvailableModelTypeConverter))]
    public string DefaultBudgetModel { get; set; } = "gpt-5.4-mini";

    [Category("Context, skills, and memory")]
    [DisplayName("Input area height")]
    [Description("Default prompt editor height in the VSCodex tool window.")]
    [DefaultValue(180d)]
    public double DefaultInputAreaHeight { get; set; } = 180d;

    [Category("Context, skills, and memory")]
    [DisplayName("Skill roots")]
    [Description("One directory per line. VSCodex scans these for reusable coding skills.")]
    public string SkillRoots { get; set; } = string.Empty;

    [Category("Context, skills, and memory")]
    [DisplayName("Enabled skills")]
    [Description("One enabled skill path per line.")]
    public string EnabledSkillPaths { get; set; } = string.Empty;

    [Category("Context, skills, and memory")]
    [DisplayName("ReactiveMemory MCP server")]
    [Description("CP.ReactiveMemory.Mcp.Server is provisioned automatically as [mcp_servers.reactivememory] and is called before and after each request so Visual Studio project context is preserved.")]
    [DefaultValue("CP.ReactiveMemory.Mcp.Server")]
    public string ReactiveMemoryServer { get; set; } = "CP.ReactiveMemory.Mcp.Server";

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
        DefaultInputAreaHeight = settings.DefaultInputAreaHeight;
        SkillRoots = JoinLines(settings.SkillRoots);
        EnabledSkillPaths = JoinLines(settings.EnabledSkillPaths);
        ReactiveMemoryServer = "CP.ReactiveMemory.Mcp.Server";
    }

    public void SaveToSettingsStore()
    {
        var store = new SettingsStore();
        var settings = store.Current;
        settings.CodexCliPath = TrimOr(CodexCliPath, "codex");
        settings.NodePath = TrimOr(NodePath, "node");
        settings.BridgeScriptPath = TrimOr(BridgeScriptPath, string.Empty);
        settings.DefaultModel = TrimOr(DefaultModel, "gpt-5.5");
        settings.DefaultFailoverModel = TrimOr(DefaultFailoverModel, "gpt-5.3-codex");
        settings.DefaultReasoningEffort = TrimOr(DefaultReasoningEffort, "medium");
        settings.DefaultVerbosity = TrimOr(DefaultVerbosity, "medium");
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
        settings.DefaultBudgetModel = TrimOr(DefaultBudgetModel, "gpt-5.4-mini");
        settings.DefaultInputAreaHeight = DefaultInputAreaHeight > 0 ? DefaultInputAreaHeight : 180d;
        settings.SkillRoots = Lines(SkillRoots).ToList();
        settings.EnabledSkillPaths = Lines(EnabledSkillPaths).ToList();
        store.Save(settings);
    }

    private static string JoinLines(System.Collections.Generic.IEnumerable<string> values)
        => string.Join(Environment.NewLine, values ?? Enumerable.Empty<string>());

    private static System.Collections.Generic.IEnumerable<string> Lines(string value)
        => (value ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static string TrimOr(string? value, string fallback)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
    }
}

public sealed class AvailableModelTypeConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        => new StandardValuesCollection(OptionsStandardValues.GetAvailableModels(context).ToArray());
}

public sealed class ReasoningEffortTypeConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        => new StandardValuesCollection(OptionsStandardValues.GetReasoningEfforts(context).ToArray());
}

public sealed class VerbosityTypeConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        => new StandardValuesCollection(OptionsStandardValues.GetVerbosityOptions(context).ToArray());
}

internal static class OptionsStandardValues
{
    private static readonly string[] DefaultModels =
    {
        "gpt-5.5",
        "gpt-5.4",
        "gpt-5.4-mini",
        "gpt-5.3-codex",
        "gpt-5.2-codex",
        "gpt-5.1-codex",
        "gpt-5-codex"
    };

    private static readonly string[] DefaultReasoningEfforts = { "minimal", "low", "medium", "high", "xhigh" };
    private static readonly string[] DefaultVerbosityOptions = { "low", "medium", "high" };

    public static IEnumerable<string> GetAvailableModels(ITypeDescriptorContext? context)
    {
        var values = new List<string>();
        if (context?.Instance is VSCodexOptionsModel model)
        {
            values.AddRange(Lines(model.CustomModels));
            values.Add(model.DefaultModel);
            values.Add(model.DefaultFailoverModel);
            values.Add(model.DefaultOrchestrationModel);
            values.Add(model.DefaultBudgetModel);
        }

        var settings = TryGetSettings();
        if (settings != null)
        {
            values.AddRange(settings.CustomModels ?? Enumerable.Empty<string>());
            values.Add(settings.DefaultModel);
            values.Add(settings.DefaultFailoverModel);
            values.Add(settings.DefaultOrchestrationModel);
            values.Add(settings.DefaultBudgetModel);
        }

        values.AddRange(DefaultModels);
        return DistinctValues(values);
    }

    public static IEnumerable<string> GetReasoningEfforts(ITypeDescriptorContext? context)
    {
        var values = new List<string>();
        if (context?.Instance is VSCodexOptionsModel model)
        {
            values.Add(model.DefaultReasoningEffort);
        }

        var settings = TryGetSettings();
        if (settings != null)
        {
            values.AddRange(settings.CustomReasoningEfforts ?? Enumerable.Empty<string>());
            values.Add(settings.DefaultReasoningEffort);
        }

        values.AddRange(DefaultReasoningEfforts);
        return DistinctValues(values);
    }

    public static IEnumerable<string> GetVerbosityOptions(ITypeDescriptorContext? context)
    {
        var values = new List<string>();
        if (context?.Instance is VSCodexOptionsModel model)
        {
            values.Add(model.DefaultVerbosity);
        }

        var settings = TryGetSettings();
        if (settings != null)
        {
            values.AddRange(settings.CustomVerbosityOptions ?? Enumerable.Empty<string>());
            values.Add(settings.DefaultVerbosity);
        }

        values.AddRange(DefaultVerbosityOptions);
        return DistinctValues(values);
    }

    private static ExtensionSettings? TryGetSettings()
    {
        try
        {
            return new SettingsStore().Current;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> Lines(string value)
        => (value ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim());

    private static IEnumerable<string> DistinctValues(IEnumerable<string> values)
        => values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
