// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using VSCodex.Core.Automations;

namespace VSCodex.ViewModels;

/// <summary>Composes opt-in schedules and durable unread results with the tool window.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Defines the scheduler polling interval.</summary>
    private const int AutomationPollSeconds = 30;

    /// <summary>Defines the default recurrence.</summary>
    private const int DefaultAutomationMinutes = 60;

    /// <summary>Defines the persisted schedule filename.</summary>
    private const string AutomationStateFile = "automations.json";

    /// <summary>Coordinates fresh state transactions and claims across Visual Studio instances.</summary>
    private readonly LocalAutomationCoordinator _automationCoordinator = new(Path.Combine(LocalPaths.AppRoot, AutomationStateFile));

    /// <summary>Stores the polling timer.</summary>
    private DispatcherTimer? _automationTimer;

    /// <summary>Stores the scheduler.</summary>
    private LocalAutomationRunner? _automationRunner;

    /// <summary>Stores the editable name.</summary>
    private string _automationName = string.Empty;

    /// <summary>Stores the editable prompt.</summary>
    private string _automationPrompt = string.Empty;

    /// <summary>Stores the editable interval.</summary>
    private int _automationMinutes = DefaultAutomationMinutes;

    /// <summary>Stores the automation-specific operational status.</summary>
    private string _automationStatus = string.Empty;

    /// <summary>Gets the saved jobs and their latest notifications.</summary>
    public ObservableCollection<LocalAutomation> Automations { get; } = new();

    /// <summary>Gets or sets the new schedule name.</summary>
    public string AutomationName { get => _automationName; set => this.RaiseAndSetIfChanged(ref _automationName, value); }

    /// <summary>Gets or sets the new schedule prompt.</summary>
    public string AutomationPrompt { get => _automationPrompt; set => this.RaiseAndSetIfChanged(ref _automationPrompt, value); }

    /// <summary>Gets or sets the recurrence in minutes.</summary>
    public int AutomationMinutes { get => _automationMinutes; set => this.RaiseAndSetIfChanged(ref _automationMinutes, value); }

    /// <summary>Gets or sets the automation status without replacing active chat status.</summary>
    public string AutomationStatus { get => _automationStatus; set => this.RaiseAndSetIfChanged(ref _automationStatus, value); }

    /// <summary>Gets the save command.</summary>
    public ReactiveCommand<Unit, Unit> AddAutomationCommand { get; private set; } = null!;

    /// <summary>Gets the enable or pause command.</summary>
    public ReactiveCommand<LocalAutomation, Unit> ToggleAutomationCommand { get; private set; } = null!;

    /// <summary>Gets the delete command.</summary>
    public ReactiveCommand<LocalAutomation, Unit> DeleteAutomationCommand { get; private set; } = null!;

    /// <summary>Gets the acknowledge command.</summary>
    public ReactiveCommand<LocalAutomation, Unit> ReadAutomationCommand { get; private set; } = null!;

    /// <summary>Initializes the local scheduler without creating any jobs.</summary>
    private void InitializeAutomationCommands()
    {
        AddAutomationCommand = ReactiveCommand.CreateFromTask(AddAutomationAsync, outputScheduler: _uiScheduler);
        ToggleAutomationCommand = ReactiveCommand.CreateFromTask<LocalAutomation>(ToggleAutomationAsync, outputScheduler: _uiScheduler);
        DeleteAutomationCommand = ReactiveCommand.CreateFromTask<LocalAutomation>(DeleteAutomationAsync, outputScheduler: _uiScheduler);
        ReadAutomationCommand = ReactiveCommand.CreateFromTask<LocalAutomation>(ReadAutomationAsync, outputScheduler: _uiScheduler);
        _automationRunner = new(() => _timeProvider.GetUtcNow(), ExecuteAutomationAsync, _automationCoordinator);
        _automationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(AutomationPollSeconds) };
        _automationTimer.Tick += OnAutomationTick;
        _automationTimer.Start();
        TaskObserver.FireAndForget(CheckAutomationsAsync());
    }

    /// <summary>Reports mutation failures and preserves unreadable state files for recovery.</summary>
    /// <param name="mutation">The requested mutation against freshly loaded rows.</param>
    /// <returns>The mutation and UI refresh completion.</returns>
    private async Task RunAutomationMutationAsync(Action<List<LocalAutomation>> mutation)
    {
        try
        {
            List<LocalAutomation>? latest = await Task.Run(() => _automationCoordinator.TryUpdate(mutation));
            if (latest is null)
            {
                AutomationStatus = "Another Visual Studio instance is updating automations. Retry this action; schedules refresh on the next poll.";
                return;
            }

            Replace(Automations, latest);
            AutomationStatus = "Automation state saved. New schedules are paused until enabled.";
        }
        catch (Exception exception)
        {
            AutomationStatus = $"Automation could not be saved: {exception.Message}";
        }
    }

    /// <summary>Creates a paused schedule using the current workspace.</summary>
    /// <returns>The durable save completion.</returns>
    private Task AddAutomationAsync()
    {
        if (string.IsNullOrWhiteSpace(AutomationName) || string.IsNullOrWhiteSpace(AutomationPrompt) || AutomationMinutes <= 0 || !Directory.Exists(_workspace.CurrentWorkspaceRoot))
        {
            AutomationStatus = "Enter a name, prompt, positive interval and open an existing workspace.";
            return Task.CompletedTask;
        }

        var job = new LocalAutomation
        {
            Name = AutomationName.Trim(),
            Prompt = AutomationPrompt,
            IntervalMinutes = AutomationMinutes,
            Workspace = _workspace.CurrentWorkspaceRoot,
            NextDue = _timeProvider.GetUtcNow().AddMinutes(AutomationMinutes),
        };
        return RunAutomationMutationAsync(jobs => jobs.Add(job));
    }

    /// <summary>Toggles explicit execution opt-in.</summary>
    /// <param name="job">The selected job.</param>
    /// <returns>The mutation completion.</returns>
    private Task ToggleAutomationAsync(LocalAutomation job)
    {
        bool enabled = !job.Enabled;
        return RunAutomationMutationAsync(jobs =>
        {
            LocalAutomation? latest = jobs.FirstOrDefault(item => item.Id == job.Id);
            if (latest is null)
            {
                return;
            }

            latest.Enabled = enabled;
        });
    }

    /// <summary>Deletes a schedule and its notification.</summary>
    /// <param name="job">The selected job.</param>
    /// <returns>The mutation completion.</returns>
    private Task DeleteAutomationAsync(LocalAutomation job) => RunAutomationMutationAsync(jobs => _ = jobs.RemoveAll(item => item.Id == job.Id));

    /// <summary>Acknowledges the latest saved result.</summary>
    /// <param name="job">The selected job.</param>
    /// <returns>The mutation completion.</returns>
    private Task ReadAutomationAsync(LocalAutomation job)
    {
        return RunAutomationMutationAsync(jobs =>
        {
            LocalAutomation? latest = jobs.FirstOrDefault(item => item.Id == job.Id && item.LastCompleted == job.LastCompleted);
            if (latest is null)
            {
                return;
            }

            latest.Unread = false;
        });
    }

    /// <summary>Checks due jobs and reports persistence failures.</summary>
    /// <param name="sender">The polling timer.</param>
    /// <param name="args">The timer event.</param>
    private void OnAutomationTick(object? sender, EventArgs args) => TaskObserver.FireAndForget(CheckAutomationsAsync());

    /// <summary>Executes a scheduler pass and observes every failure.</summary>
    /// <returns>The scheduler pass completion.</returns>
    private async Task CheckAutomationsAsync()
    {
        CancellationToken lifetime = _lifetime.Token;
        try
        {
            if (_automationRunner is not null)
            {
                List<LocalAutomation>? jobs = await Task.Run(() => _automationCoordinator.TryUpdate());
                if (jobs is not null)
                {
                    Replace(Automations, jobs);
                    await Task.Run(() => _automationRunner.TickAsync(jobs, lifetime));
                    List<LocalAutomation>? latest = await Task.Run(() => _automationCoordinator.TryUpdate());
                    if (latest is not null)
                    {
                        Replace(Automations, latest);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            AutomationStatus = $"Automation scheduler failed: {exception.Message}";
        }
    }

    /// <summary>Runs one isolated, read-only CLI process without publishing into the current chat.</summary>
    /// <param name="job">The saved prompt and workspace.</param>
    /// <param name="cancellationToken">The timeout or host cancellation.</param>
    /// <returns>The final response.</returns>
    private async Task<string> ExecuteAutomationAsync(LocalAutomation job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CodexCliClient(_settingsStore);
        Task<CodexRunResult> execution = client.RunAsync(new CodexRunRequest
        {
            Prompt = job.Prompt,
            WorkspaceRoot = job.Workspace,
            Options = new CodexRunOptions { Model = _settingsStore.Current.DefaultModel, SandboxMode = SandboxMode.ReadOnly, ApprovalPolicy = ApprovalPolicy.Never },
        });
        using var registration = cancellationToken.Register(client.CancelActiveRun);
        CodexRunResult result = await execution;
        cancellationToken.ThrowIfCancellationRequested();
        return result.FinalResponse;
    }
}
