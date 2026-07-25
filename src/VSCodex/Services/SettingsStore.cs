// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VSCodex.Core.Models;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the Settings Store implementation.</summary>
public sealed class SettingsStore : ISettingsStore
{
    /// <summary>Stores the sync.</summary>
    private static readonly object Sync = new();

    /// <summary>Stores the store.</summary>
    private static readonly JsonFileStore Store = new();

    /// <summary>Stores the shared Settings.</summary>
    private static BehaviorSubject<ExtensionSettings>? _sharedSettings;

    /// <summary>Stores the settings.</summary>
    private readonly BehaviorSubject<ExtensionSettings> _settings;

    /// <summary>Initializes a new instance of the <see cref="SettingsStore"/> class.</summary>
    public SettingsStore()
    {
        lock (Sync)
        {
            if (_sharedSettings is null)
            {
                ExtensionSettings settings = Store.ReadOrCreate<ExtensionSettings>(LocalPaths.SettingsFile);
                Normalize(settings);
                if (settings.SkillRoots.Count == 0)
                {
                    settings.SkillRoots.Add(LocalPaths.UserSkillsRoot);
                }

                Store.Write(LocalPaths.SettingsFile, settings);
                _sharedSettings = new(settings);
            }

            _settings = _sharedSettings;
        }
    }

    /// <summary>Gets the settings Changed.</summary>
    public IObservable<ExtensionSettings> SettingsChanged => _settings.AsObservable();

    /// <summary>Gets the current.</summary>
    public ExtensionSettings Current => _settings.Value;

    /// <summary>Saves the operation.</summary>
    /// <param name="settings">The settings.</param>
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

    /// <summary>Loads for Workspace.</summary>
    /// <param name="identity">The identity.</param>
    /// <returns>The load For Workspace result.</returns>
    public ExtensionSettings LoadForWorkspace(WorkspaceIdentity identity)
    {
        if (identity is null || string.IsNullOrWhiteSpace(identity.Id))
        {
            return Current;
        }

        lock (Sync)
        {
            string path = LocalPaths.WorkspaceSettingsFile(identity.Id);
            if (!File.Exists(path))
            {
                return Current;
            }

            ExtensionSettings settings = Store.ReadOrCreate<ExtensionSettings>(path);
            ExtensionSettings globalSettings = Store.ReadOrCreate<ExtensionSettings>(LocalPaths.SettingsFile);
            Normalize(globalSettings);
            MergeGlobalExecutionDefaults(settings, globalSettings);
            Normalize(settings);
            Store.Write(path, settings);
            _settings.OnNext(settings);
            return settings;
        }
    }

    /// <summary>Saves for Workspace.</summary>
    /// <param name="identity">The identity.</param>
    /// <param name="settings">The settings.</param>
    public void SaveForWorkspace(WorkspaceIdentity identity, ExtensionSettings settings)
    {
        if (identity is null || string.IsNullOrWhiteSpace(identity.Id))
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

    /// <summary>Saves global Execution Defaults.</summary>
    /// <param name="settings">The settings.</param>
    private static void SaveGlobalExecutionDefaults(ExtensionSettings settings)
    {
        ExtensionSettings globalSettings = Store.ReadOrCreate<ExtensionSettings>(LocalPaths.SettingsFile);
        MergeGlobalExecutionDefaults(globalSettings, settings);
        Normalize(globalSettings);
        Store.Write(LocalPaths.SettingsFile, globalSettings);
    }

    /// <summary>Performs the synchronize Workspace Execution Defaults operation.</summary>
    /// <param name="settings">The settings.</param>
    private static void SynchronizeWorkspaceExecutionDefaults(ExtensionSettings settings)
    {
        if (!Directory.Exists(LocalPaths.WorkspaceStateRoot))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(LocalPaths.WorkspaceStateRoot, "settings.json", SearchOption.AllDirectories))
        {
            ExtensionSettings workspaceSettings = Store.ReadOrCreate<ExtensionSettings>(path);
            MergeGlobalExecutionDefaults(workspaceSettings, settings);
            Normalize(workspaceSettings);
            Store.Write(path, workspaceSettings);
        }
    }

