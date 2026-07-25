// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the coding assistant context service contract.</summary>
public interface ICodingAssistantContextService
{
    /// <summary>Captures the debug context.</summary>
    /// <returns>The captured debug context.</returns>
    DebugContextSnapshot CaptureDebugContext();

    /// <summary>Builds an ask prompt.</summary>
    /// <returns>The prompt.</returns>
    string BuildAskPrompt();

    /// <summary>Builds an explain prompt.</summary>
    /// <returns>The prompt.</returns>
    string BuildExplainPrompt();

    /// <summary>Builds a fix prompt.</summary>
    /// <returns>The prompt.</returns>
    string BuildFixPrompt();

    /// <summary>Builds a review prompt.</summary>
    /// <returns>The prompt.</returns>
    string BuildReviewPrompt();

    /// <summary>Builds an optimize prompt.</summary>
    /// <returns>The prompt.</returns>
    string BuildOptimizePrompt();

    /// <summary>Builds a documentation prompt.</summary>
    /// <returns>The prompt.</returns>
    string BuildDocumentationPrompt();

    /// <summary>Builds a debug prompt.</summary>
    /// <returns>The prompt.</returns>
    string BuildDebugPrompt();

    /// <summary>Builds a test prompt.</summary>
    /// <returns>The prompt.</returns>
    string BuildTestPrompt();

    /// <summary>Builds a test failure prompt.</summary>
    /// <returns>The prompt.</returns>
    string BuildTestFailurePrompt();

    /// <summary>Builds a plan prompt.</summary>
    /// <param name="userGoal">The user's goal.</param>
    /// <param name="agentSummary">The configured-agent summary.</param>
    /// <returns>The prompt.</returns>
    string BuildPlanPrompt(string userGoal, string agentSummary);

    /// <summary>Builds a ReactiveMemory setup prompt.</summary>
    /// <returns>The prompt.</returns>
    string BuildReactiveMemorySetupPrompt();
}
