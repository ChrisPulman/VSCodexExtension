using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using VSCodexExtension.Infrastructure;
using VSCodexExtension.Models;

namespace VSCodexExtension.Options
{
    public sealed class CodexOptionsPage : DialogPage
    {
        public CodexOptionsPage()
        {
            try
            {
                var settings = new JsonFileStore().ReadOrCreate<ExtensionSettings>(LocalPaths.SettingsFile);
                CodexCliPath = settings.CodexCliPath;
                NodePath = settings.NodePath;
                BridgeScriptPath = settings.BridgeScriptPath;
                DefaultModel = settings.DefaultModel;
                DefaultReasoningEffort = settings.DefaultReasoningEffort;
                DefaultVerbosity = settings.DefaultVerbosity;
                DefaultServiceTier = settings.DefaultServiceTier;
                DefaultProfile = settings.DefaultProfile;
                DefaultApprovalPolicy = settings.DefaultApprovalPolicy;
                DefaultSandboxMode = settings.DefaultSandboxMode;
                SkillRoots = string.Join(";", settings.SkillRoots);
                UseMultiAgentOrchestration = settings.DefaultUseMultiAgentOrchestration;
                MaxAgentConcurrency = settings.DefaultMaxAgentConcurrency;
                AgentStrategy = settings.DefaultAgentStrategy;
                OrchestrationModel = settings.DefaultOrchestrationModel;
                BudgetDrivenModelSelection = settings.DefaultBudgetDrivenModelSelection;
                BudgetModel = settings.DefaultBudgetModel;
                InputAreaHeight = settings.DefaultInputAreaHeight;
                AgentRoles = string.Join(Environment.NewLine, (settings.AgentRoles ?? new System.Collections.Generic.List<AgentRoleDefinition>()).Select(x => $"{x.Name}|{x.Role}|{x.Instructions}|{x.Model}|{x.ModelSelectionMode}"));
            }
            catch
            {
                // The Visual Studio option page must remain constructible even before local settings exist.
            }
        }

        [Category("Codex Runtime")]
        [DisplayName("Codex CLI Path")]
        [Description("Path to the local Codex CLI executable. Use 'codex' when it is on PATH.")]
        public string CodexCliPath { get; set; } = "codex";

        [Category("Codex Runtime")]
        [DisplayName("Node Path")]
        [Description("Path to node.exe used by the Codex SDK JSONL bridge. Use 'node' when it is on PATH.")]
        public string NodePath { get; set; } = "node";

        [Category("Codex Runtime")]
        [DisplayName("Bridge Script Path")]
        [Description("Optional explicit path to codex-bridge.mjs. Leave empty to use the VSIX embedded bridge.")]
        public string BridgeScriptPath { get; set; } = string.Empty;

        [Category("Defaults")]
        [DisplayName("Model")]
        public string DefaultModel { get; set; } = "gpt-5.4";

        [Category("Defaults")]
        [DisplayName("Reasoning Effort")]
        public string DefaultReasoningEffort { get; set; } = "medium";

        [Category("Defaults")]
        [DisplayName("Verbosity")]
        public string DefaultVerbosity { get; set; } = "medium";

        [Category("Defaults")]
        [DisplayName("Service Tier")]
        public string DefaultServiceTier { get; set; } = "auto";

        [Category("Defaults")]
        [DisplayName("Profile")]
        [Description("Codex profile name from %USERPROFILE%\\.codex\\config.toml.")]
        public string DefaultProfile { get; set; } = "default";

        [Category("Defaults")]
        [DisplayName("Approval Policy")]
        public ApprovalPolicy DefaultApprovalPolicy { get; set; } = ApprovalPolicy.OnRequest;

        [Category("Defaults")]
        [DisplayName("Sandbox Mode")]
        public SandboxMode DefaultSandboxMode { get; set; } = SandboxMode.WorkspaceWrite;

        [Category("Context")]
        [DisplayName("Skill Roots")]
        [Description("Semicolon-separated list of folders to scan recursively for SKILL.md files.")]
        public string SkillRoots { get; set; } = string.Empty;

        [Category("User Interface")]
        [DisplayName("Input Area Height")]
        [Description("Default prompt input area height in pixels. Runtime edits are persisted automatically.")]
        public double InputAreaHeight { get; set; } = 180d;

        [Category("Multi-Agent Orchestration")]
        [DisplayName("Use Multi-Agent Orchestration")]
        [Description("Default state for splitting large prompts into agent-assigned task sections.")]
        public bool UseMultiAgentOrchestration { get; set; }

        [Category("Multi-Agent Orchestration")]
        [DisplayName("Max Agent Concurrency")]
        [Description("Maximum logical agents to run at once. Workspace-write requests remain safest with 1.")]
        public int MaxAgentConcurrency { get; set; } = 1;

