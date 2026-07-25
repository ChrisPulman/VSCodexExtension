// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using TUnit.Assertions.Enums;
using VSCodex.Core.Chats;

namespace VSCodex.Tests;

/// <summary>Verifies isolated chat execution, steering, queueing, stopping, and pause persistence.</summary>
public sealed class ChatRunCoordinatorTests
{
    /// <summary>Named string used by this type.</summary>
    private const string ActiveText = "active";

    /// <summary>Named string used by this type.</summary>
    private const string ChatAText = "chat-a";

    /// <summary>Named string used by this type.</summary>
    private const string ChatBText = "chat-b";

    /// <summary>Named string used by this type.</summary>
    private const string Checkpoint1Text = "checkpoint-1";

    /// <summary>Named string used by this type.</summary>
    private const string PauseText = "pause";

    /// <summary>Named string used by this type.</summary>
    private const string QueuedText = "queued";

    /// <summary>Named string used by this type.</summary>
    private const string SummaryText = "summary";

    /// <summary>Verifies that queued turns retain FIFO order and their submission-time snapshots.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Queue_is_fifo_and_captures_immutable_turn_inputs()
    {
        var transport = new RecordingTransport();
        var checkpointStore = new RecordingCheckpointStore();
        var coordinator = new ChatRunCoordinator(ChatAText, transport, checkpointStore);
        var firstAttachments = new List<string> { "one.cs" };
        var first = new TurnEnvelope("first", "workspace-a", "context-a", "settings-a", firstAttachments);
        var second = new TurnEnvelope("second", "workspace-b", "context-b", "settings-b");

        coordinator.Enqueue(first);
        coordinator.Enqueue(second);
        firstAttachments.Add("late.cs");

        var activeFirst = await coordinator.StartNextAsync(CancellationToken.None);
        var firstCompleted = coordinator.TryComplete(activeFirst!.OperationId);
        var activeSecond = await coordinator.StartNextAsync(CancellationToken.None);

        await Assert.That(firstCompleted).IsTrue();
        await Assert.That(activeFirst.Envelope.Prompt).IsEqualTo("first");
        await Assert.That(activeFirst.Envelope.Attachments).Count().IsEqualTo(1);
        await Assert.That(activeSecond!.Envelope.Prompt).IsEqualTo("second");
        await Assert.That(activeSecond.Envelope.WorkspaceId).IsEqualTo("workspace-b");
    }

    /// <summary>Verifies that steering targets the active turn and is never converted into queueing.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Steer_targets_the_active_turn_without_queueing()
    {
        var transport = new RecordingTransport();
        var coordinator = new ChatRunCoordinator(ChatAText, transport, new RecordingCheckpointStore());
        coordinator.Enqueue(NewTurn("initial"));
        var active = await coordinator.StartNextAsync(CancellationToken.None);

        await coordinator.SteerAsync("change direction", CancellationToken.None);

        await Assert.That(transport.Steers).Count().IsEqualTo(1);
        await Assert.That(transport.Steers[0].OperationId).IsEqualTo(active!.OperationId);
        await Assert.That(transport.Steers[0].Prompt).IsEqualTo("change direction");
        await Assert.That(coordinator.QueuedTurns).IsEmpty();
        await Assert.That(coordinator.State).IsEqualTo(ChatExecutionState.Running);
    }

    /// <summary>Verifies that an unsupported steering request stays explicit and preserves the draft responsibility.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Unsupported_steer_fails_instead_of_silently_queueing()
    {
        var transport = new RecordingTransport(canSteer: false);
        var coordinator = new ChatRunCoordinator(ChatAText, transport, new RecordingCheckpointStore());
        coordinator.Enqueue(NewTurn("initial"));
        _ = await coordinator.StartNextAsync(CancellationToken.None);

        await Assert.That(async () => await coordinator.SteerAsync("draft", CancellationToken.None))
            .Throws<NotSupportedException>();
        await Assert.That(coordinator.QueuedTurns).IsEmpty();
    }

