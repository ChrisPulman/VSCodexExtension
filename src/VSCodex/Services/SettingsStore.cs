using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

public interface ISettingsStore
{
    IObservable<ExtensionSettings> SettingsChanged { get; }
    ExtensionSettings Current { get; }
    void Save(ExtensionSettings settings);
    ExtensionSettings LoadForWorkspace(WorkspaceIdentity identity);
    void SaveForWorkspace(WorkspaceIdentity identity, ExtensionSettings settings);
}
public sealed class SettingsStore : ISettingsStore
{
    private static readonly object Sync = new object();
    private static readonly JsonFileStore Store = new JsonFileStore();
    private static BehaviorSubject<ExtensionSettings>? SharedSettings;
    private readonly BehaviorSubject<ExtensionSettings> _settings;
    public SettingsStore()
    {
        lock (Sync)
        {
            if (SharedSettings == null)
            {
                var settings = Store.ReadOrCreate<ExtensionSettings>(LocalPaths.SettingsFile);
                Normalize(settings);
                if (settings.SkillRoots.Count == 0) settings.SkillRoots.Add(LocalPaths.UserSkillsRoot);
                Store.Write(LocalPaths.SettingsFile, settings);
                SharedSettings = new BehaviorSubject<ExtensionSettings>(settings);
            }

            _settings = SharedSettings;
        }
    }
    public IObservable<ExtensionSettings> SettingsChanged => _settings.AsObservable();
    public ExtensionSettings Current => _settings.Value;
    public void Save(ExtensionSettings settings)
    {
        lock (Sync)
        {
            Normalize(settings);
            Store.Write(LocalPaths.SettingsFile, settings);
            SynchronizeWorkspaceExecutionDefaults(settings);
            _settings.OnNext(settings);
        }
    }

    public ExtensionSettings LoadForWorkspace(WorkspaceIdentity identity)
    {
        if (identity == null || string.IsNullOrWhiteSpace(identity.Id))
        {
            return Current;
        }

        lock (Sync)
        {
            var path = LocalPaths.WorkspaceSettingsFile(identity.Id);
            if (!File.Exists(path))
            {
                return Current;
            }

            var settings = Store.ReadOrCreate<ExtensionSettings>(path);
            var globalSettings = Store.ReadOrCreate<ExtensionSettings>(LocalPaths.SettingsFile);
            Normalize(globalSettings);
            MergeGlobalExecutionDefaults(settings, globalSettings);
            Normalize(settings);
            Store.Write(path, settings);
            _settings.OnNext(settings);
            return settings;
        }
    }

    public void SaveForWorkspace(WorkspaceIdentity identity, ExtensionSettings settings)
    {
        if (identity == null || string.IsNullOrWhiteSpace(identity.Id))
        {
            Save(settings);
            return;
        }

        lock (Sync)
        {
            Normalize(settings);
            SaveGlobalExecutionDefaults(settings);
            Store.Write(LocalPaths.WorkspaceSettingsFile(identity.Id), settings);
            _settings.OnNext(settings);
        }
    }

    private static void SaveGlobalExecutionDefaults(ExtensionSettings settings)
    {
        var globalSettings = Store.ReadOrCreate<ExtensionSettings>(LocalPaths.SettingsFile);
        MergeGlobalExecutionDefaults(globalSettings, settings);
        Normalize(globalSettings);
        Store.Write(LocalPaths.SettingsFile, globalSettings);
    }

