// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Provides the model Profile implementation.</summary>
public sealed class ModelProfile
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the display Name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the input Price Per Million.</summary>
    public double InputPricePerMillion { get; set; }

    /// <summary>Gets or sets the output Price Per Million.</summary>
    public double OutputPricePerMillion { get; set; }

    /// <summary>Gets or sets the context Window Tokens.</summary>
    public int ContextWindowTokens { get; set; }

    /// <summary>Gets or sets the best For Complexity.</summary>
    public ModelTaskComplexity BestForComplexity { get; set; } = ModelTaskComplexity.Medium;

    /// <summary>Gets or sets the is Codex Optimized.</summary>
    public bool IsCodexOptimized { get; set; }

    /// <summary>Gets or sets the notes.</summary>
    public string Notes { get; set; } = string.Empty;
}