        [Category("Multi-Agent Orchestration")]
        [DisplayName("Agent Strategy")]
        public AgentExecutionStrategy AgentStrategy { get; set; } = AgentExecutionStrategy.ReviewGate;

        [Category("Multi-Agent Orchestration")]
        [DisplayName("Orchestration Model")]
        [Description("Model used by the main orchestration/coordinator AI.")]
        public string OrchestrationModel { get; set; } = "gpt-5.4-codex";

        [Category("Multi-Agent Orchestration")]
        [DisplayName("Budget Driven Model Selection")]
        [Description("When enabled, uses the budget model for main orchestration and sub-agents unless explicitly overridden at runtime.")]
        public bool BudgetDrivenModelSelection { get; set; }

        [Category("Multi-Agent Orchestration")]
        [DisplayName("Budget Model")]
        public string BudgetModel { get; set; } = "gpt-5.1-codex";

        [Category("Multi-Agent Orchestration")]
        [DisplayName("Agent Roles")]
        [Description("One role per line: Name|Role|Instructions|OptionalModel|ExplicitOrBudgetDriven")]
        public string AgentRoles { get; set; } = string.Empty;

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
            var settings = new JsonFileStore().ReadOrCreate<ExtensionSettings>(LocalPaths.SettingsFile);
            settings.CodexCliPath = string.IsNullOrWhiteSpace(CodexCliPath) ? "codex" : CodexCliPath.Trim();
            settings.NodePath = string.IsNullOrWhiteSpace(NodePath) ? "node" : NodePath.Trim();
            settings.BridgeScriptPath = BridgeScriptPath?.Trim() ?? string.Empty;
            settings.DefaultModel = string.IsNullOrWhiteSpace(DefaultModel) ? "gpt-5.4" : DefaultModel.Trim();
            settings.DefaultReasoningEffort = string.IsNullOrWhiteSpace(DefaultReasoningEffort) ? "medium" : DefaultReasoningEffort.Trim();
            settings.DefaultVerbosity = string.IsNullOrWhiteSpace(DefaultVerbosity) ? "medium" : DefaultVerbosity.Trim();
            settings.DefaultServiceTier = string.IsNullOrWhiteSpace(DefaultServiceTier) ? "auto" : DefaultServiceTier.Trim();
            settings.DefaultProfile = string.IsNullOrWhiteSpace(DefaultProfile) ? "default" : DefaultProfile.Trim();
            settings.DefaultApprovalPolicy = DefaultApprovalPolicy;
            settings.DefaultSandboxMode = DefaultSandboxMode;
            settings.SkillRoots = (SkillRoots ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (settings.SkillRoots.Count == 0)
            {
                settings.SkillRoots.Add(LocalPaths.UserSkillsRoot);
            }
            settings.DefaultUseMultiAgentOrchestration = UseMultiAgentOrchestration;
            settings.DefaultMaxAgentConcurrency = Math.Max(1, MaxAgentConcurrency);
            settings.DefaultAgentStrategy = AgentStrategy;
            settings.DefaultOrchestrationModel = string.IsNullOrWhiteSpace(OrchestrationModel) ? settings.DefaultModel : OrchestrationModel.Trim();
            settings.DefaultBudgetDrivenModelSelection = BudgetDrivenModelSelection;
            settings.DefaultBudgetModel = string.IsNullOrWhiteSpace(BudgetModel) ? settings.DefaultModel : BudgetModel.Trim();
            settings.DefaultInputAreaHeight = Math.Max(80d, Math.Min(600d, InputAreaHeight <= 0d ? 180d : InputAreaHeight));
            var parsedRoles = ParseAgentRoles(AgentRoles).ToList();
            if (parsedRoles.Count > 0)
            {
                settings.AgentRoles = parsedRoles;
            }
            new JsonFileStore().Write(LocalPaths.SettingsFile, settings);
        }
        private static System.Collections.Generic.IEnumerable<AgentRoleDefinition> ParseAgentRoles(string value)
        {
            foreach (var raw in (value ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = raw.Split(new[] { '|' }, 5);
                if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    var selectionMode = AgentModelSelectionMode.Explicit;
                    if (parts.Length == 5)
                    {
                        Enum.TryParse(parts[4].Trim(), true, out selectionMode);
                    }
                    yield return new AgentRoleDefinition
                    {
                        Name = parts[0].Trim(),
                        Role = parts[1].Trim(),
                        Instructions = parts[2].Trim(),
                        Model = parts.Length >= 4 ? parts[3].Trim() : string.Empty,
                        ModelSelectionMode = selectionMode,
                        IsEnabled = true
                    };
                }
            }
        }
    }
}