    private static void SynchronizeWorkspaceExecutionDefaults(ExtensionSettings settings)
    {
        if (!Directory.Exists(LocalPaths.WorkspaceStateRoot))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(LocalPaths.WorkspaceStateRoot, "settings.json", SearchOption.AllDirectories))
        {
            var workspaceSettings = Store.ReadOrCreate<ExtensionSettings>(path);
            MergeGlobalExecutionDefaults(workspaceSettings, settings);
            Normalize(workspaceSettings);
            Store.Write(path, workspaceSettings);
        }
    }

    private static void MergeGlobalExecutionDefaults(ExtensionSettings target, ExtensionSettings source)
    {
        target.CodexCliPath = source.CodexCliPath;
        target.NodePath = source.NodePath;
        target.BridgeScriptPath = source.BridgeScriptPath;
        target.DefaultModel = source.DefaultModel;
        target.DefaultFailoverModel = source.DefaultFailoverModel;
        target.DefaultReasoningEffort = source.DefaultReasoningEffort;
        target.DefaultVerbosity = source.DefaultVerbosity;
        target.DefaultServiceTier = source.DefaultServiceTier;
        target.DefaultProfile = source.DefaultProfile;
        target.DefaultApprovalPolicy = source.DefaultApprovalPolicy;
        target.DefaultSandboxMode = source.DefaultSandboxMode;
        target.CustomModels = source.CustomModels?.ToList() ?? new List<string>();
        target.CustomReasoningEfforts = source.CustomReasoningEfforts?.ToList() ?? new List<string>();
        target.CustomVerbosityOptions = source.CustomVerbosityOptions?.ToList() ?? new List<string>();
        target.DefaultUseMultiAgentOrchestration = source.DefaultUseMultiAgentOrchestration;
        target.DefaultMaxAgentConcurrency = source.DefaultMaxAgentConcurrency;
        target.DefaultAgentStrategy = source.DefaultAgentStrategy;
        target.DefaultOrchestrationModel = source.DefaultOrchestrationModel;
        target.DefaultBudgetDrivenModelSelection = source.DefaultBudgetDrivenModelSelection;
        target.DefaultBudgetModel = source.DefaultBudgetModel;
        target.DefaultInputAreaHeight = source.DefaultInputAreaHeight;
        target.AgentRoles = (source.AgentRoles ?? new List<AgentRoleDefinition>()).Select(CloneAgentRole).ToList();
    }

    private static void Normalize(ExtensionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DefaultModel) || settings.DefaultModel.Equals("gpt-5.4-codex", StringComparison.OrdinalIgnoreCase))
        {
            settings.DefaultModel = "gpt-5.5";
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultFailoverModel))
        {
            settings.DefaultFailoverModel = "gpt-5.3-codex";
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultOrchestrationModel) || settings.DefaultOrchestrationModel.Equals("gpt-5.4-codex", StringComparison.OrdinalIgnoreCase))
        {
            settings.DefaultOrchestrationModel = settings.DefaultModel;
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultBudgetModel) || settings.DefaultBudgetModel.Equals("gpt-5.1-codex", StringComparison.OrdinalIgnoreCase))
        {
            settings.DefaultBudgetModel = "gpt-5.4-mini";
        }

        settings.SkillRoots = (settings.SkillRoots ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.EnabledSkillPaths = (settings.EnabledSkillPaths ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var defaults = new[] { "gpt-5.5", "gpt-5.4", "gpt-5.4-mini", "gpt-5.3-codex", "gpt-5.2-codex", "gpt-5.1-codex", "gpt-5-codex" };
        settings.CustomModels = (settings.CustomModels ?? new List<string>())
            .Concat(defaults)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.CustomReasoningEfforts = NormalizeStringOptions(settings.CustomReasoningEfforts, new[] { "minimal", "low", "medium", "high", "xhigh" });
        settings.CustomVerbosityOptions = NormalizeStringOptions(settings.CustomVerbosityOptions, new[] { "low", "medium", "high" });

        settings.AgentRoles = NormalizeAgentRoles(settings.AgentRoles);
    }

    private static List<string> NormalizeStringOptions(IEnumerable<string>? values, IEnumerable<string> defaults)
        => (values ?? Enumerable.Empty<string>())
            .Concat(defaults)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<AgentRoleDefinition> NormalizeAgentRoles(IEnumerable<AgentRoleDefinition>? roles)
    {
        var defaults = new ExtensionSettings().AgentRoles;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new List<AgentRoleDefinition>();
        foreach (var role in (roles ?? Enumerable.Empty<AgentRoleDefinition>()).Concat(defaults))
        {
            var key = string.IsNullOrWhiteSpace(role.Name) ? role.Role : role.Name;
            key = (key ?? string.Empty).Trim();
            if (key.Length == 0 || !seen.Add(key))
            {
                continue;
            }

            normalized.Add(CloneAgentRole(role));
        }

        return normalized;
    }

    private static AgentRoleDefinition CloneAgentRole(AgentRoleDefinition role)
    {
        return new AgentRoleDefinition
        {
            Name = (role.Name ?? string.Empty).Trim(),
            Role = (role.Role ?? string.Empty).Trim(),
            Instructions = (role.Instructions ?? string.Empty).Trim(),
            Model = role.Model,
            ModelSelectionMode = role.ModelSelectionMode,
            IsEnabled = role.IsEnabled
        };
    }
}
