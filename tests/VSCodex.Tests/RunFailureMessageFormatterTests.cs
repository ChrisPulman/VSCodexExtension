// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using VSCodex.Services;

namespace VSCodex.Tests;

/// <summary>Verifies that service errors remain readable without losing their explanation.</summary>
public sealed class RunFailureMessageFormatterTests
{
    /// <summary>Extracts service explanations from errors returned by the Codex runtime.</summary>
    /// <returns>The asynchronous verification operation.</returns>
    [Test]
    public async Task Service_rejection_shows_the_reason_without_stack_frames()
    {
        var cause = new InvalidOperationException("Error: {\"type\":\"error\",\"error\":{\"message\":\"The model is not supported 日本語 😀\"}}\n at completeAppServerTurn (bridge.mjs:506)");
        var failure = new InvalidOperationException("Dispatch failed", cause);
        await Assert.That(RunFailureMessageFormatter.Format(failure)).IsEqualTo("The model is not supported 日本語 😀");
        await Assert.That(failure.InnerException).IsSameReferenceAs(cause);
    }

    /// <summary>Retains plain text explanations when a response is not a JSON error object.</summary>
    /// <param name="message">The transport error text.</param>
    /// <param name="expected">The readable explanation.</param>
    /// <returns>The asynchronous verification operation.</returns>
    [Test]
    [Arguments("Connection closed\r\n at transport", "Connection closed")]
    [Arguments("Error: {not JSON}\n at transport", "Error: {not JSON}")]
    [Arguments("{\"error\":\"unavailable\"}", "{\"error\":\"unavailable\"}")]
    [Arguments("{\"message\":\"Rate limit reached\"}", "Rate limit reached")]
    [Arguments("   ", "Codex could not finish this request.")]
    public async Task Plain_text_and_malformed_responses_are_safe(string message, string expected)
    {
        await Assert.That(RunFailureMessageFormatter.Format(new InvalidOperationException(message))).IsEqualTo(expected);
    }
}
