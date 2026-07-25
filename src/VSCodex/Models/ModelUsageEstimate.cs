// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the model Usage Estimate implementation.</summary>
public sealed class ModelUsageEstimate : ReactiveObject
{
    /// <summary>Gets or sets the estimated Input Tokens.</summary>
    public int EstimatedInputTokens { get; set; }

    /// <summary>Gets or sets the estimated Output Tokens.</summary>
    public int EstimatedOutputTokens { get; set; }

    /// <summary>Gets or sets the context Window Tokens.</summary>
    public int ContextWindowTokens { get; set; }

    /// <summary>Gets or sets the context Remaining Tokens.</summary>
    public int ContextRemainingTokens { get; set; }

    /// <summary>Gets or sets the context Used Percent.</summary>
    public int ContextUsedPercent { get; set; }

    /// <summary>Gets or sets the context Remaining Percent.</summary>
    public int ContextRemainingPercent { get; set; }

    /// <summary>Gets or sets the primary Model.</summary>
    public string PrimaryModel { get; set; } = string.Empty;

    /// <summary>Gets or sets the failover Model.</summary>
    public string FailoverModel { get; set; } = string.Empty;

    /// <summary>Gets or sets the budget Model.</summary>
    public string BudgetModel { get; set; } = string.Empty;

    /// <summary>Gets or sets the recommended Model.</summary>
    public string RecommendedModel { get; set; } = string.Empty;

    /// <summary>Gets or sets the primary Estimated Cost.</summary>
    public double PrimaryEstimatedCost { get; set; }

    /// <summary>Gets or sets the budget Estimated Cost.</summary>
    public double BudgetEstimatedCost { get; set; }

    /// <summary>Gets or sets the estimated Savings Percent.</summary>
    public double EstimatedSavingsPercent { get; set; }

    /// <summary>Gets or sets the complexity.</summary>
    public ModelTaskComplexity Complexity { get; set; } = ModelTaskComplexity.Medium;

    /// <summary>Gets or sets the recommendation Reason.</summary>
    public string RecommendationReason { get; set; } = string.Empty;

    /// <summary>Gets or sets the summary.</summary>
    public string Summary { get; set; } = string.Empty;
}
