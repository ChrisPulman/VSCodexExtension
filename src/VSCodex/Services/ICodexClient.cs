// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the Codex client contract.</summary>
public interface ICodexClient
{
    /// <summary>Gets the events.</summary>
    IObservable<CodexEvent> Events { get; }

    /// <summary>Runs the request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task whose result contains the run result.</returns>
    Task<CodexRunResult> RunAsync(CodexRunRequest request);

    /// <summary>Gets rate limits.</summary>
    /// <returns>A task whose result contains the rate limits.</returns>
    Task<JObject?> GetRateLimitsAsync();

    /// <summary>Steers an active turn.</summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="prompt">The steering prompt.</param>
    /// <returns>A task whose result contains the bridge response.</returns>
    Task<JObject> SteerAsync(string threadId, string prompt);

    /// <summary>Interrupts an active turn.</summary>
    /// <param name="threadId">The optional thread identifier.</param>
    /// <returns>A task whose result contains the bridge response.</returns>
    Task<JObject> InterruptAsync(string? threadId);

    /// <summary>Responds to a server request.</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="result">The response payload.</param>
    /// <returns>A task whose result contains the bridge response.</returns>
    Task<JObject> RespondToServerRequestAsync(string requestId, JObject result);

    /// <summary>Cancels the active run without waiting for its completion.</summary>
    void CancelActiveRun();
}
