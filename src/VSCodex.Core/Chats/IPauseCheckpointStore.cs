// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace VSCodex.Core.Chats;

/// <summary>Saves and restores durable pause checkpoints, normally through ReactiveMemory.</summary>
public interface IPauseCheckpointStore
{
    /// <summary>Saves a pause checkpoint and returns its durable identifier.</summary>
    /// <param name="checkpoint">The checkpoint to persist.</param>
    /// <param name="cancellationToken">A token that cancels persistence.</param>
    /// <returns>The durable checkpoint identifier.</returns>
    Task<string> SaveAsync(PauseCheckpoint checkpoint, CancellationToken cancellationToken);

    /// <summary>Loads a pause checkpoint by durable identifier.</summary>
    /// <param name="checkpointId">The durable checkpoint identifier.</param>
    /// <param name="cancellationToken">A token that cancels retrieval.</param>
    /// <returns>The correlated pause checkpoint.</returns>
    Task<PauseCheckpoint> LoadAsync(string checkpointId, CancellationToken cancellationToken);
}
