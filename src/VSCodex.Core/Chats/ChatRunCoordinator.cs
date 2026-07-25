// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VSCodex.Core.Chats;

/// <summary>Owns the queue and active operation for exactly one chat.</summary>
public sealed class ChatRunCoordinator
{
    /// <summary>Message returned when a running chat lacks an active turn.</summary>
    private static readonly string MissingActiveTurnMessage = "The chat has no active turn.";

    /// <summary>Synchronizes state snapshots and queue mutations.</summary>
    private readonly object _sync = new();

    /// <summary>Stores immutable turns in FIFO submission order.</summary>
    private readonly Queue<TurnEnvelope> _queue = new();

    /// <summary>Serializes asynchronous transport and checkpoint operations.</summary>
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    /// <summary>Starts and controls exact remote Codex turns.</summary>
    private readonly IChatRunTransport _transport;

    /// <summary>Persists and restores durable pause checkpoints.</summary>
    private readonly IPauseCheckpointStore _checkpointStore;

    /// <summary>Stores the exact active turn correlation.</summary>
    private ActiveTurn? _activeTurn;

    /// <summary>Stores the durable checkpoint identifier while paused.</summary>
    private string? _checkpointId;

    /// <summary>Stores the current execution lifecycle state.</summary>
    private ChatExecutionState _state;

    /// <summary>Initializes a new instance of the <see cref="ChatRunCoordinator"/> class.</summary>
    /// <param name="chatId">The stable local chat identifier.</param>
    /// <param name="transport">The exact-turn Codex transport.</param>
    /// <param name="checkpointStore">The durable pause checkpoint store.</param>
    public ChatRunCoordinator(string chatId, IChatRunTransport transport, IPauseCheckpointStore checkpointStore)
    {
        ChatId = string.IsNullOrWhiteSpace(chatId) ? throw new ArgumentException("A chat identifier is required.", nameof(chatId)) : chatId;
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
    }

    /// <summary>Gets the stable local chat identifier.</summary>
    public string ChatId { get; }

    /// <summary>Gets the current execution state.</summary>
    public ChatExecutionState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    /// <summary>Gets the active turn, if any.</summary>
    public ActiveTurn? ActiveTurn
    {
        get
        {
            lock (_sync)
            {
                return _activeTurn;
            }
        }
    }

    /// <summary>Gets the durable checkpoint identifier, if the chat is paused.</summary>
    public string? CheckpointId
    {
        get
        {
            lock (_sync)
            {
                return _checkpointId;
            }
        }
    }

    /// <summary>Gets a FIFO snapshot of queued turns.</summary>
    public IReadOnlyList<TurnEnvelope> QueuedTurns
    {
        get
        {
            lock (_sync)
            {
                return _queue.ToArray();
            }
        }
    }

    /// <summary>Adds an immutable turn to this chat's queue.</summary>
    /// <param name="envelope">The immutable turn to queue.</param>
    public void Enqueue(TurnEnvelope envelope)
    {
        if (envelope is null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        lock (_sync)
        {
            if (_state is ChatExecutionState.Pausing or ChatExecutionState.Stopping)
            {
                throw new InvalidOperationException("A turn cannot be queued while the chat is stopping.");
            }

            _queue.Enqueue(envelope);
        }
    }

    /// <summary>Starts the next queued turn, if the chat is idle.</summary>
    /// <param name="cancellationToken">A token that cancels the start request.</param>
    /// <returns>The active turn, or <see langword="null"/> when the queue is empty.</returns>
    public async Task<ActiveTurn?> StartNextAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TurnEnvelope envelope;
            lock (_sync)
            {
                EnsureState(ChatExecutionState.Idle);
                if (_queue.Count == 0)
                {
                    return null;
                }

                envelope = _queue.Dequeue();
                _state = ChatExecutionState.Starting;
            }

            try
            {
                var activeTurn = await _transport.StartAsync(ChatId, envelope, cancellationToken).ConfigureAwait(false);
                lock (_sync)
                {
                    _activeTurn = activeTurn;
                    _state = ChatExecutionState.Running;
                }

                return activeTurn;
            }
            catch
            {
                lock (_sync)
                {
                    _queue.Enqueue(envelope);
                    _state = ChatExecutionState.Faulted;
                }

                throw;
            }
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    /// <summary>Delivers a steering prompt to the exact active turn without silently queueing it.</summary>
    /// <param name="prompt">The steering input.</param>
    /// <param name="cancellationToken">A token that cancels the steering request.</param>
    /// <returns>A task representing steering acknowledgement.</returns>
    public async Task SteerAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("A steering prompt is required.", nameof(prompt));
        }

