// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Provides the orchestration Event implementation.</summary>
public sealed class OrchestrationEvent
{
    /// <summary>Gets or sets the type.</summary>
    public string Type { get; set; } = "status";

    /// <summary>Gets or sets the message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the plan Id.</summary>
    public string? PlanId { get; set; }

    /// <summary>Gets or sets the section Id.</summary>
    public string? SectionId { get; set; }

    /// <summary>Gets or sets the section.</summary>
    public OrchestrationTaskSection? Section { get; set; }
}