    /// <summary>Verifies that stop interrupts only the owning chat and preserves its queued turns.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Stop_is_scoped_and_preserves_the_queue()
    {
        var transportA = new RecordingTransport();
        var transportB = new RecordingTransport();
        var checkpointStore = new RecordingCheckpointStore();
        var chatA = new ChatRunCoordinator(ChatAText, transportA, checkpointStore);
        var chatB = new ChatRunCoordinator(ChatBText, transportB, checkpointStore);
        chatA.Enqueue(NewTurn("a-active"));
        chatA.Enqueue(NewTurn("a-next"));
        chatB.Enqueue(NewTurn("b-active"));
        var activeA = await chatA.StartNextAsync(CancellationToken.None);
        var activeB = await chatB.StartNextAsync(CancellationToken.None);

        await chatA.StopAsync(CancellationToken.None);

        await Assert.That(transportA.Interrupts).IsEquivalentTo([activeA!.OperationId]);
        await Assert.That(transportB.Interrupts).IsEmpty();
        await Assert.That(chatA.QueuedTurns).Count().IsEqualTo(1);
        await Assert.That(chatA.State).IsEqualTo(ChatExecutionState.Idle);
        await Assert.That(chatB.ActiveTurn!.OperationId).IsEqualTo(activeB!.OperationId);
        await Assert.That(chatB.State).IsEqualTo(ChatExecutionState.Running);
    }

