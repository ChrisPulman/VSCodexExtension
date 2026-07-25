// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI;

namespace VSCodex.ViewModels;

/// <summary>Builds and updates the Codex run-activity presentation.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Creates agent Plan Prompt.</summary>
    private void CreateAgentPlanPrompt()
    {
        string goal = (string.IsNullOrWhiteSpace(Prompt) ? "Plan the selected coding task from current Visual Studio solution context." : Prompt);
        RefreshAgentPlanPreview(goal);
        Prompt = _assistantContext.BuildPlanPrompt(goal, BuildAgentSummary());
        Mode = CodexRunMode.Plan;
        SelectedToolTabIndex = Numeric6;
        IsToolPanelOpen = true;
        Status = "Prepared VSCodex agent plan";
    }

    /// <summary>Refreshes agent Plan Preview.</summary>
    /// <param name="goal">The goal.</param>
    private void RefreshAgentPlanPreview(string goal)
    {
        List<AgentRoleDefinition> agents = DistinctAgentRoles(AgentRoles.Where((x) => x.IsEnabled)).ToList();
        if (agents.Count == 0)
        {
            agents = DefaultAgentRoles();
        }

        List<OrchestrationTaskSection> sections = new[]
        {
            (
                Preferred: "Planner",
                Title: "Clarify goal and acceptance criteria",
                Description: "Use the current Visual Studio solution, selected code, references, memories, and MCP tools to define the work."),
            (
                Preferred: "Architect",
                Title: "Assess architecture and integration risks",
                Description: "Identify affected projects, services, UI surfaces, threading risks, and compatibility constraints before editing."),
            (
                Preferred: "Builder",
                Title: "Implement focused changes",
                Description: "Apply the smallest coherent code changes needed for the requested outcome."),
            (
                Preferred: "Reviewer",
                Title: "Review behavior, UX, and safety",
                Description: "Check correctness, regressions, user-visible behavior, and missing coverage."),
            (
                Preferred: "Verifier",
                Title: "Validate in Visual Studio and command-line tests",
                Description: "Run the relevant build, test, VSIX, and interactive Visual Studio checks, then summarize evidence.")
        }.Select((section, index) => new OrchestrationTaskSection
        {
            Index = index + 1,
            Title = section.Title,
            Description = $"{section.Description}{Environment.NewLine}Goal: {(string.IsNullOrWhiteSpace(goal) ? "Use current context." : goal)}",
            AssignedAgent = PickAgentName(agents, section.Preferred, index),
            DependsOnSectionId = ((index == 0) ? string.Empty : "previous")
        }).ToList();
        Replace(OrchestrationSections, sections);
    }

    /// <summary>Copies message To Clipboard.</summary>
    /// <param name="message">The message.</param>
    private void CopyMessageToClipboard(ChatMessage? message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.Content))
        {
            Status = "No message content to copy";
            return;
        }

        try
        {
            Clipboard.SetText(message.Content);
            Status = "Copied VSCodex message";
        }
        catch (Exception ex)
        {
            Status = $"Could not copy message: {ex.Message}";
        }
    }

    /// <summary>Copies activity Detail To Clipboard.</summary>
    /// <param name="node">The node.</param>
    private void CopyActivityDetailToClipboard(RunActivityNode? node)
    {
        if (node is null || string.IsNullOrWhiteSpace(node.Detail))
        {
            Status = "No activity text to copy";
            return;
        }

        try
        {
            Clipboard.SetText(node.Detail);
            Status = "Copied VSCodex response text";
        }
        catch (Exception ex)
        {
            Status = $"Could not copy response text: {ex.Message}";
        }
    }

    /// <summary>Performs the use Message As Prompt operation.</summary>
    /// <param name="message">The message.</param>
    private void UseMessageAsPrompt(ChatMessage? message)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.Content))
        {
            Status = "No message content to use";
            return;
        }

        Prompt = message.Content;
        Status = ((message.Role == CodexMessageRole.User) ? "Copied user prompt back to input" : "Copied message back to input");
    }

    /// <summary>Opens activity File.</summary>
    /// <param name="node">The node.</param>
    private void OpenActivityFile(RunActivityNode? node)
    {
        if (node is null || string.IsNullOrWhiteSpace(node.FilePath))
        {
            Status = "No file is associated with this activity";
            return;
        }

        if (node.IsDeleted || !File.Exists(node.FilePath))
        {
            Status = $"Changed file no longer exists: {node.FilePath}";
            return;
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo
            {
                FileName = node.FilePath,
                UseShellExecute = true
            });
            Status = $"Opened {node.FilePath}";
        }
        catch (Exception ex)
        {
            Status = $"Could not open changed file: {ex.Message}";
        }
    }

    /// <summary>Performs the begin Run Activity operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The begin Run Activity result.</returns>
    private RunActivityNode BeginRunActivity(string prompt)
    {
        DateTimeOffset started = _timeProvider.GetLocalNow();
        RunActivityNode root = new RunActivityNode
        {
            Kind = RunActivityKind.User,
            Title = "User request",
            Detail = (prompt ?? string.Empty),
            StartedAt = started,
            IsExpanded = true
        };
        UpdateRunActivityElapsed(root);
        AddDefaultActivitySections(root);
        RunActivityRoots.Add(root);
        _activeRunActivity = root;
        return root;
    }

    /// <summary>Adds default Activity Sections.</summary>
    /// <param name="root">The root.</param>
    private void AddDefaultActivitySections(RunActivityNode root)
    {
        root.Children.Add(CreateSection(RunActivityKind.Agent, "Agent actions"));
        root.Children.Add(CreateSection(RunActivityKind.Mcp, "MCP usage"));
        root.Children.Add(CreateSection(RunActivityKind.Skill, "Skill usage"));
        root.Children.Add(CreateSection(RunActivityKind.Files, "Files changed"));
        root.Children.Add(CreateSection(RunActivityKind.Assistant, "Assistant response"));
        root.Children.Add(CreateSection(RunActivityKind.System, "System prompts and diagnostics"));
    }

    /// <summary>Creates section.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="title">The title.</param>
    /// <returns>The create Section result.</returns>
    private RunActivityNode CreateSection(RunActivityKind kind, string title)
    {
        return new RunActivityNode
        {
            Kind = kind,
            Title = title,
            StartedAt = _timeProvider.GetLocalNow(),
            IsExpanded = (kind != RunActivityKind.System)
        };
    }

    /// <summary>Performs the current Activity Root operation.</summary>
    /// <returns>The current Activity Root result.</returns>
    private RunActivityNode? CurrentActivityRoot()
    {
        return _activeRunActivity ?? RunActivityRoots.LastOrDefault();
    }

    /// <summary>Gets activity Section.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The get Activity Section result.</returns>
    private RunActivityNode GetActivitySection(RunActivityKind kind)
    {
        RunActivityNode? root = CurrentActivityRoot();
        root ??= BeginRunActivity("System activity");

        RunActivityKind sectionKind = ((kind == RunActivityKind.File) ? RunActivityKind.Files : kind);
        RunActivityNode? section = root.Children.FirstOrDefault((child) => child.Kind == sectionKind);
        if (section is not null)
        {
            return section;
        }

        section = CreateSection(sectionKind, ActivitySectionTitle(sectionKind));
        RunActivityNode system = root.Children.FirstOrDefault((child) => child.Kind == RunActivityKind.System);
        if (system is null)
        {
            root.Children.Add(section);
        }
        else
        {
            root.Children.Insert(Math.Max(0, root.Children.IndexOf(system)), section);
        }

        return section;
    }

    /// <summary>Adds run Activity.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="title">The title.</param>
    /// <param name="detail">The detail.</param>
    /// <param name="filePath">The file Path.</param>
    /// <param name="isDeleted">The is Deleted.</param>
    /// <returns>The add Run Activity result.</returns>
    private RunActivityNode AddRunActivity(RunActivityKind kind, string title, string detail = "", string filePath = "", bool isDeleted = false)
    {
        RunActivityNode node = new RunActivityNode
        {
            Kind = (string.IsNullOrWhiteSpace(filePath) ? kind : RunActivityKind.File),
            Title = (title ?? string.Empty),
            Detail = (detail ?? string.Empty),
            FilePath = (filePath ?? string.Empty),
            IsDeleted = isDeleted,
            StartedAt = _timeProvider.GetLocalNow(),
            IsExpanded = (kind != RunActivityKind.System)
        };
        RunOnUiThread(() =>
        {
            RunActivityNode activitySection = GetActivitySection(kind);
            activitySection.Children.Add(node);
            activitySection.IsExpanded = true;
        });
        return node;
    }

    /// <summary>Performs the append Message To Activity Tree operation.</summary>
    /// <param name="message">The message.</param>
    private void AppendMessageToActivityTree(ChatMessage message)
    {
        if (message.IsTransient)
        {
            return;
        }

        if (message.Role == CodexMessageRole.User)
        {
            bool suppressPendingActivity =
                !string.IsNullOrWhiteSpace(_pendingUserActivityPromptToSuppress) &&
                _activeRunActivity is not null &&
                string.Equals(_activeRunActivity.Detail, message.Content, StringComparison.Ordinal) &&
                string.Equals(_pendingUserActivityPromptToSuppress, message.Content, StringComparison.Ordinal);
            if (suppressPendingActivity)
            {
                _pendingUserActivityPromptToSuppress = string.Empty;
            }
            else
            {
                _ = BeginRunActivity(message.Content);
            }
        }
        else
        {
            _ = AddRunActivity(ActivityKindForRole(message.Role), ActivityTitleForRole(message.Role), message.Content);
        }
    }

    /// <summary>Performs the rebuild Activity Tree From Messages operation.</summary>
    private void RebuildActivityTreeFromMessages()
    {
        RunActivityRoots.Clear();
        _activeRunActivity = null;
        _activeProgressNode = null;
        _pendingUserActivityPromptToSuppress = string.Empty;
        foreach (ChatMessage message in Messages)
        {
            AppendMessageToActivityTree(message);
        }
    }

    /// <summary>Updates run Activity Elapsed.</summary>
    /// <param name="root">The root.</param>
    private void UpdateRunActivityElapsed(RunActivityNode? root)
    {
        if (root is null)
        {
            return;
        }

        DateTimeOffset end = root.CompletedAt ?? _timeProvider.GetLocalNow();
        root.ElapsedText = $"started {root.StartedAt.LocalDateTime:HH':'mm':'ss} | elapsed {FormatElapsed(end - root.StartedAt)}";
    }

    /// <summary>Adds changed Files Activity.</summary>
    /// <param name="files">The files.</param>
    private void AddChangedFilesActivity(IReadOnlyList<ChangedFileActivity> files)
    {
        RunOnUiThread(() =>
        {
            RunActivityNode activitySection = GetActivitySection(RunActivityKind.Files);
            activitySection.Children.Clear();
            if (files.Count == 0)
            {
                activitySection.Children.Add(new RunActivityNode
                {
                    Kind = RunActivityKind.Files,
                    Title = "No changed files detected",
                    Detail = "Git did not report workspace file changes after this request.",
                    StartedAt = _timeProvider.GetLocalNow()
                });
            }
            else
            {
                foreach (ChangedFileActivity current in files.OrderBy((file) => file.RelativePath, StringComparer.OrdinalIgnoreCase))
                {
                    activitySection.Children.Add(new RunActivityNode
                    {
                        Kind = RunActivityKind.File,
                        Title = current.RelativePath,
                        Detail = current.Status,
                        FilePath = current.FullPath,
                        IsDeleted = current.IsDeleted,
                        StartedAt = _timeProvider.GetLocalNow(),
                        IsExpanded = false
                    });
                }

                activitySection.IsExpanded = true;
            }
        });
    }

    /// <summary>Handles a Codex event.</summary>
    /// <param name="ev">The ev.</param>
    private void OnCodexEvent(CodexEvent ev)
    {
        RunOnUiThread(() =>
        {
            UpdateRateLimitsFromJson(ev.RawJson);
            if (!string.IsNullOrWhiteSpace(ev.ThreadId))
            {
                ThreadId = ev.ThreadId;
            }

            if (ev.Type == "turn-started")
            {
                _activeTurnId = ev.TurnId ?? string.Empty;
                _activeOperationId = ev.OperationId ?? _activeOperationId;
                this.RaisePropertyChanged();
            }

            if (ev.Type == "approval-request")
            {
                JObject jsonObject = JObject.Parse(ev.RawJson);
                string text = jsonObject.Value<string>("method") ?? string.Empty;
                ApprovalRequest item = new ApprovalRequest
                {
                    Id = (jsonObject.Value<string>("requestId") ?? Guid.NewGuid().ToString("N")),
                    Method = text,
                    ToolName = text,
                    ArgumentsPreview = (jsonObject["params"]?.ToString() ?? string.Empty),
                    Reason = ev.Message
                };
                ApprovalRequests.Add(item);
                _ = AddMessage(CodexMessageRole.Approval, ev.Message, persist: false);
                Status = ev.Message;
            }
            else if (ev.Type == "assistant-delta")
            {
                _activeStreamingResponse ??= AddRunActivity(RunActivityKind.Assistant, "Codex response", string.Empty);

                _activeStreamingResponse.Detail += ev.Message;
                Status = "Codex is responding...";
            }
            else if (ev.Type == "stdout" || ev.Type == "message")
            {
                _ = AddMessage(CodexMessageRole.Assistant, ev.Message);
            }
            else if (ev.Type == "fallback" || ev.Type == "transport-fallback" || ev.Type == "stderr" || ev.Type == "app-server-stderr" || ev.Type == "bridge-output")
            {
                _ = AddMessage(CodexMessageRole.System, $"[{ev.Type}] {ev.Message}");
            }
            else if (ev.Type == "progress")
            {
                SetRunProgress(ev.Message);
            }
            else
            {
                Status = ev.Message;
            }
        });
    }

    /// <summary>Handles an orchestration event.</summary>
    /// <param name="ev">The ev.</param>
    private void OnOrchestrationEvent(OrchestrationEvent ev)
    {
        RunOnUiThread(() =>
        {
            Status = ev.Message;
            if (ev.Type == "plan-created" && _taskOrchestrator.CurrentPlan is not null)
            {
                Replace(OrchestrationSections, _taskOrchestrator.CurrentPlan.Sections);
            }

            if (ev.Section is not null && !OrchestrationSections.Any((x) => x.Id == ev.Section.Id))
            {
                OrchestrationSections.Add(ev.Section);
            }

            _ = AddMessage(CodexMessageRole.System, $"[orchestration:{ev.Type}] {ev.Message}");
        });
    }

    /// <summary>Adds message.</summary>
    /// <param name="role">The role.</param>
    /// <param name="content">The content.</param>
    /// <param name="persist">The persist.</param>
    /// <param name="appendToActivity">The append To Activity.</param>
    /// <returns>The add Message result.</returns>
    private ChatMessage AddMessage(CodexMessageRole role, string content, bool persist = true, bool appendToActivity = true)
    {
        ChatMessage message = new ChatMessage
        {
            Role = role,
            Content = (content ?? string.Empty),
            IsTransient = !persist
        };
        RunOnUiThread(() =>
        {
            Messages.Add(message);
            if (appendToActivity)
            {
                AppendMessageToActivityTree(message);
            }

            if (message.IsTransient)
            {
                return;
            }

            _session.Messages.Add(message);
            _session.Updated = message.Timestamp;
            if (role != CodexMessageRole.User || !string.IsNullOrWhiteSpace(_session.Title))
            {
                return;
            }

            _session.Title = CompactLine(content, Numeric90);
        });
        return message;
    }

    /// <summary>Starts run Progress.</summary>
    /// <param name="stage">The stage.</param>
    /// <returns>The start Run Progress result.</returns>
    private IDisposable StartRunProgress(string stage)
    {
        _activeRunStartedAt = _timeProvider.GetLocalNow();
        _activeRunStage = stage;
        _activeProgressNode = AddRunActivity(RunActivityKind.Agent, "VSCodex is working", BuildRunProgressMessage(stage));
        return Observable.Interval(TimeSpan.FromSeconds(Numeric15Point0), _uiScheduler).Subscribe(_ => RefreshRunProgress());
    }

    /// <summary>Sets run Progress.</summary>
    /// <param name="stage">The stage.</param>
    private void SetRunProgress(string stage)
    {
        RunOnUiThread(() =>
        {
            _activeRunStage = (string.IsNullOrWhiteSpace(stage) ? _activeRunStage : stage);
            Status = _activeRunStage;
            RefreshRunProgress();
        });
    }

    /// <summary>Refreshes run Progress.</summary>
    private void RefreshRunProgress()
    {
        if (_activeProgressNode is null || !IsRunning)
        {
            return;
        }

        _activeProgressNode.Title = (string.IsNullOrWhiteSpace(_activeRunStage) ? "VSCodex is working" : _activeRunStage);
        _activeProgressNode.Detail = BuildRunProgressMessage(_activeRunStage);
        UpdateRunActivityElapsed(_activeRunActivity);
    }

    /// <summary>Performs the finish Run Progress operation.</summary>
    /// <param name="stage">The stage.</param>
    private void FinishRunProgress(string stage)
    {
        RunOnUiThread(() =>
        {
            if (_activeProgressNode is not null)
            {
                _activeProgressNode.Title = (string.IsNullOrWhiteSpace(stage) ? "VSCodex complete" : stage);
                _activeProgressNode.Detail = BuildRunProgressMessage(stage);
            }

            _activeProgressNode = null;
            _activeRunStage = string.Empty;
        });
    }

    /// <summary>Builds run Progress Message.</summary>
    /// <param name="stage">The stage.</param>
    /// <returns>The build Run Progress Message result.</returns>
    private string BuildRunProgressMessage(string stage)
    {
        TimeSpan elapsed = ((_activeRunStartedAt == default(DateTimeOffset))
            ? TimeSpan.Zero
            : (_timeProvider.GetLocalNow() - _activeRunStartedAt));
        string workspace = (string.IsNullOrWhiteSpace(_workspace.CurrentWorkspaceRoot) ? "Waiting for Visual Studio workspace" : _workspace.CurrentWorkspaceRoot);
        string currentStage = (string.IsNullOrWhiteSpace(stage) ? "Preparing request" : stage);
        return $"**VSCodex is working**{Environment.NewLine}{Environment.NewLine}" +
            $"- Status: {currentStage}{Environment.NewLine}" +
            $"- Elapsed: {FormatElapsed(elapsed)}{Environment.NewLine}" +
            $"- Workspace: {workspace}";
    }
}
