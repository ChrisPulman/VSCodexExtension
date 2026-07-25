// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the i Reactive Memory Service contract.</summary>
public interface IReactiveMemoryService
{
    /// <summary>Performs the react To Prompt operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="identity">The identity.</param>
    /// <param name="threadId">The thread Id.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    Task<ReactiveMemoryCallResult> ReactToPromptAsync(string prompt, WorkspaceIdentity identity, string? threadId);

    /// <summary>Writes diary.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="response">The response.</param>
    /// <param name="identity">The identity.</param>
    /// <param name="threadId">The thread Id.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    Task<ReactiveMemoryCallResult> WriteDiaryAsync(string prompt, string response, WorkspaceIdentity identity, string? threadId);

    /// <summary>Adds memory.</summary>
    /// <param name="text">The text.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="identity">The identity.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    Task<ReactiveMemoryCallResult> AddMemoryAsync(string text, string scope, WorkspaceIdentity identity);

    /// <summary>Scans the workspace manually.</summary>
    /// <param name="identity">The identity.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    Task<ReactiveMemoryCallResult> ScanWorkspaceAsync(WorkspaceIdentity identity);

    /// <summary>Scans the workspace with the requested scheduling mode.</summary>
    /// <param name="identity">The workspace identity and paths.</param>
    /// <param name="automatic">Whether to apply automatic scan limits.</param>
    /// <returns>A task whose result contains the scan result.</returns>
    Task<ReactiveMemoryCallResult> ScanWorkspaceAsync(WorkspaceIdentity identity, bool automatic);

    /// <summary>Saves pause Checkpoint.</summary>
    /// <param name="checkpoint">The checkpoint.</param>
    /// <param name="cancellationToken">The cancellation Token.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    Task<ReactiveMemoryPauseCheckpointResult> SavePauseCheckpointAsync(ReactiveMemoryPauseCheckpoint checkpoint, CancellationToken cancellationToken);

    /// <summary>Restores pause Checkpoint.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation Token.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    Task<ReactiveMemoryPauseCheckpointResult> RestorePauseCheckpointAsync(ReactiveMemoryPauseCheckpointQuery query, CancellationToken cancellationToken);
}