    /// <summary>Verifies that pause reports success only after the exact turn is interrupted and ReactiveMemory is durable.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Pause_interrupts_then_saves_complete_correlated_context()
    {
        var order = new List<string>();
        var transport = new RecordingTransport(order: order);
        var checkpointStore = new RecordingCheckpointStore(order);
        var coordinator = new ChatRunCoordinator(ChatAText, transport, checkpointStore);
        coordinator.Enqueue(NewTurn(ActiveText));
        coordinator.Enqueue(NewTurn(QueuedText));
        var active = await coordinator.StartNextAsync(CancellationToken.None);

        var checkpointId = await coordinator.PauseAsync(
            "conversation summary",
            "user requested",
            new(2026, 7, 24, 22, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        await Assert.That(order).IsEquivalentTo(["start", "interrupt", "checkpoint"], CollectionOrdering.Matching);
        await Assert.That(checkpointId).IsEqualTo(Checkpoint1Text);
        await Assert.That(coordinator.State).IsEqualTo(ChatExecutionState.Paused);
        await Assert.That(coordinator.ActiveTurn).IsNull();
        await Assert.That(checkpointStore.Saved!.ChatId).IsEqualTo(ChatAText);
        await Assert.That(checkpointStore.Saved.ActiveTurn.OperationId).IsEqualTo(active!.OperationId);
        await Assert.That(checkpointStore.Saved.QueuedTurns).Count().IsEqualTo(1);
        await Assert.That(checkpointStore.Saved.ConversationSummary).IsEqualTo("conversation summary");
    }

    /// <summary>Verifies that a checkpoint failure never presents the chat as paused.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Checkpoint_failure_is_visible_and_never_claims_paused()
    {
        var checkpointStore = new RecordingCheckpointStore { SaveFailure = new IOException("memory unavailable") };
        var coordinator = new ChatRunCoordinator(ChatAText, new RecordingTransport(), checkpointStore);
        coordinator.Enqueue(NewTurn(ActiveText));
        _ = await coordinator.StartNextAsync(CancellationToken.None);

        await Assert.That(async () => await coordinator.PauseAsync(
                SummaryText,
                PauseText,
                TimeProvider.System.GetUtcNow(),
                CancellationToken.None))
            .Throws<IOException>();
        await Assert.That(coordinator.State).IsEqualTo(ChatExecutionState.CheckpointFailed);
        await Assert.That(coordinator.CheckpointId).IsNull();
    }

    /// <summary>Verifies that chat selection does not cancel or transfer another chat's active turn.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Switching_chats_keeps_execution_owned_by_the_originating_chat()
    {
        var workspace = new ChatWorkspace();
        var checkpointStore = new RecordingCheckpointStore();
        var chatA = new ChatRunCoordinator(ChatAText, new RecordingTransport(), checkpointStore);
        var chatB = new ChatRunCoordinator(ChatBText, new RecordingTransport(), checkpointStore);
        workspace.Add(chatA);
        workspace.Add(chatB);
        chatA.Enqueue(NewTurn("active-a"));
        var activeA = await chatA.StartNextAsync(CancellationToken.None);

        var selected = workspace.Select(ChatBText);

        await Assert.That(selected.ChatId).IsEqualTo(ChatBText);
        await Assert.That(workspace.SelectedChatId).IsEqualTo(ChatBText);
        await Assert.That(chatA.ActiveTurn!.OperationId).IsEqualTo(activeA!.OperationId);
        await Assert.That(chatA.State).IsEqualTo(ChatExecutionState.Running);
        await Assert.That(chatB.State).IsEqualTo(ChatExecutionState.Idle);
    }

    /// <summary>Verifies idle, invalid-state, and queue-clear boundaries.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Empty_and_invalid_state_operations_are_explicit()
    {
        var coordinator = new ChatRunCoordinator(ChatAText, new RecordingTransport(), new RecordingCheckpointStore());

        var empty = await coordinator.StartNextAsync(CancellationToken.None);
        await Assert.That(empty).IsNull();
        await Assert.That(async () => await coordinator.StopAsync(CancellationToken.None)).Throws<InvalidOperationException>();

        coordinator.Enqueue(NewTurn("discard me"));
        coordinator.ClearQueue();
        await Assert.That(coordinator.QueuedTurns).IsEmpty();
    }

    /// <summary>Verifies that a failed start is visible and does not lose its queued turn.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Start_failure_requeues_the_turn_and_faults_the_chat()
    {
        var transport = new RecordingTransport { StartFailure = new IOException("transport unavailable") };
        var coordinator = new ChatRunCoordinator(ChatAText, transport, new RecordingCheckpointStore());
        coordinator.Enqueue(NewTurn("retain me"));

        await Assert.That(async () => await coordinator.StartNextAsync(CancellationToken.None)).Throws<IOException>();
        await Assert.That(coordinator.State).IsEqualTo(ChatExecutionState.Faulted);
        await Assert.That(coordinator.QueuedTurns).Count().IsEqualTo(1);
        await Assert.That(coordinator.QueuedTurns[0].Prompt).IsEqualTo("retain me");
    }

    /// <summary>Verifies that an interrupt failure is reported without discarding queued or active ownership.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Stop_failure_faults_without_losing_active_or_queued_work()
    {
        var transport = new RecordingTransport { InterruptFailure = new IOException("interrupt unavailable") };
        var coordinator = new ChatRunCoordinator(ChatAText, transport, new RecordingCheckpointStore());
        coordinator.Enqueue(NewTurn(ActiveText));
        coordinator.Enqueue(NewTurn(QueuedText));
        var active = await coordinator.StartNextAsync(CancellationToken.None);

        await Assert.That(async () => await coordinator.StopAsync(CancellationToken.None)).Throws<IOException>();
        await Assert.That(coordinator.State).IsEqualTo(ChatExecutionState.Faulted);
        await Assert.That(coordinator.ActiveTurn!.OperationId).IsEqualTo(active!.OperationId);
        await Assert.That(coordinator.QueuedTurns).Count().IsEqualTo(1);
    }

    /// <summary>Verifies a complete durable pause and resume round trip.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Resume_restores_the_exact_checkpoint_and_preserves_queue()
    {
        var transport = new RecordingTransport();
        var checkpointStore = new RecordingCheckpointStore();
        var coordinator = new ChatRunCoordinator(ChatAText, transport, checkpointStore);
        coordinator.Enqueue(NewTurn(ActiveText));
        coordinator.Enqueue(NewTurn(QueuedText));
        var interrupted = await coordinator.StartNextAsync(CancellationToken.None);
        _ = await coordinator.PauseAsync(SummaryText, PauseText, TimeProvider.System.GetUtcNow(), CancellationToken.None);

        var resumed = await coordinator.ResumeAsync(CancellationToken.None);

        await Assert.That(resumed.ThreadId).IsEqualTo(interrupted!.ThreadId);
        await Assert.That(resumed.Envelope.Id).IsEqualTo(interrupted.Envelope.Id);
        await Assert.That(coordinator.CheckpointId).IsNull();
        await Assert.That(coordinator.State).IsEqualTo(ChatExecutionState.Running);
        await Assert.That(coordinator.QueuedTurns).Count().IsEqualTo(1);
    }

    /// <summary>Verifies that resume capability and checkpoint ownership failures remain explicit.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Resume_rejects_unsupported_transport_and_cross_chat_checkpoint()
    {
        var unsupported = new ChatRunCoordinator(
            "chat-unsupported",
            new RecordingTransport(canResume: false),
            new RecordingCheckpointStore());
        await Assert.That(async () => await unsupported.ResumeAsync(CancellationToken.None)).Throws<NotSupportedException>();

        var checkpointStore = new RecordingCheckpointStore();
        var coordinator = new ChatRunCoordinator(ChatAText, new RecordingTransport(), checkpointStore);
        coordinator.Enqueue(NewTurn(ActiveText));
        _ = await coordinator.StartNextAsync(CancellationToken.None);
        _ = await coordinator.PauseAsync(SummaryText, PauseText, TimeProvider.System.GetUtcNow(), CancellationToken.None);
        checkpointStore.LoadedChatId = "another-chat";

        await Assert.That(async () => await coordinator.ResumeAsync(CancellationToken.None)).Throws<InvalidOperationException>();
        await Assert.That(coordinator.State).IsEqualTo(ChatExecutionState.Paused);
        await Assert.That(coordinator.CheckpointId).IsEqualTo(Checkpoint1Text);
    }

    /// <summary>Verifies a checkpoint store cannot report success without a durable identifier.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Empty_checkpoint_identifier_is_a_visible_failure()
    {
        var checkpointStore = new RecordingCheckpointStore { CheckpointId = string.Empty };
        var coordinator = new ChatRunCoordinator(ChatAText, new RecordingTransport(), checkpointStore);
        coordinator.Enqueue(NewTurn(ActiveText));
        _ = await coordinator.StartNextAsync(CancellationToken.None);

        await Assert.That(async () => await coordinator.PauseAsync(
                SummaryText,
                PauseText,
                TimeProvider.System.GetUtcNow(),
                CancellationToken.None))
            .Throws<InvalidOperationException>();
        await Assert.That(coordinator.State).IsEqualTo(ChatExecutionState.CheckpointFailed);
    }

    /// <summary>Verifies stale terminal events and invalid registration cannot mutate unrelated work.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Stale_completion_and_invalid_workspace_registration_are_rejected()
    {
        var coordinator = new ChatRunCoordinator(ChatAText, new RecordingTransport(), new RecordingCheckpointStore());
        coordinator.Enqueue(NewTurn(ActiveText));
        var active = await coordinator.StartNextAsync(CancellationToken.None);

        await Assert.That(coordinator.TryComplete("stale-operation")).IsFalse();
        await Assert.That(coordinator.ActiveTurn!.OperationId).IsEqualTo(active!.OperationId);
        await Assert.That(coordinator.TryComplete(active.OperationId)).IsTrue();
        await Assert.That(coordinator.TryComplete(active.OperationId)).IsFalse();

        var workspace = new ChatWorkspace();
        workspace.Add(coordinator);
        await Assert.That(() => workspace.Add(coordinator)).Throws<InvalidOperationException>();
        await Assert.That(() => workspace.Select("missing")).Throws<KeyNotFoundException>();
        await Assert.That(() => workspace.Get("missing")).Throws<KeyNotFoundException>();
        await Assert.That(workspace.Get(ChatAText)).IsEqualTo(coordinator);
    }

    /// <summary>Verifies public inputs fail fast instead of creating ambiguous chat work.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Test]
    public async Task Invalid_chat_turn_and_steer_inputs_fail_fast()
    {
        var transport = new RecordingTransport();
        var checkpointStore = new RecordingCheckpointStore();

        await Assert.That(() => new ChatRunCoordinator(string.Empty, transport, checkpointStore)).Throws<ArgumentException>();
        await Assert.That(() => new ChatRunCoordinator("chat", null!, checkpointStore)).Throws<ArgumentNullException>();
        await Assert.That(() => new ChatRunCoordinator("chat", transport, null!)).Throws<ArgumentNullException>();
        await Assert.That(() => new TurnEnvelope(string.Empty, "workspace", "context", "settings")).Throws<ArgumentException>();

        var coordinator = new ChatRunCoordinator(ChatAText, transport, checkpointStore);
        await Assert.That(() => coordinator.Enqueue(null!)).Throws<ArgumentNullException>();
        coordinator.Enqueue(NewTurn(ActiveText));
        _ = await coordinator.StartNextAsync(CancellationToken.None);
        await Assert.That(async () => await coordinator.SteerAsync(" ", CancellationToken.None)).Throws<ArgumentException>();
    }

    /// <summary>Performs the new Turn operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The new Turn result.</returns>
    private static TurnEnvelope NewTurn(string prompt) => new(prompt, "workspace", "context", "settings");

    /// <summary>Provides the recording Transport implementation.</summary>
    private sealed class RecordingTransport : IChatRunTransport
    {
        /// <summary>Stores the order.</summary>
        private readonly List<string>? _order;

        /// <summary>Stores the sequence.</summary>
        private int _sequence;

        /// <summary>Initializes a new instance of the <see cref="RecordingTransport"/> class.</summary>
        /// <param name="canSteer">The can Steer.</param>
        /// <param name="canResume">The can Resume.</param>
        /// <param name="order">The order.</param>
        internal RecordingTransport(bool canSteer = true, bool canResume = true, List<string>? order = null)
        {
            Capabilities = new(canSteer, canResume);
            _order = order;
        }

        /// <summary>Gets the capabilities.</summary>
        public ChatTransportCapabilities Capabilities { get; }

        /// <summary>Gets the steers.</summary>
        internal List<(string OperationId, string Prompt)> Steers { get; } = new();

        /// <summary>Gets the interrupts.</summary>
        internal List<string> Interrupts { get; } = new();

        /// <summary>Gets or sets the start Failure.</summary>
        internal Exception? StartFailure { get; set; }

        /// <summary>Gets or sets the interrupt Failure.</summary>
        internal Exception? InterruptFailure { get; set; }

        /// <summary>Starts the operation.</summary>
        /// <param name="chatId">The chat Id.</param>
        /// <param name="envelope">The envelope.</param>
        /// <param name="cancellationToken">The cancellation Token.</param>
        /// <returns>A task whose result contains the operation result.</returns>
        public Task<ActiveTurn> StartAsync(string chatId, TurnEnvelope envelope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (StartFailure is not null)
            {
                return Task.FromException<ActiveTurn>(StartFailure);
            }

            _order?.Add("start");
            _sequence++;
            return Task.FromResult(new ActiveTurn($"operation-{chatId}-{_sequence}", $"thread-{chatId}", $"turn-{_sequence}", envelope));
        }

        /// <summary>Performs the steer operation.</summary>
        /// <param name="chatId">The chat Id.</param>
        /// <param name="activeTurn">The active Turn.</param>
        /// <param name="prompt">The prompt.</param>
        /// <param name="cancellationToken">The cancellation Token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task SteerAsync(string chatId, ActiveTurn activeTurn, string prompt, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Steers.Add((activeTurn.OperationId, prompt));
            return Task.CompletedTask;
        }

        /// <summary>Interrupts the operation.</summary>
        /// <param name="chatId">The chat Id.</param>
        /// <param name="activeTurn">The active Turn.</param>
        /// <param name="cancellationToken">The cancellation Token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task InterruptAsync(string chatId, ActiveTurn activeTurn, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (InterruptFailure is not null)
            {
                return Task.FromException(InterruptFailure);
            }

            Interrupts.Add(activeTurn.OperationId);
            _order?.Add("interrupt");
            return Task.CompletedTask;
        }

        /// <summary>Resumes the operation.</summary>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <param name="cancellationToken">The cancellation Token.</param>
        /// <returns>A task whose result contains the operation result.</returns>
        public Task<ActiveTurn> ResumeAsync(PauseCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sequence++;
            return Task.FromResult(new ActiveTurn(
                $"operation-{checkpoint.ChatId}-{_sequence}",
                checkpoint.ActiveTurn.ThreadId,
                $"turn-{_sequence}",
                checkpoint.ActiveTurn.Envelope));
        }
    }

