// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the orchestration Run Plan implementation.</summary>
public sealed class OrchestrationRunPlan : ReactiveObject
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the goal.</summary>
    public string Goal { get; set; } = string.Empty;

    /// <summary>Gets or sets the strategy.</summary>
    public AgentExecutionStrategy Strategy { get; set; } = AgentExecutionStrategy.ReviewGate;

    /// <summary>Gets the agents.</summary>
    public List<AgentRoleDefinition> Agents { get; } = [];

    /// <summary>Gets the sections.</summary>
    public List<OrchestrationTaskSection> Sections { get; } = [];

    /// <summary>Gets or sets the created.</summary>
    public DateTimeOffset Created { get; set; } = TimeProvider.System.GetLocalNow();
}
