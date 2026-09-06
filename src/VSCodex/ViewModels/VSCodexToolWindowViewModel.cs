// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI;

namespace VSCodex.ViewModels;

/// <summary>Coordinates prompt submission, steering, queuing, interruption, pause, and resume.</summary>
public sealed partial class VSCodexToolWindowViewModel : ReactiveObject, IDisposable
{
    /// <summary>Submits prompt.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SubmitPromptAsync()
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        if (TryHandleLocalSlashCommand(Prompt))
        {
            return;
        }

        bool flag = !IsRunning;
        if (flag)
        {
            flag = !(await EnsureCodexSdkReadyForRunAsync().ConfigureAwait(continueOnCapturedContext: true));
        }

        if (flag)
        {
            return;
        }

        string userPrompt = ExpandAssistantSlashCommand(Prompt);
        if (IsMcpDiscoveryPrompt(userPrompt))
        {
            ShowMcpServerList();
            SelectedToolTabIndex = Numeric4;
            IsToolPanelOpen = true;
            return;
        }

        if (!IsRunning)
        {
            Refresh();
            if (!EnsureWorkspaceReadyForRun())
            {
                return;
            }
        }
        else if (string.IsNullOrWhiteSpace(_workspace.CurrentWorkspaceRoot))
        {
            Status = "Visual Studio project context is still loading";
            return;
        }

