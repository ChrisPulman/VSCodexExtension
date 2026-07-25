// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI;

namespace VSCodex.ViewModels;

/// <summary>Applies and persists interactive VSCodex settings.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Determines whether can Change Setting.</summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <param name="currentValue">The current Value.</param>
    /// <param name="nextValue">The next Value.</param>
    /// <returns><see langword="true"/> when can Change Setting succeeds; otherwise, <see langword="false"/>.</returns>
    private bool CanChangeSetting<T>(T currentValue, T nextValue)
    {
        if (!IsRunning || EqualityComparer<T>.Default.Equals(currentValue, nextValue))
        {
            return true;
        }

        Status = VSCodexSettingsAreLockedWhileATaskIsRunnText;
        return false;
    }

    /// <summary>Sets model Setting.</summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <param name="field">The field.</param>
    /// <param name="value">The value.</param>
    /// <param name="propertyName">The property Name.</param>
    /// <param name="refreshAnalytics">The refresh Analytics.</param>
    private void SetModelSetting<T>(ref T field, T value, string propertyName, bool refreshAnalytics)
    {
        if (EqualityComparer<T>.Default.Equals(field, value) || !CanChangeSetting(field, value))
        {
            return;
        }

        AssignProperty(ref field, value, propertyName);
        ScheduleModelSettingsSave(refreshAnalytics);
    }

    /// <summary>Sets model Setting.</summary>
    /// <param name="field">The field.</param>
    /// <param name="value">The value.</param>
    /// <param name="propertyName">The property Name.</param>
    /// <param name="refreshAnalytics">The refresh Analytics.</param>
    private void SetModelSetting(ref string field, string? value, string propertyName, bool refreshAnalytics)
    {
        string next = value ?? string.Empty;
        if (StringComparer.Ordinal.Equals(field, next) || !CanChangeSetting(field, next))
        {
            return;
        }

        AssignProperty(ref field, next, propertyName);
        ScheduleModelSettingsSave(refreshAnalytics);
    }

    /// <summary>Sets input Area Height.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The set Input Area Height result.</returns>
    private double SetInputAreaHeight(double value)
    {
        double clamped = ClampInputHeight(value);
        _ = this.RaiseAndSetIfChanged(ref _inputAreaHeight, clamped);
        return clamped;
    }

    /// <summary>Applies settings From Store.</summary>
    /// <param name="settings">The settings.</param>
    private void ApplySettingsFromStore(ExtensionSettings settings)
    {
        if (settings is null)
        {
            return;
        }

        string model = DefaultIfBlank(settings.DefaultModel, CodexModelCatalog.DefaultModel);
        string budgetModel = DefaultIfBlank(
            settings.DefaultBudgetModel,
            CodexModelCatalog.DefaultBudgetModel);
        string reasoningModel = settings.DefaultBudgetDrivenModelSelection ? budgetModel : model;
        string reasoningEffort = CodexModelCatalog.ResolveReasoningEffort(
            reasoningModel,
            settings.DefaultReasoningEffort);
        IEnumerable<string> models = CodexModelCatalog.SupportedModels
            .Concat(settings.CustomModels ?? new List<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase);
        bool changed = ApplySettingsValues(settings, model, budgetModel, reasoningEffort);
        changed |= ApplySettingsCollections(settings, models, reasoningModel);
        if (!changed)
        {
            return;
        }

        UpdateAnalytics(Prompt);
    }

    /// <summary>Applies settings-backed scalar values.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="model">The resolved main model.</param>
    /// <param name="budgetModel">The resolved budget model.</param>
    /// <param name="reasoningEffort">The resolved reasoning effort.</param>
    /// <returns><see langword="true"/> when any value changed.</returns>
    private bool ApplySettingsValues(
        ExtensionSettings settings,
        string model,
        string budgetModel,
        string reasoningEffort)
    {
        bool changed = SetPropertyFromSettings(ref _selectedModel, model, nameof(SelectedModel));
        string failover = DefaultIfBlank(
            settings.DefaultFailoverModel,
            CodexModelCatalog.DefaultFailoverModel);
        changed |= SetPropertyFromSettings(ref _failoverModel, failover, nameof(FailoverModel));
        changed |= SetPropertyFromSettings(
            ref _selectedReasoning,
            reasoningEffort,
            nameof(SelectedReasoning));
        changed |= SetPropertyFromSettings(
            ref _selectedVerbosity,
            settings.DefaultVerbosity,
            nameof(SelectedVerbosity));
        changed |= ApplyRunPolicySettings(settings);
        string orchestrationModel = DefaultIfBlank(settings.DefaultOrchestrationModel, model);
        changed |= SetPropertyFromSettings(
            ref _orchestrationModel,
            orchestrationModel,
            nameof(OrchestrationModel));
        changed |= SetPropertyFromSettings(
            ref _budgetDrivenModelSelection,
            settings.DefaultBudgetDrivenModelSelection,
            nameof(BudgetDrivenModelSelection));
        changed |= SetPropertyFromSettings(ref _budgetModel, budgetModel, nameof(BudgetModel));
        changed |= SetPropertyFromSettings(
            ref _inputAreaHeight,
            ClampInputHeight(settings.DefaultInputAreaHeight),
            nameof(InputAreaHeight));
        return changed;
    }

    /// <summary>Applies run-policy scalar settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns><see langword="true"/> when any value changed.</returns>
    private bool ApplyRunPolicySettings(ExtensionSettings settings)
    {
        bool changed = SetPropertyFromSettings(
            ref _approvalPolicy,
            settings.DefaultApprovalPolicy,
            nameof(ApprovalPolicy));
        changed |= SetPropertyFromSettings(
            ref _sandboxMode,
            settings.DefaultSandboxMode,
            nameof(SandboxMode));
        changed |= SetPropertyFromSettings(
            ref _accessLevel,
            AccessLevelFromSandbox(settings.DefaultSandboxMode),
            nameof(AccessLevel));
        changed |= SetPropertyFromSettings(
            ref _useMultiAgentOrchestration,
            settings.DefaultUseMultiAgentOrchestration,
            nameof(UseMultiAgentOrchestration));
        changed |= SetPropertyFromSettings(
            ref _maxAgentConcurrency,
            Math.Max(1, settings.DefaultMaxAgentConcurrency),
            nameof(MaxAgentConcurrency));
        changed |= SetPropertyFromSettings(
            ref _agentStrategy,
            settings.DefaultAgentStrategy,
            nameof(AgentStrategy));
        return changed;
    }

    /// <summary>Applies settings-backed collection values.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="models">The available models.</param>
    /// <param name="reasoningModel">The model that controls reasoning efforts.</param>
    /// <returns><see langword="true"/> when any collection changed.</returns>
    private bool ApplySettingsCollections(
        ExtensionSettings settings,
        IEnumerable<string> models,
        string reasoningModel)
    {
        bool changed = ReplaceCollection(ModelOptions, models);
        changed |= ReplaceCollection(
            ReasoningOptions,
            CodexModelCatalog.GetReasoningEfforts(reasoningModel));
        changed |= ReplaceCollection(
            VerbosityOptions,
            DistinctOptions(settings.CustomVerbosityOptions));
        changed |= ReplaceCollection(
            AgentRoles,
            settings.AgentRoles ?? new List<AgentRoleDefinition>());
        return changed;
    }

    /// <summary>Refreshes the reasoning picker for the effective model.</summary>
    private void RefreshReasoningOptions()
    {
        string model = EffectiveMainModel();
        _ = ReplaceCollection(ReasoningOptions, CodexModelCatalog.GetReasoningEfforts(model));
        string normalized = CodexModelCatalog.ResolveReasoningEffort(model, _selectedReasoning);
        if (StringComparer.Ordinal.Equals(normalized, _selectedReasoning))
        {
            return;
        }

        _selectedReasoning = normalized;
        SetPropertyFromSettingsNotification(nameof(SelectedReasoning));
    }

    /// <summary>Sets property From Settings.</summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <param name="field">The field.</param>
    /// <param name="value">The value.</param>
    /// <param name="propertyName">The property Name.</param>
    /// <returns><see langword="true"/> when set Property From Settings succeeds; otherwise, <see langword="false"/>.</returns>
    private bool SetPropertyFromSettings<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        AssignProperty(ref field, value, propertyName);
        return true;
    }

    /// <summary>Raises a property-change notification for a settings-backed property.</summary>
    /// <param name="propertyName">The property name.</param>
    private void SetPropertyFromSettingsNotification(string propertyName)
    {
        ((IReactiveObject)this).RaisePropertyChanged(new(propertyName));
    }

    /// <summary>Assigns a property value and raises ReactiveUI notifications.</summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">The property name.</param>
    private void AssignProperty<T>(ref T field, T value, string propertyName)
    {
        IReactiveObject reactiveObject = this;
        reactiveObject.RaisePropertyChanging(new(propertyName));
        field = value;
        reactiveObject.RaisePropertyChanged(new(propertyName));
    }
}
