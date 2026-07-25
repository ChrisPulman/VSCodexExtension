// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace VSCodex.Core.Chats;

/// <summary>Contains the durable state required to resume a paused chat.</summary>
public sealed class PauseCheckpoint
{
    /// <summary>Initializes a new instance of the <see cref="PauseCheckpoint"/> class.</summary>
    /// <param name="chatId">The owning local chat identifier.</param>
    /// <param name="activeTurn">The interrupted turn.</param>
    /// <param name="queuedTurns">The queued turns preserved by the pause.</param>
    /// <param name="conversationSummary">The compact conversation summary.</param>
    /// <param name="pauseReason">The user or lifecycle reason for pausing.</param>
    /// <param name="createdAt">The checkpoint creation time.</param>
    public PauseCheckpoint(
        string chatId,
        ActiveTurn activeTurn,
        IReadOnlyList<TurnEnvelope> queuedTurns,
        string conversationSummary,
        string pauseReason,
        DateTimeOffset createdAt)
    {
        ChatId = chatId ?? throw new ArgumentNullException(nameof(chatId));
        ActiveTurn = activeTurn ?? throw new ArgumentNullException(nameof(activeTurn));
        QueuedTurns = queuedTurns ?? throw new ArgumentNullException(nameof(queuedTurns));
        ConversationSummary = conversationSummary ?? string.Empty;
        PauseReason = pauseReason ?? string.Empty;
        CreatedAt = createdAt;
    }

    /// <summary>Gets the owning local chat identifier.</summary>
    public string ChatId { get; }

    /// <summary>Gets the interrupted turn.</summary>
    public ActiveTurn ActiveTurn { get; }

    /// <summary>Gets the queued turns preserved by the pause.</summary>
    public IReadOnlyList<TurnEnvelope> QueuedTurns { get; }

    /// <summary>Gets the compact conversation summary saved with the checkpoint.</summary>
    public string ConversationSummary { get; }

    /// <summary>Gets the user or lifecycle reason for the pause.</summary>
    public string PauseReason { get; }

    /// <summary>Gets the checkpoint creation time.</summary>
    public DateTimeOffset CreatedAt { get; }
}
