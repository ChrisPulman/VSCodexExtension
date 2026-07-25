// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the task orchestration service contract.</summary>
public interface ITaskOrchestrationService
{
    /// <summary>Gets orchestration events.</summary>
    IObservable<OrchestrationEvent> Events { get; }

    /// <summary>Gets the current plan.</summary>
    OrchestrationRunPlan? CurrentPlan { get; }

    /// <summary>Runs an orchestration request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task that resolves to the run result.</returns>
    Task<CodexRunResult> RunAsync(CodexRunRequest request);

    /// <summary>Cancels the active orchestration.</summary>
    void Cancel();
}
