// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

namespace VSCodex.Core.Models;

/// <summary>Provides the Codex application model and reasoning-effort catalog.</summary>
public static class CodexModelCatalog
{
    /// <summary>Stores the current model identifiers.</summary>
    private static readonly string[] Models =
    {
        "gpt-5.6-sol",
        "gpt-5.6-terra",
        "gpt-5.6-luna",
        "gpt-5.5",
        "gpt-5.4",
        "gpt-5.4-mini",
        "gpt-5.3-codex-spark"
    };

    /// <summary>Stores the Sol and Terra effort identifiers.</summary>
    private static readonly string[] SolTerraEfforts = { "low", "medium", "high", "xhigh", "max", "ultra" };

    /// <summary>Stores the Luna effort identifiers.</summary>
    private static readonly string[] LunaEfforts = { "low", "medium", "high", "xhigh", "max" };

    /// <summary>Stores the standard effort identifiers.</summary>
    private static readonly string[] StandardEfforts = { "low", "medium", "high", "xhigh" };

    /// <summary>Stores retired shipped model identifiers.</summary>
    private static readonly string[] LegacyModels =
    {
        "gpt-5.4-codex",
        "gpt-5.3-codex",
        "gpt-5.2",
        "gpt-5.2-codex",
        "gpt-5.1-codex",
        "gpt-5-codex"
    };

    /// <summary>Stores the read-only Sol and Terra effort identifiers.</summary>
    private static readonly IReadOnlyList<string> SolTerraEffortValues = Array.AsReadOnly(SolTerraEfforts);

    /// <summary>Stores the read-only Luna effort identifiers.</summary>
    private static readonly IReadOnlyList<string> LunaEffortValues = Array.AsReadOnly(LunaEfforts);

    /// <summary>Stores the read-only standard effort identifiers.</summary>
    private static readonly IReadOnlyList<string> StandardEffortValues = Array.AsReadOnly(StandardEfforts);

    /// <summary>Stores the safe effort identifiers used for an unknown custom model.</summary>
    private static readonly IReadOnlyList<string> UnknownModelEffortValues = Array.AsReadOnly(["medium"]);

    /// <summary>Gets the Codex Power default.</summary>
    public static string DefaultModel => "gpt-5.6-sol";

    /// <summary>Gets the default lower-cost failover model.</summary>
    public static string DefaultFailoverModel => "gpt-5.6-terra";

    /// <summary>Gets the default model for lower-cost, repeatable work.</summary>
    public static string DefaultBudgetModel => "gpt-5.6-luna";

    /// <summary>Gets the balanced reasoning default.</summary>
    public static string DefaultReasoningEffort => "medium";

    /// <summary>Gets the models exposed by the current Codex application.</summary>
    public static IReadOnlyList<string> SupportedModels { get; } = Array.AsReadOnly(Models);

    /// <summary>Gets the complete ordered reasoning-effort list used across the current catalog.</summary>
    public static IReadOnlyList<string> ReasoningEfforts { get; } = SolTerraEffortValues;

    /// <summary>Gets whether a model is part of the current Codex application catalog.</summary>
    /// <param name="model">The model identifier.</param>
    /// <returns><see langword="true"/> when the model is supported; otherwise, <see langword="false"/>.</returns>
    public static bool IsSupportedModel(string? model)
        => Models.Contains(model ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets whether a model identifier is a retired shipped default.</summary>
    /// <param name="model">The model identifier.</param>
    /// <returns><see langword="true"/> when the identifier is retired; otherwise, <see langword="false"/>.</returns>
    public static bool IsLegacyModel(string? model)
        => LegacyModels.Contains(model ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the supported efforts for a model.</summary>
    /// <param name="model">The model identifier.</param>
    /// <returns>The ordered model-specific efforts.</returns>
    public static IReadOnlyList<string> GetReasoningEfforts(string? model)
    {
        return (model ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "gpt-5.6-sol" or "gpt-5.6-terra" => SolTerraEffortValues,
            "gpt-5.6-luna" => LunaEffortValues,
            "gpt-5.5" or "gpt-5.4" or "gpt-5.4-mini" or "gpt-5.3-codex-spark" => StandardEffortValues,
            _ => UnknownModelEffortValues,
        };
    }

    /// <summary>Resolves an effort to a valid value for a model.</summary>
    /// <param name="model">The model identifier.</param>
    /// <param name="effort">The requested effort.</param>
    /// <returns>A valid model-specific effort.</returns>
    public static string ResolveReasoningEffort(string? model, string? effort)
    {
        IReadOnlyList<string> supported = GetReasoningEfforts(model);
        string requested = (effort ?? string.Empty).Trim().ToLowerInvariant();
        if (supported.Contains(requested, StringComparer.OrdinalIgnoreCase))
        {
            return requested;
        }

        return string.Equals(requested, "max", StringComparison.OrdinalIgnoreCase)
            || string.Equals(requested, "ultra", StringComparison.OrdinalIgnoreCase)
            ? supported[supported.Count - 1]
            : DefaultReasoningEffort;
    }
}