    /// <summary>Provides the recording Checkpoint Store implementation.</summary>
    private sealed class RecordingCheckpointStore : IPauseCheckpointStore
    {
        /// <summary>Stores the order.</summary>
        private readonly List<string>? _order;

        /// <summary>Stores the saved.</summary>
        private PauseCheckpoint? _saved;

        /// <summary>Initializes a new instance of the <see cref="RecordingCheckpointStore"/> class.</summary>
        /// <param name="order">The order.</param>
        internal RecordingCheckpointStore(List<string>? order = null)
        {
            _order = order;
        }

        /// <summary>Gets or sets the save Failure.</summary>
        internal Exception? SaveFailure { get; set; }

        /// <summary>Gets the saved.</summary>
        internal PauseCheckpoint? Saved => _saved;

        /// <summary>Gets or sets the checkpoint Id.</summary>
        internal string CheckpointId { get; set; } = "checkpoint-1";

        /// <summary>Gets or sets the loaded Chat Id.</summary>
        internal string? LoadedChatId { get; set; }

        /// <summary>Saves the operation.</summary>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <param name="cancellationToken">The cancellation Token.</param>
        /// <returns>A task whose result contains the operation result.</returns>
        public Task<string> SaveAsync(PauseCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (SaveFailure is not null)
            {
                return Task.FromException<string>(SaveFailure);
            }

            _saved = checkpoint;
            _order?.Add("checkpoint");
            return Task.FromResult(CheckpointId);
        }

        /// <summary>Loads the operation.</summary>
        /// <param name="checkpointId">The checkpoint Id.</param>
        /// <param name="cancellationToken">The cancellation Token.</param>
        /// <returns>A task whose result contains the operation result.</returns>
        public Task<PauseCheckpoint> LoadAsync(string checkpointId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var checkpoint = _saved ?? throw new InvalidOperationException("No checkpoint has been saved.");
            return LoadedChatId is null ? Task.FromResult(checkpoint) : Task.FromResult(new PauseCheckpoint(
                LoadedChatId,
                checkpoint.ActiveTurn,
                checkpoint.QueuedTurns,
                checkpoint.ConversationSummary,
                checkpoint.PauseReason,
                checkpoint.CreatedAt));
        }
    }
}
