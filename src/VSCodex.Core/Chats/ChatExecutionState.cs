// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Core.Chats;

/// <summary>Specifies the available chat Execution State values.</summary>
public enum ChatExecutionState
{
    /// <summary>Specifies the idle option.</summary>
    Idle,
    /// <summary>Specifies the starting option.</summary>
    Starting,
    /// <summary>Specifies the running option.</summary>
    Running,
    /// <summary>Specifies the steering option.</summary>
    Steering,
    /// <summary>Specifies the pausing option.</summary>
    Pausing,
    /// <summary>Specifies the paused option.</summary>
    Paused,
    /// <summary>Specifies the resuming option.</summary>
    Resuming,
    /// <summary>Specifies the stopping option.</summary>
    Stopping,
    /// <summary>Specifies the checkpoint Failed option.</summary>
    CheckpointFailed,
    /// <summary>Specifies the faulted option.</summary>
    Faulted
}
