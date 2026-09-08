// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the Codex orchestrator contract.</summary>
public interface ICodexOrchestrator
{
    /// <summary>Gets Codex events.</summary>
    IObservable<CodexEvent> Events { get; }

    /// <summary>Runs a Codex request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task whose result contains the run result.</returns>
    Task<CodexRunResult> RunAsync(CodexRunRequest request);

    /// <summary>Gets rate limits.</summary>
    /// <returns>A task whose result contains the rate limits.</returns>
    Task<JObject?> GetRateLimitsAsync();

    /// <summary>Steers an active turn.</summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="prompt">The steering prompt.</param>
    /// <returns>A task that completes after steering.</returns>
    Task SteerAsync(string threadId, string prompt);

    /// <summary>Interrupts an active turn.</summary>
    /// <param name="threadId">The optional thread identifier.</param>
    /// <returns>A task that completes after interruption.</returns>
    Task InterruptAsync(string? threadId);

    /// <summary>Responds to a server request.</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="method">The server request method.</param>
    /// <param name="approve">Whether to approve the request.</param>
    /// <returns>A task that completes after the response is sent.</returns>
    Task RespondToServerRequestAsync(string requestId, string method, bool approve);

    /// <summary>Cancels active work without blocking.</summary>
    void Cancel();
}
