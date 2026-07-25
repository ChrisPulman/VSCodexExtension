// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace VSCodex.Models;

/// <summary>Provides the reactive Memory Pause Checkpoint implementation.</summary>
public sealed class ReactiveMemoryPauseCheckpoint
{
    /// <summary>Gets or sets the checkpoint Id.</summary>
    public string CheckpointId { get; set; } = Guid.NewGuid().ToString("N");

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

    /// <summary>Gets or sets the pause Reason.</summary>
    public string PauseReason { get; set; } = string.Empty;

    /// <summary>Gets or sets the prompt.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Gets or sets the partial Response.</summary>
    public string PartialResponse { get; set; } = string.Empty;

    /// <summary>Gets or sets the context.</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>Gets the queued prompts.</summary>
    public List<string> QueuedPrompts { get; } = [];

    /// <summary>Gets or sets the created At.</summary>
    public DateTimeOffset CreatedAt { get; set; } = TimeProvider.System.GetUtcNow();

    /// <summary>Gets or sets the state.</summary>
    public PauseCheckpointState State { get; set; } = PauseCheckpointState.Pending;
}
