// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Specifies the available agent Execution Strategy values.</summary>
public enum AgentExecutionStrategy
{
    /// <summary>Specifies the sequential option.</summary>
    Sequential,
    /// <summary>Specifies the planner Then Parallel option.</summary>
    PlannerThenParallel,
    /// <summary>Specifies the review Gate option.</summary>
    ReviewGate
}
