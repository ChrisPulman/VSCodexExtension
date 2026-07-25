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

/// <summary>Provides the options Standard Values implementation.</summary>
internal static class OptionsStandardValues
{
    /// <summary>Stores the default Verbosity Options.</summary>
    private static readonly string[] DefaultVerbosityOptions = { "low", "medium", "high" };

    /// <summary>Gets available Models.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The get Available Models result.</returns>
    internal static IEnumerable<string> GetAvailableModels(ITypeDescriptorContext? context)
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
        if (settings is not null)
        {
            values.AddRange(settings.CustomModels ?? Enumerable.Empty<string>());
            values.Add(settings.DefaultModel);
            values.Add(settings.DefaultFailoverModel);
            values.Add(settings.DefaultOrchestrationModel);
            values.Add(settings.DefaultBudgetModel);
        }

        values.AddRange(CodexModelCatalog.SupportedModels);
        return DistinctValues(values);
    }

    /// <summary>Gets reasoning Efforts.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The get Reasoning Efforts result.</returns>
    internal static IEnumerable<string> GetReasoningEfforts(ITypeDescriptorContext? context)
    {
        string modelName = CodexModelCatalog.DefaultModel;
        if (context?.Instance is VSCodexOptionsModel model)
        {
            modelName = model.DefaultModel;
        }
        else
        {
            ExtensionSettings? settings = TryGetSettings();
            if (settings is not null)
            {
                modelName = settings.DefaultModel;
            }
        }

        return CodexModelCatalog.GetReasoningEfforts(modelName);
    }

    /// <summary>Gets verbosity Options.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The get Verbosity Options result.</returns>
    internal static IEnumerable<string> GetVerbosityOptions(ITypeDescriptorContext? context)
    {
        var values = new List<string>();
        if (context?.Instance is VSCodexOptionsModel model)
        {
            values.Add(model.DefaultVerbosity);
        }

        var settings = TryGetSettings();
        if (settings is not null)
        {
            values.AddRange(settings.CustomVerbosityOptions ?? Enumerable.Empty<string>());
            values.Add(settings.DefaultVerbosity);
        }

        values.AddRange(DefaultVerbosityOptions);
        return DistinctValues(values);
    }

    /// <summary>Attempts to get Settings.</summary>
    /// <returns>The try Get Settings result.</returns>
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

    /// <summary>Performs the lines operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The lines result.</returns>
    private static IEnumerable<string> Lines(string value)
        => (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim());

    /// <summary>Performs the distinct Values operation.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The distinct Values result.</returns>
    private static IEnumerable<string> DistinctValues(IEnumerable<string> values)
        => values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