        Prompt = string.Empty;
        if (IsRunning && _settingsStore.Current.DefaultFollowUpBehavior == FollowUpBehavior.Steer)
        {
            await SteerPromptTextAsync(userPrompt).ConfigureAwait(continueOnCapturedContext: true);
        }
        else
        {
            EnqueuePrompt(userPrompt);
        }
    }

    /// <summary>Submits alternate Follow Up.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SubmitAlternateFollowUpAsync()
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        string userPrompt = ExpandAssistantSlashCommand(Prompt);
        if (string.IsNullOrWhiteSpace(userPrompt) || !IsRunning)
        {
            return;
        }

        Prompt = string.Empty;
        if (_settingsStore.Current.DefaultFollowUpBehavior == FollowUpBehavior.Queue)
        {
            await SteerPromptTextAsync(userPrompt).ConfigureAwait(continueOnCapturedContext: true);
        }
        else
        {
            EnqueuePrompt(userPrompt);
        }
    }

    /// <summary>Performs the steer Prompt operation.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SteerPromptAsync()
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        string userPrompt = ExpandAssistantSlashCommand(Prompt);
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return;
        }

        Prompt = string.Empty;
        await SteerPromptTextAsync(userPrompt).ConfigureAwait(continueOnCapturedContext: true);
    }

    /// <summary>Performs the steer Prompt Text operation.</summary>
    /// <param name="userPrompt">The user Prompt.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SteerPromptTextAsync(string userPrompt)
    {
        if (!IsRunning || string.IsNullOrWhiteSpace(ThreadId))
        {
            Prompt = userPrompt;
            Status = "Steer is available after the active Codex turn has started.";
            return;
        }

        try
        {
            string? threadId = ThreadId;
            if (threadId is null || threadId.Trim().Length == 0)
            {
                throw new InvalidOperationException("The active Codex turn has no thread identifier.");
            }

            await _codex.SteerAsync(threadId, userPrompt).ConfigureAwait(continueOnCapturedContext: false);
            _ = AddMessage(CodexMessageRole.User, userPrompt);
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            Status = "Guidance added to the active Codex turn.";
        }
        catch (Exception ex)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            Prompt = userPrompt;
            Status = $"Could not steer the active turn: {ex.Message}";
        }
    }

    /// <summary>Performs the queue Prompt operation.</summary>
    private void QueuePrompt()
    {
        string userPrompt = ExpandAssistantSlashCommand(Prompt);
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return;
        }

        Prompt = string.Empty;
        EnqueuePrompt(userPrompt);
    }

    /// <summary>Performs the enqueue Prompt operation.</summary>
    /// <param name="userPrompt">The user Prompt.</param>
    private void EnqueuePrompt(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return;
        }

        _queuedPrompts.Enqueue(userPrompt);
        QueuedPromptCount = _queuedPrompts.Count;
        if (_isProcessingRunQueue)
        {
            Status = $"Queued VSCodex request ({QueuedPromptCount} waiting). You can keep editing the next prompt.";
            return;
        }

        _isProcessingRunQueue = true;
        IsRunning = true;
        Status = "Running VSCodex...";
        TaskObserver.FireAndForget(_joinableTaskFactory.RunAsync((Func<Task>)ProcessRunQueueAsync).Task);
    }

    /// <summary>Stops active Run.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task StopActiveRunAsync()
    {
        _stopRequested = true;
        _taskOrchestrator.Cancel();
        try
        {
            await _codex.InterruptAsync(ThreadId).ConfigureAwait(continueOnCapturedContext: false);
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            Status = (HasQueuedPrompts ? "Stopped the active VSCodex turn; queued follow-ups are preserved." : "Stopped the active VSCodex turn.");
        }
        catch (Exception ex)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            _stopRequested = false;
            Status = $"Could not stop the active VSCodex turn: {ex.Message}";
        }
    }

    /// <summary>Pauses active Run.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task PauseActiveRunAsync()
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        if (!IsRunning || string.IsNullOrWhiteSpace(ThreadId) || string.IsNullOrWhiteSpace(_activeTurnId))
        {
            Status = "Pause is available after the active Codex turn has started.";
            return;
        }

        string threadId = ThreadId ?? string.Empty;
        ReactiveMemoryPauseCheckpoint checkpoint = CreatePauseCheckpoint(threadId);
        _pauseRequested = true;
        Status = "Interrupting the active turn before saving its ReactiveMemory checkpoint...";
        try
        {
            await _codex.InterruptAsync(threadId).ConfigureAwait(continueOnCapturedContext: false);
            ReactiveMemoryPauseCheckpointResult saved = await _reactiveMemory.SavePauseCheckpointAsync(checkpoint, _lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            if (!saved.Success || saved.Checkpoint is null)
            {
                _pauseRequested = false;
                _stopRequested = true;
                Status = $"The turn was interrupted, but its ReactiveMemory checkpoint was not saved: {saved.Message}";
                _ = AddMessage(CodexMessageRole.Error, Status, persist: false);
                return;
            }

            _pausedCheckpoint = saved.Checkpoint;
            IsPaused = true;
            Status = "Paused. Context and queued follow-ups are saved in ReactiveMemory.";
            _ = AddMessage(CodexMessageRole.Memory, Status, persist: false);
        }
        catch (Exception ex)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            _pauseRequested = false;
            _stopRequested = true;
            Status = $"Pause failed: {ex.Message}";
            _ = AddMessage(CodexMessageRole.Error, Status, persist: false);
        }
    }

    /// <summary>Creates the durable checkpoint for the active turn.</summary>
    /// <param name="threadId">The active thread identifier.</param>
    /// <returns>The populated checkpoint.</returns>
    private ReactiveMemoryPauseCheckpoint CreatePauseCheckpoint(string threadId)
    {
        WorkspaceIdentity identity =
            CloneWorkspaceIdentity(_workspace.CurrentWorkspaceIdentity) ?? new WorkspaceIdentity();
        ChatMessage? lastAssistant = Messages.LastOrDefault(
            (message) => message.Role == CodexMessageRole.Assistant);
        ReactiveMemoryPauseCheckpoint checkpoint = new()
        {
            WorkspaceIdentityId = identity.Id,
            WorkspaceName = identity.Name,
            WorkspaceRoot = identity.RootPath,
            MemoryRoot = identity.MemoryRoot,
            ChatId = _session.Id,
            ThreadId = threadId,
            TurnId = _activeTurnId,
            OperationId = _activeOperationId,
            PauseReason = "User paused the active VSCodex turn.",
            Prompt = _activePrompt,
            PartialResponse = _activeStreamingResponse?.Detail ?? lastAssistant?.Content ?? string.Empty,
            Context = BuildPauseContext()
        };
        checkpoint.QueuedPrompts.AddRange(_queuedPrompts);
        return checkpoint;
    }

    /// <summary>Resumes paused Run.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ResumePausedRunAsync()
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        ReactiveMemoryPauseCheckpoint? checkpoint = _pausedCheckpoint;
        if (!IsPaused || checkpoint is null)
        {
            Status = "There is no paused VSCodex checkpoint to resume.";
            return;
        }

        Status = "Restoring the paused context from ReactiveMemory...";
        ReactiveMemoryPauseCheckpointResult restored = await _reactiveMemory.RestorePauseCheckpointAsync(
            new ReactiveMemoryPauseCheckpointQuery
            {
                CheckpointId = checkpoint.CheckpointId,
                MemoryDrawerId = checkpoint.MemoryDrawerId,
                WorkspaceIdentityId = checkpoint.WorkspaceIdentityId,
                WorkspaceName = checkpoint.WorkspaceName,
                WorkspaceRoot = checkpoint.WorkspaceRoot,
                MemoryRoot = checkpoint.MemoryRoot,
                ChatId = checkpoint.ChatId,
                ThreadId = checkpoint.ThreadId,
                TurnId = checkpoint.TurnId,
                OperationId = checkpoint.OperationId
            },
            _lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        if (!restored.Success || restored.Checkpoint is null)
        {
            Status = $"Could not restore the paused ReactiveMemory checkpoint: {restored.Message}";
            return;
        }

        ReactiveMemoryPauseCheckpoint restoredCheckpoint = restored.Checkpoint;
        _queuedPrompts.Clear();
        _queuedPrompts.Enqueue(BuildResumePrompt(restoredCheckpoint));
        foreach (string queuedPrompt in restoredCheckpoint.QueuedPrompts.Where((prompt) => !string.IsNullOrWhiteSpace(prompt)))
        {
            _queuedPrompts.Enqueue(queuedPrompt);
        }

        QueuedPromptCount = _queuedPrompts.Count;
        _pausedCheckpoint = null;
        _pauseRequested = false;
        IsPaused = false;
        _isProcessingRunQueue = true;
        IsRunning = true;
        Status = "Resuming VSCodex from its ReactiveMemory checkpoint...";
        TaskObserver.FireAndForget(_joinableTaskFactory.RunAsync((Func<Task>)ProcessRunQueueAsync).Task);
    }

    /// <summary>Builds the continuation prompt stored for a paused turn.</summary>
    /// <param name="checkpoint">The restored checkpoint.</param>
    /// <returns>The continuation prompt.</returns>
    private string BuildResumePrompt(ReactiveMemoryPauseCheckpoint checkpoint)
    {
        string separator = Environment.NewLine + Environment.NewLine;
        return string.Concat(
            "Resume the interrupted task from the durable ReactiveMemory checkpoint. ",
            "Continue the original request without repeating completed work.",
            separator,
            "Original request:",
            Environment.NewLine,
            checkpoint.Prompt,
            separator,
            "Saved context:",
            Environment.NewLine,
            checkpoint.Context);
    }

    /// <summary>Performs the process Run Queue operation.</summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ProcessRunQueueAsync()
    {
        try
        {
            while (true)
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
                if (_queuedPrompts.Count != 0 && !_pauseRequested && !_stopRequested)
                {
                    string nextPrompt = _queuedPrompts.Dequeue();
                    QueuedPromptCount = _queuedPrompts.Count;
                    await ProcessQueuedPromptAsync(nextPrompt).ConfigureAwait(continueOnCapturedContext: true);
                    continue;
                }

                break;
            }
        }
        finally
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            _isProcessingRunQueue = false;
            QueuedPromptCount = _queuedPrompts.Count;
            bool shouldRemainStopped = _pauseRequested || _stopRequested;
            _stopRequested = false;
            if (QueuedPromptCount > 0 && !shouldRemainStopped)
            {
                _isProcessingRunQueue = true;
                IsRunning = true;
                TaskObserver.FireAndForget(_joinableTaskFactory.RunAsync((Func<Task>)ProcessRunQueueAsync).Task);
            }
            else
            {
                IsRunning = false;
            }
        }
    }

    /// <summary>Processes one queued prompt against the active Visual Studio workspace.</summary>
    /// <param name="userPrompt">The user prompt.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ProcessQueuedPromptAsync(string userPrompt)
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
        QueuedPromptContext? context = PrepareQueuedPrompt(userPrompt);
        if (context is null)
        {
            return;
        }

        IDisposable progressSubscription = StartRunProgress(
            $"Preparing request for {context.WorkspaceRoot}");
        try
        {
            await ExecuteQueuedPromptAsync(context).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception exception)
        {
            HandleQueuedPromptFailure(context, exception);
        }
        finally
        {
            progressSubscription.Dispose();
            ResetQueuedPromptState();
        }
    }

    /// <summary>Prepares immutable state for one queued prompt.</summary>
    /// <param name="userPrompt">The user prompt.</param>
    /// <returns>The prepared context, or <see langword="null"/> when no workspace is available.</returns>
    private QueuedPromptContext? PrepareQueuedPrompt(string userPrompt)
    {
        _workspace.RefreshWorkspaceIdentity();
        RaiseWorkspaceDisplayProperties();
        if (!EnsureWorkspaceReadyForRun())
        {
            _ = AddMessage(
                CodexMessageRole.Error,
                "VSCodex cannot run because Visual Studio has not provided a solution or repository folder project root yet.");
            return null;
        }

        ApplyCurrentWorkspaceToSession();
        _pendingUserActivityPromptToSuppress = userPrompt;
        string? threadId = ThreadId;
        return new QueuedPromptContext
        {
            Prompt = userPrompt,
            RunRoot = BeginRunActivity(userPrompt),
            WorkspaceRoot = _workspace.CurrentWorkspaceRoot,
            WorkspaceName = _workspace.CurrentWorkspaceName,
            SolutionPath = _workspace.CurrentSolutionPath,
            MemoryRoot = _workspace.CurrentWorkspaceMemoryRoot,
            WorkspaceIdentity = CloneWorkspaceIdentity(_workspace.CurrentWorkspaceIdentity)
                ?? new WorkspaceIdentity(),
            ThreadId = threadId,
            HashReferences = _workspace.ResolveHashReferences(userPrompt, 0),
            SelectedAgents = ApplyModelSelection(AgentRoles.Where((agent) => agent.IsEnabled)).ToList(),
            Skills = Skills.Where((skill) => skill.IsEnabled).ToList(),
            McpServers = McpServers.Where((server) => server.IsEnabled).ToList(),
            Attachments = Attachments.ToList(),
            Options = CreateRunOptions()
        };
    }

    /// <summary>Creates a stable option snapshot for a queued prompt.</summary>
    /// <returns>The run option snapshot.</returns>
    private CodexRunOptions CreateRunOptions()
    {
        string effectiveModel = EffectiveMainModel();
        CodexRunMode mode = Mode;
        string failoverModel = FailoverModel;
        ApprovalPolicy approvalPolicy = ApprovalPolicy;
        SandboxMode sandboxMode = SandboxMode;
        CodexTransportKind transport = Transport;
        bool useAgents = UseMultiAgentOrchestration;
        int agentConcurrency = MaxAgentConcurrency;
        AgentExecutionStrategy strategy = AgentStrategy;
        bool budgetSelection = BudgetDrivenModelSelection;
        string budgetModel = BudgetModel;
        return new CodexRunOptions
        {
            Mode = mode,
            Model = effectiveModel,
            FailoverModel = failoverModel,
            ReasoningEffort = CodexModelCatalog.ResolveReasoningEffort(
                effectiveModel,
                SelectedReasoning),
            Verbosity = SelectedVerbosity,
            ApprovalPolicy = approvalPolicy,
            SandboxMode = sandboxMode,
            Transport = transport,
            UseMultiAgentOrchestration = useAgents,
            MaxAgentConcurrency = agentConcurrency,
            AgentStrategy = strategy,
            OrchestrationModel = EffectiveOrchestrationModel(),
            BudgetDrivenModelSelection = budgetSelection,
            BudgetModel = budgetModel
        };
    }

    /// <summary>Executes one prepared queued prompt.</summary>
    /// <param name="context">The prepared run context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ExecuteQueuedPromptAsync(QueuedPromptContext context)
    {
        Status = QueuedPromptCount > 0
            ? $"Running VSCodex ({QueuedPromptCount} queued). You can keep editing the next prompt."
            : "Running VSCodex...";
        _ = AddMessage(CodexMessageRole.User, context.Prompt);
        AddRunSetupActivities(context);
        ReactiveMemoryCallResult memoryReaction =
            await ReactToMemoryAsync(context).ConfigureAwait(continueOnCapturedContext: false);
        CodexRunRequest request =
            await BuildRunRequestAsync(context, memoryReaction).ConfigureAwait(continueOnCapturedContext: false);
        _activeOperationId = request.OperationId;
        _activePrompt = context.Prompt;
        await UpdateModelEstimateAsync(request).ConfigureAwait(continueOnCapturedContext: false);
        SetRunProgress("Sending request to Codex. Longer project analysis can take several minutes.");
        Task<CodexRunResult> runTask = context.Options.UseMultiAgentOrchestration
            ? _taskOrchestrator.RunAsync(request)
            : _codex.RunAsync(request);
        CodexRunResult result = await runTask.ConfigureAwait(continueOnCapturedContext: false);
        if (_pauseRequested || _stopRequested)
        {
            FinishRunProgress(_pauseRequested ? "Paused" : "Stopped");
            return;
        }

        await ProcessCodexResultAsync(context, result).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>Adds setup activity rows for a queued prompt.</summary>
    /// <param name="context">The prepared run context.</param>
    private void AddRunSetupActivities(QueuedPromptContext context)
    {
        _ = AddRunActivity(
            RunActivityKind.Agent,
            "Run started",
            $"Mode: {context.Options.Mode}{Environment.NewLine}Model: {context.Options.Model}");
        AddNamedRunActivities(
            context.SelectedAgents.Select((agent) => $"- {agent.Name} ({agent.Role})"),
            RunActivityKind.Agent,
            "Selected agents");
        AddNamedRunActivities(
            context.Skills.Select((skill) => $"- {skill.Name}"),
            RunActivityKind.Skill,
            "Enabled skills");
        AddNamedRunActivities(
            context.McpServers.Select((server) => $"- {server.Name}"),
            RunActivityKind.Mcp,
            "Available MCP servers");
    }

    /// <summary>Adds a run activity when the supplied values are not empty.</summary>
    /// <param name="values">The formatted activity values.</param>
    /// <param name="kind">The activity kind.</param>
    /// <param name="title">The activity title.</param>
    private void AddNamedRunActivities(
        IEnumerable<string> values,
        RunActivityKind kind,
        string title)
    {
        string[] rows = values.ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        _ = AddRunActivity(kind, title, string.Join(Environment.NewLine, rows));
    }

    /// <summary>Loads ReactiveMemory context for a queued prompt.</summary>
    /// <param name="context">The prepared run context.</param>
    /// <returns>The ReactiveMemory result.</returns>
    private async Task<ReactiveMemoryCallResult> ReactToMemoryAsync(QueuedPromptContext context)
    {
        SetRunProgress("Updating ReactiveMemory context");
        ReactiveMemoryCallResult reaction = await _reactiveMemory.ReactToPromptAsync(
            context.Prompt,
            context.WorkspaceIdentity,
            context.ThreadId).ConfigureAwait(continueOnCapturedContext: false);
        SetRunProgress(reaction.Success
            ? reaction.Message
            : "ReactiveMemory unavailable; continuing with local context");
        _ = AddRunActivity(
            RunActivityKind.Mcp,
            "ReactiveMemory prompt context",
            reaction.Message,
            string.Empty);
        if (!string.IsNullOrWhiteSpace(reaction.ContextText))
        {
            _ = AddMessage(
                CodexMessageRole.Memory,
                "Recovered ReactiveMemory context for this project.",
                persist: false);
        }

        return reaction;
    }

    /// <summary>Builds the orchestrator request for a queued prompt.</summary>
    /// <param name="context">The prepared run context.</param>
    /// <param name="memoryReaction">The ReactiveMemory result.</param>
    /// <returns>The prepared request.</returns>
    private async Task<CodexRunRequest> BuildRunRequestAsync(
        QueuedPromptContext context,
        ReactiveMemoryCallResult memoryReaction)
    {
        SetRunProgress("Resolving VSCodex references and attachments");
        List<WorkspaceFileReference> workspaceFiles = await Task.Run(
            () => ResolveWorkspaceFiles(context)).ConfigureAwait(continueOnCapturedContext: false);
        AddNamedRunActivities(
            workspaceFiles.Select((file) => $"- {file.ReferenceKey}"),
            RunActivityKind.Agent,
            "Resolved prompt references");
        IReadOnlyList<MemoryEntry> memories = await Task.Run(
            () => _memoryStore.Search(context.Prompt, Numeric10))
            .ConfigureAwait(continueOnCapturedContext: false);
        return new CodexRunRequest
        {
            Prompt = context.Prompt,
            ThreadId = context.ThreadId,
            WorkspaceRoot = context.WorkspaceRoot,
            WorkspaceName = context.WorkspaceName,
            WorkspaceSolutionPath = context.SolutionPath,
            WorkspaceMemoryRoot = context.MemoryRoot,
            ReactiveMemoryContext = memoryReaction.ContextText,
            WorkspaceIdentity = context.WorkspaceIdentity,
            Options = context.Options,
            Attachments = context.Attachments,
            Skills = context.Skills,
            Memories = memories,
            McpServers = context.McpServers,
            WorkspaceFiles = workspaceFiles,
            AgentRoles = context.SelectedAgents
        };
    }

    /// <summary>Resolves and de-duplicates prompt workspace references.</summary>
    /// <param name="context">The prepared run context.</param>
    /// <returns>The resolved references.</returns>
    private List<WorkspaceFileReference> ResolveWorkspaceFiles(QueuedPromptContext context)
    {
        return _workspace.ResolveMentions(context.Prompt, Numeric12000)
            .Concat(context.HashReferences)
            .GroupBy(
                (reference) => string.IsNullOrWhiteSpace(reference.ReferenceKey)
                    ? reference.Path
                    : reference.ReferenceKey,
                StringComparer.OrdinalIgnoreCase)
            .Select((group) => group.First())
            .ToList();
    }

    /// <summary>Updates the model estimate for a prepared request.</summary>
    /// <param name="request">The prepared request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task UpdateModelEstimateAsync(CodexRunRequest request)
    {
        ModelUsageEstimate estimate = await Task.Run(
            () => _modelAnalytics.Estimate(request)).ConfigureAwait(continueOnCapturedContext: false);
        RunOnUiThread(() =>
        {
            ModelEstimate = estimate;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
        });
    }

    /// <summary>Processes the completed Codex result.</summary>
    /// <param name="context">The prepared run context.</param>
    /// <param name="result">The completed result.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ProcessCodexResultAsync(
        QueuedPromptContext context,
        CodexRunResult result)
    {
        RunOnUiThread(() =>
        {
            UpdateRateLimitsFromJson(result.RawJson);
            ThreadId = result.ThreadId ?? ThreadId;
        });
        CompleteStreamingResponse(result);
        _ = AddMessage(
            CodexMessageRole.Assistant,
            result.FinalResponse,
            persist: true,
            _activeStreamingResponse is null);
        await SaveRunDiaryAsync(context, result).ConfigureAwait(continueOnCapturedContext: false);
        SetRunProgress("Collecting changed files");
        IReadOnlyList<ChangedFileActivity> changedFiles = await Task.Run(
            () => CollectChangedFilesForWorkspace(context.WorkspaceRoot))
            .ConfigureAwait(continueOnCapturedContext: false);
        AddChangedFilesActivity(changedFiles);
        FinishRunProgress(result.UsedFallback ? "Completed using CLI fallback" : "Completed");
        CompleteQueuedPromptOnUiThread(context, result);
    }

    /// <summary>Finalizes the streaming response node.</summary>
    /// <param name="result">The completed result.</param>
    private void CompleteStreamingResponse(CodexRunResult result)
    {
        if (_activeStreamingResponse is null)
        {
            return;
        }

        _activeStreamingResponse.Title = "Final assistant response";
        _activeStreamingResponse.Detail = result.FinalResponse;
    }

    /// <summary>Writes the completed run to the ReactiveMemory diary.</summary>
    /// <param name="context">The prepared run context.</param>
    /// <param name="result">The completed result.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SaveRunDiaryAsync(
        QueuedPromptContext context,
        CodexRunResult result)
    {
        SetRunProgress("Saving ReactiveMemory diary");
        ReactiveMemoryCallResult diary = await _reactiveMemory.WriteDiaryAsync(
            context.Prompt,
            result.FinalResponse,
            context.WorkspaceIdentity,
            ThreadId).ConfigureAwait(continueOnCapturedContext: false);
        _ = AddRunActivity(RunActivityKind.Mcp, "ReactiveMemory diary", diary.Message);
        if (diary.Success)
        {
            return;
        }

        _ = AddMessage(
            CodexMessageRole.Memory,
            $"ReactiveMemory diary was not saved: {diary.Message}",
            persist: false);
    }

    /// <summary>Completes a queued prompt on the UI thread.</summary>
    /// <param name="context">The prepared run context.</param>
    /// <param name="result">The completed result.</param>
    private void CompleteQueuedPromptOnUiThread(
        QueuedPromptContext context,
        CodexRunResult result)
    {
        RunOnUiThread(() =>
        {
            Status = result.UsedFallback ? "Complete using CLI fallback" : "Complete";
            context.RunRoot.CompletedAt = _timeProvider.GetLocalNow();
            UpdateRunActivityElapsed(context.RunRoot);
            _session.ThreadId = ThreadId;
            if (string.IsNullOrWhiteSpace(_session.Title))
            {
                _session.Title = DeriveSessionTitle(_session);
            }

            _sessionStore.Save(_session);
            RefreshHistory();
        });
    }

    /// <summary>Records a queued prompt failure.</summary>
    /// <param name="context">The prepared run context.</param>
    /// <param name="exception">The run failure.</param>
    private void HandleQueuedPromptFailure(
        QueuedPromptContext context,
        Exception exception)
    {
        FinishRunProgress($"Failed: {exception.Message}");
        _ = AddMessage(CodexMessageRole.Error, exception.ToString());
        RunOnUiThread(() =>
        {
            context.RunRoot.CompletedAt = _timeProvider.GetLocalNow();
            UpdateRunActivityElapsed(context.RunRoot);
            Status = $"Failed: {exception.Message}";
        });
    }

    /// <summary>Clears transient state after one queued prompt.</summary>
    private void ResetQueuedPromptState()
    {
        _activeRunActivity = null;
        _activeProgressNode = null;
        _activeStreamingResponse = null;
        _pendingUserActivityPromptToSuppress = string.Empty;
        if (IsPaused)
        {
            return;
        }

        _activeTurnId = string.Empty;
        _activeOperationId = string.Empty;
        _activePrompt = string.Empty;
    }

    /// <summary>Builds pause Context.</summary>
    /// <returns>The build Pause Context result.</returns>
    private string BuildPauseContext()
    {
        IEnumerable<string> entries = Messages
            .Where((message) => !message.IsTransient)
            .Reverse()
            .Take(Numeric40)
            .Reverse()
            .Select((message) => $"{message.Role}: {message.Content}");
        return string.Join(Environment.NewLine + Environment.NewLine, entries);
    }

    /// <summary>Performs the respond To Approval operation.</summary>
    /// <param name="request">The request.</param>
    /// <param name="approve">The approve.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task RespondToApprovalAsync(ApprovalRequest request, bool approve)
    {
        if (request?.IsPending != true)
        {
            return;
        }

        if (!IsApprovalRequest(request.Method))
        {
            Status = "This Codex request requires structured input that is not a yes/no approval.";
            return;
        }

        try
        {
            await _codex.RespondToServerRequestAsync(request.Id, request.Method, approve).ConfigureAwait(continueOnCapturedContext: false);
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            request.IsPending = false;
            _ = ApprovalRequests.Remove(request);
            Status = (approve ? "Approved Codex request." : "Declined Codex request.");
        }
        catch (Exception ex)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            Status = $"Could not answer the Codex approval request: {ex.Message}";
        }
    }

    /// <summary>Determines whether a server request can be answered by an approval decision.</summary>
    /// <param name="method">The server request method.</param>
    /// <returns><see langword="true"/> when the request supports approve or decline.</returns>
    private static bool IsApprovalRequest(string method)
    {
        return method.EndsWith("/requestApproval", StringComparison.Ordinal)
            || method.Equals("mcpServer/elicitation/request", StringComparison.Ordinal);
    }

    /// <summary>Refreshes the operation.</summary>
    private void Refresh()
    {
        try
        {
            Status = "Refreshing VSCodex workspace context...";
            string previousIdentity = _lastWorkspaceIdentityId;
            _workspace.Refresh();
            RaiseWorkspaceDisplayProperties();
            string currentIdentity = _workspace.CurrentWorkspaceIdentity.Id;
            if (!string.IsNullOrWhiteSpace(currentIdentity) && !string.Equals(_lastWorkspaceSettingsId, currentIdentity, StringComparison.OrdinalIgnoreCase))
            {
                _lastWorkspaceSettingsId = currentIdentity;
                ApplySettingsFromStore(_settingsStore.LoadForWorkspace(_workspace.CurrentWorkspaceIdentity));
            }

            if (!string.IsNullOrWhiteSpace(previousIdentity)
                && !string.Equals(
                    previousIdentity,
                    currentIdentity,
                    StringComparison.OrdinalIgnoreCase))
            {
                ThreadId = null;
                _codex.Cancel();
            }

            _lastWorkspaceIdentityId = currentIdentity;
            _memoryStore.LoadWorkspace(_workspace.CurrentWorkspaceRoot);
            string workspaceRoot = _workspace.CurrentWorkspaceRoot ?? string.Empty;
            _skillIndex.Refresh(
                _settingsStore.Current.SkillRoots.Concat(
                    [Path.Combine(workspaceRoot, ".codex", "skills")]));
            _mcpConfig.Refresh();
            Status = string.IsNullOrWhiteSpace(_workspace.CurrentWorkspaceRoot)
                ? "Visual Studio project context is still loading"
                : $"Refreshed VSCodex context for {_workspace.CurrentWorkspaceRoot}";
            RefreshRateLimitsInBackground();
        }
        catch (Exception ex)
        {
            Status = $"Refresh failed: {ex.Message}";
        }
    }
}
