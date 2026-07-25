// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;

namespace VSCodex.Core.Chats;

/// <summary>Correlates one active local operation with its remote Codex thread and turn.</summary>
public sealed class ActiveTurn
{
    /// <summary>Initializes a new instance of the <see cref="ActiveTurn"/> class.</summary>
    /// <param name="operationId">The local operation identifier.</param>
    /// <param name="threadId">The remote Codex thread identifier.</param>
    /// <param name="turnId">The remote Codex turn identifier.</param>
    /// <param name="envelope">The immutable submitted turn.</param>
    public ActiveTurn(string operationId, string threadId, string turnId, TurnEnvelope envelope)
    {
        OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
        ThreadId = threadId ?? string.Empty;
        TurnId = turnId ?? string.Empty;
        Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
    }

    /// <summary>Gets the operation identifier used for cancellation and event ownership.</summary>
    public string OperationId { get; }

    /// <summary>Gets the remote thread identifier.</summary>
    public string ThreadId { get; }

    /// <summary>Gets the remote turn identifier.</summary>
    public string TurnId { get; }

    /// <summary>Gets the immutable submitted turn.</summary>
    public TurnEnvelope Envelope { get; }
}