    /// <summary>Performs the merge Global Execution Defaults operation.</summary>
    /// <param name="target">The target.</param>
    /// <param name="source">The source.</param>
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
        target.DefaultFollowUpBehavior = source.DefaultFollowUpBehavior;
        target.DefaultInputAreaHeight = source.DefaultInputAreaHeight;
        target.AgentRoles = (source.AgentRoles ?? new List<AgentRoleDefinition>()).Select(CloneAgentRole).ToList();
    }

    /// <summary>Performs the normalize operation.</summary>
    /// <param name="settings">The settings.</param>
    private static void Normalize(ExtensionSettings settings)
    {
        NormalizeModelDefaults(settings);
        settings.DefaultReasoningEffort = CodexModelCatalog.ResolveReasoningEffort(settings.DefaultModel, settings.DefaultReasoningEffort);

        settings.SkillRoots = (from x in settings.SkillRoots ?? new List<string>()
                               where !string.IsNullOrWhiteSpace(x)
                               select x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        settings.EnabledSkillPaths = (from x in settings.EnabledSkillPaths ?? new List<string>()
                                      where !string.IsNullOrWhiteSpace(x)
                                      select x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        settings.CustomModels = (from x in CodexModelCatalog.SupportedModels.Concat(settings.CustomModels ?? new List<string>())
                                 where !string.IsNullOrWhiteSpace(x) && !CodexModelCatalog.IsLegacyModel(x)
                                 select x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        settings.CustomReasoningEfforts = CodexModelCatalog.ReasoningEfforts.ToList();
        settings.CustomVerbosityOptions = NormalizeStringOptions(settings.CustomVerbosityOptions, ["low", "medium", "high"]);
        settings.AgentRoles = NormalizeAgentRoles(settings.AgentRoles);
    }

    /// <summary>Performs the normalize Model Defaults operation.</summary>
    /// <param name="settings">The settings.</param>
    private static void NormalizeModelDefaults(ExtensionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DefaultModel) || CodexModelCatalog.IsLegacyModel(settings.DefaultModel))
        {
            settings.DefaultModel = CodexModelCatalog.DefaultModel;
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultFailoverModel) || CodexModelCatalog.IsLegacyModel(settings.DefaultFailoverModel))
        {
            settings.DefaultFailoverModel = CodexModelCatalog.DefaultFailoverModel;
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultOrchestrationModel) || CodexModelCatalog.IsLegacyModel(settings.DefaultOrchestrationModel))
        {
            settings.DefaultOrchestrationModel = settings.DefaultModel;
        }

        if (!string.IsNullOrWhiteSpace(settings.DefaultBudgetModel) && !CodexModelCatalog.IsLegacyModel(settings.DefaultBudgetModel))
        {
            return;
        }

        settings.DefaultBudgetModel = CodexModelCatalog.DefaultBudgetModel;
    }

    /// <summary>Performs the normalize String Options operation.</summary>
    /// <param name="values">The values.</param>
    /// <param name="defaults">The defaults.</param>
    /// <returns>The normalize String Options result.</returns>
    private static List<string> NormalizeStringOptions(IEnumerable<string>? values, IEnumerable<string> defaults)
    {
        return (from x in (values ?? Enumerable.Empty<string>()).Concat(defaults)
                where !string.IsNullOrWhiteSpace(x)
                select x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Performs the normalize Agent Roles operation.</summary>
    /// <param name="roles">The roles.</param>
    /// <returns>The normalize Agent Roles result.</returns>
    private static List<AgentRoleDefinition> NormalizeAgentRoles(IEnumerable<AgentRoleDefinition>? roles)
    {
        List<AgentRoleDefinition> defaults = new ExtensionSettings().AgentRoles;
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<AgentRoleDefinition> normalized = new();
        foreach (AgentRoleDefinition role in (roles ?? Enumerable.Empty<AgentRoleDefinition>()).Concat(defaults))
        {
            string key = (string.IsNullOrWhiteSpace(role.Name) ? role.Role : role.Name);
            key = (key ?? string.Empty).Trim();
            if (key.Length == 0 || !seen.Add(key))
            {
                continue;
            }

            normalized.Add(CloneAgentRole(role));
        }

        return normalized;
    }

    /// <summary>Performs the clone Agent Role operation.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The clone Agent Role result.</returns>
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
