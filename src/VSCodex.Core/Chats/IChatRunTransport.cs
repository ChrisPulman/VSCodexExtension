// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace VSCodex.Core.Chats;

/// <summary>Starts, steers, interrupts, and resumes exact chat turns.</summary>
public interface IChatRunTransport
{
    /// <summary>Gets the transport capabilities.</summary>
    ChatTransportCapabilities Capabilities { get; }

    /// <summary>Starts a queued turn.</summary>
    /// <param name="chatId">The owning local chat identifier.</param>
    /// <param name="envelope">The immutable queued turn.</param>
    /// <param name="cancellationToken">A token that cancels the start request.</param>
    /// <returns>The correlated active turn.</returns>
    Task<ActiveTurn> StartAsync(string chatId, TurnEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>Steers the exact active turn.</summary>
    /// <param name="chatId">The owning local chat identifier.</param>
    /// <param name="activeTurn">The exact active turn to steer.</param>
    /// <param name="prompt">The steering input.</param>
    /// <param name="cancellationToken">A token that cancels the steering request.</param>
    /// <returns>A task representing steering acknowledgement.</returns>
    Task SteerAsync(string chatId, ActiveTurn activeTurn, string prompt, CancellationToken cancellationToken);

    /// <summary>Interrupts the exact active turn.</summary>
    /// <param name="chatId">The owning local chat identifier.</param>
    /// <param name="activeTurn">The exact active turn to interrupt.</param>
    /// <param name="cancellationToken">A token that cancels the interrupt request.</param>
    /// <returns>A task representing interrupt acknowledgement.</returns>
    Task InterruptAsync(string chatId, ActiveTurn activeTurn, CancellationToken cancellationToken);

    /// <summary>Resumes a durable checkpoint.</summary>
    /// <param name="checkpoint">The durable checkpoint to resume.</param>
    /// <param name="cancellationToken">A token that cancels the resume request.</param>
    /// <returns>The correlated resumed turn.</returns>
    Task<ActiveTurn> ResumeAsync(PauseCheckpoint checkpoint, CancellationToken cancellationToken);
}
