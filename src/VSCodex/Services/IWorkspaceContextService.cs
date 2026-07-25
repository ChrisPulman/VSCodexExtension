// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the Workspace Context Service contract.</summary>
public interface IWorkspaceContextService
{
    /// <summary>Gets the workspace Root.</summary>
    IObservable<string> WorkspaceRoot { get; }

    /// <summary>Gets the current Workspace Root.</summary>
    string CurrentWorkspaceRoot { get; }

    /// <summary>Gets the current Workspace Name.</summary>
    string CurrentWorkspaceName { get; }

    /// <summary>Gets the current Solution Path.</summary>
    string CurrentSolutionPath { get; }

    /// <summary>Gets the current Workspace Memory Root.</summary>
    string CurrentWorkspaceMemoryRoot { get; }

    /// <summary>Gets the current Workspace Identity.</summary>
    WorkspaceIdentity CurrentWorkspaceIdentity { get; }

    /// <summary>Refreshes the operation.</summary>
    void Refresh();

    /// <summary>Refreshes workspace Identity.</summary>
    void RefreshWorkspaceIdentity();

    /// <summary>Performs the search Files operation.</summary>
    /// <param name="query">The query.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The search Files result.</returns>
    IReadOnlyList<WorkspaceFileReference> SearchFiles(string query, int limit);

    /// <summary>Performs the search Context References operation.</summary>
    /// <param name="query">The query.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The search Context References result.</returns>
    IReadOnlyList<WorkspaceFileReference> SearchContextReferences(string query, int limit);

    /// <summary>Resolves mentions.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="maxBytesPerFile">The max Bytes Per File.</param>
    /// <returns>The resolve Mentions result.</returns>
    IReadOnlyList<WorkspaceFileReference> ResolveMentions(string prompt, int maxBytesPerFile);

    /// <summary>Resolves hash References.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="maxBytesPerReference">The max Bytes Per Reference.</param>
    /// <returns>The resolve Hash References result.</returns>
    IReadOnlyList<WorkspaceFileReference> ResolveHashReferences(string prompt, int maxBytesPerReference);

    /// <summary>Gets current Selection Reference.</summary>
    /// <param name="maxChars">The max Chars.</param>
    /// <returns>The get Current Selection Reference result.</returns>
    WorkspaceFileReference? GetCurrentSelectionReference(int maxChars);
}
