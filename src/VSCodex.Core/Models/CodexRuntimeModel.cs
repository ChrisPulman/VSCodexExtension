// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

namespace VSCodex.Core.Models;

/// <summary>Describes capabilities advertised by the signed-in Codex runtime.</summary>
public sealed class CodexRuntimeModel
{
    /// <summary>Gets or sets the identifier accepted when starting a turn.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Gets or sets whether this is the runtime's default model.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Gets or sets the reasoning default advertised by the runtime.</summary>
    public string DefaultReasoningEffort { get; set; } = "medium";

    /// <summary>Gets or sets the advertised reasoning choices, including future effort values.</summary>
    public IReadOnlyList<string> ReasoningEfforts { get; set; } = Array.Empty<string>();

    /// <summary>Resolves a saved model without discarding explicitly configured custom identifiers.</summary>
    /// <param name="selected">The current selection.</param>
    /// <param name="available">The successfully discovered models.</param>
    /// <param name="customModels">The user's explicitly configured custom models.</param>
    /// <returns>The existing choice or the runtime default for a retired shipped identifier.</returns>
    public static string ResolveSelection(string? selected, IReadOnlyList<CodexRuntimeModel> available, IEnumerable<string> customModels)
    {
        string value = (selected ?? string.Empty).Trim();
        if (available.Count == 0 || available.Any(model => string.Equals(model.Model, value, StringComparison.OrdinalIgnoreCase))
            || customModels.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return value;
        }

        bool shipped = CodexModelCatalog.IsShippedModel(value) || CodexModelCatalog.IsLegacyModel(value);
        return string.IsNullOrWhiteSpace(value) || shipped
            ? (available.FirstOrDefault(model => model.IsDefault) ?? available[0]).Model
            : value;
    }

    /// <summary>Resolves an effort against this model's advertised capabilities.</summary>
    /// <param name="requested">The saved or typed effort.</param>
    /// <returns>The requested effort when supported, otherwise the runtime default.</returns>
    public string ResolveReasoningEffort(string? requested)
    {
        string value = (requested ?? string.Empty).Trim().ToLowerInvariant();
        if (ReasoningEfforts.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return value;
        }

        return ReasoningEfforts.Contains(DefaultReasoningEffort, StringComparer.OrdinalIgnoreCase)
            ? DefaultReasoningEffort
            : ReasoningEfforts.FirstOrDefault() ?? DefaultReasoningEffort;
    }
}
