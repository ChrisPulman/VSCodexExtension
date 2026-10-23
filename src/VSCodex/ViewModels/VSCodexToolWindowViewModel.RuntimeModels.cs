// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace VSCodex.ViewModels;

/// <summary>Loads account-specific model and reasoning choices.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Discovers runtime capabilities without blocking construction or replacing custom choices.</summary>
    /// <returns>A task that completes after updating the picker on the UI thread.</returns>
    private async Task RefreshRuntimeModelsAsync()
    {
        try
        {
            JObject? response = await _codex.GetModelsAsync().ConfigureAwait(false);
            List<CodexRuntimeModel> models = (response?["data"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Where(model => !string.IsNullOrWhiteSpace(model.Value<string>("model")) && model.Value<bool?>("hidden") != true)
                .Select(model => new CodexRuntimeModel
                {
                    Model = model.Value<string>("model")!,
                    IsDefault = model.Value<bool?>("isDefault") == true,
                    DefaultReasoningEffort = model.Value<string>("defaultReasoningEffort") ?? "medium",
                    ReasoningEfforts = (model["supportedReasoningEfforts"] as JArray ?? new JArray())
                        .OfType<JObject>()
                        .Select(effort => effort.Value<string>("reasoningEffort") ?? string.Empty)
                        .Where(effort => !string.IsNullOrWhiteSpace(effort))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                }).ToList();
            if (models.Count == 0)
            {
                return;
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync(_lifetime.Token);
            CodexModelCatalog.SetRuntimeModels(models);

            // Read current selections after awaiting discovery: the user may have edited them meanwhile.
            // Older settings automatically added every shipped suggestion to CustomModels.
            // Only identifiers outside that shipped list represent user-provided custom models.
            IEnumerable<string> custom = (_settingsStore.Current.CustomModels ?? new List<string>())
                .Where(model => !CodexModelCatalog.IsShippedModel(model) && !CodexModelCatalog.IsLegacyModel(model));
            if (!IsRunning)
            {
                SelectedModel = CodexRuntimeModel.ResolveSelection(SelectedModel, models, custom);
                BudgetModel = CodexRuntimeModel.ResolveSelection(BudgetModel, models, custom);
                FailoverModel = CodexRuntimeModel.ResolveSelection(FailoverModel, models, custom);
                OrchestrationModel = CodexRuntimeModel.ResolveSelection(OrchestrationModel, models, custom);
                foreach (AgentRoleDefinition agent in AgentRoles)
                {
                    if (!string.IsNullOrWhiteSpace(agent.Model))
                    {
                        agent.Model = CodexRuntimeModel.ResolveSelection(agent.Model, models, custom);
                    }
                }

                RefreshReasoningOptions();
            }

            _ = ReplaceCollection(ModelOptions, models.Select(model => model.Model)
                .Concat(custom)
                .Concat(new[] { SelectedModel, BudgetModel, FailoverModel, OrchestrationModel })
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            Status = $"Codex model discovery failed; saved model choices remain available: {exception.Message}";
        }
    }
}
