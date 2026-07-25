// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Provides the reactive Memory Pause Checkpoint Query implementation.</summary>
public sealed class ReactiveMemoryPauseCheckpointQuery
{
    /// <summary>Gets or sets the checkpoint Id.</summary>
    public string CheckpointId { get; set; } = string.Empty;

    /// <summary>Gets or sets the memory Drawer Id.</summary>
    public string MemoryDrawerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Identity Id.</summary>
    public string WorkspaceIdentityId { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Name.</summary>
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Root.</summary>
    public string WorkspaceRoot { get; set; } = string.Empty;

    /// <summary>Gets or sets the memory Root.</summary>
    public string MemoryRoot { get; set; } = string.Empty;

    /// <summary>Gets or sets the chat Id.</summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>Gets or sets the thread Id.</summary>
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>Gets or sets the turn Id.</summary>
    public string TurnId { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation Id.</summary>
    public string OperationId { get; set; } = string.Empty;
}