        if (!_transport.Capabilities.CanSteer)
        {
            throw new NotSupportedException("The selected Codex transport does not support steering.");
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ActiveTurn activeTurn;
            lock (_sync)
            {
                EnsureState(ChatExecutionState.Running);
                activeTurn = _activeTurn ?? throw new InvalidOperationException(MissingActiveTurnMessage);
                _state = ChatExecutionState.Steering;
            }

            try
            {
                await _transport.SteerAsync(ChatId, activeTurn, prompt, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_sync)
                {
                    if (_state == ChatExecutionState.Steering)
                    {
                        _state = ChatExecutionState.Running;
                    }
                }
            }
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    /// <summary>Stops the active turn and preserves queued work.</summary>
    /// <param name="cancellationToken">A token that cancels the stop request.</param>
    /// <returns>A task representing interrupt acknowledgement.</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ActiveTurn activeTurn;
            lock (_sync)
            {
                EnsureState(ChatExecutionState.Running);
                activeTurn = _activeTurn ?? throw new InvalidOperationException(MissingActiveTurnMessage);
                _state = ChatExecutionState.Stopping;
            }

            try
            {
                await _transport.InterruptAsync(ChatId, activeTurn, cancellationToken).ConfigureAwait(false);
                lock (_sync)
                {
                    _activeTurn = null;
                    _state = ChatExecutionState.Idle;
                }
            }
            catch
            {
                lock (_sync)
                {
                    _state = ChatExecutionState.Faulted;
                }

                throw;
            }
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    /// <summary>Interrupts the active turn, saves its context, and reports paused only after the save succeeds.</summary>
    /// <param name="conversationSummary">The compact conversation summary.</param>
    /// <param name="pauseReason">The user or lifecycle reason for pausing.</param>
    /// <param name="createdAt">The checkpoint creation time.</param>
    /// <param name="cancellationToken">A token that cancels interrupt or persistence.</param>
    /// <returns>The durable checkpoint identifier.</returns>
    public async Task<string> PauseAsync(
        string conversationSummary,
        string pauseReason,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ActiveTurn activeTurn;
            IReadOnlyList<TurnEnvelope> queuedTurns;
            lock (_sync)
            {
                EnsureState(ChatExecutionState.Running);
                activeTurn = _activeTurn ?? throw new InvalidOperationException(MissingActiveTurnMessage);
                queuedTurns = _queue.ToArray();
                _state = ChatExecutionState.Pausing;
            }

            try
            {
                await _transport.InterruptAsync(ChatId, activeTurn, cancellationToken).ConfigureAwait(false);
                var checkpoint = new PauseCheckpoint(
                    ChatId,
                    activeTurn,
                    queuedTurns,
                    conversationSummary,
                    pauseReason,
                    createdAt);
                var checkpointId = await _checkpointStore.SaveAsync(checkpoint, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(checkpointId))
                {
                    throw new InvalidOperationException("The checkpoint store returned no durable identifier.");
                }

                lock (_sync)
                {
                    _activeTurn = null;
                    _checkpointId = checkpointId;
                    _state = ChatExecutionState.Paused;
                }

                return checkpointId;
            }
            catch
            {
                lock (_sync)
                {
                    _state = ChatExecutionState.CheckpointFailed;
                }

                throw;
            }
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    /// <summary>Restores and resumes this chat's durable checkpoint.</summary>
    /// <param name="cancellationToken">A token that cancels retrieval or resume.</param>
    /// <returns>The correlated resumed active turn.</returns>
    public async Task<ActiveTurn> ResumeAsync(CancellationToken cancellationToken)
    {
        if (!_transport.Capabilities.CanResume)
        {
            throw new NotSupportedException("The selected Codex transport does not support checkpoint resume.");
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string checkpointId;
            lock (_sync)
            {
                EnsureState(ChatExecutionState.Paused);
                checkpointId = _checkpointId ?? throw new InvalidOperationException("The chat has no durable checkpoint.");
                _state = ChatExecutionState.Resuming;
            }

            try
            {
                var checkpoint = await _checkpointStore.LoadAsync(checkpointId, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(checkpoint.ChatId, ChatId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The checkpoint belongs to another chat.");
                }

                var activeTurn = await _transport.ResumeAsync(checkpoint, cancellationToken).ConfigureAwait(false);
                lock (_sync)
                {
                    _activeTurn = activeTurn;
                    _checkpointId = null;
                    _state = ChatExecutionState.Running;
                }

                return activeTurn;
            }
            catch
            {
                lock (_sync)
                {
                    _state = ChatExecutionState.Paused;
                }

                throw;
            }
        }
        finally
        {
            _ = _operationGate.Release();
        }
    }

    /// <summary>Marks the exact active operation complete and ignores stale completion events.</summary>
    /// <param name="operationId">The terminal operation identifier.</param>
    /// <returns><see langword="true"/> when the active turn was completed; otherwise, <see langword="false"/>.</returns>
    public bool TryComplete(string operationId)
    {
        lock (_sync)
        {
            if (_activeTurn is null
                || !string.Equals(_activeTurn.OperationId, operationId, StringComparison.Ordinal)
                || (_state is not ChatExecutionState.Running and not ChatExecutionState.Steering))
            {
                return false;
            }

            _activeTurn = null;
            _state = ChatExecutionState.Idle;
            return true;
        }
    }

    /// <summary>Clears all queued turns without affecting another chat.</summary>
    public void ClearQueue()
    {
        lock (_sync)
        {
            _queue.Clear();
        }
    }

    /// <summary>Requires the chat to be in an expected state.</summary>
    /// <param name="expected">The required execution state.</param>
    private void EnsureState(ChatExecutionState expected)
    {
        if (_state == expected)
        {
            return;
        }

        throw new InvalidOperationException($"Chat '{ChatId}' must be {expected}, but it is {_state}.");
    }
}
