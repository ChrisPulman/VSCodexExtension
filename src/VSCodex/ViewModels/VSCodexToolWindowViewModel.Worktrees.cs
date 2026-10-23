// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace VSCodex.ViewModels;

/// <summary>Composes owned local Git worktree operations with the tool window.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Stores checkout ownership outside repository contents.</summary>
    private readonly ManagedWorktreeService _worktrees = new(Path.Combine(LocalPaths.AppRoot, "managed-worktrees"));

    /// <summary>Stores the new branch name.</summary>
    private string _newWorktreeBranch = "CP_";

    /// <summary>Gets the visible repository checkouts.</summary>
    public ObservableCollection<ManagedWorktree> Worktrees { get; } = new();

    /// <summary>Gets or sets the branch to create.</summary>
    public string NewWorktreeBranch
    {
        get => _newWorktreeBranch;
        set => this.RaiseAndSetIfChanged(ref _newWorktreeBranch, value);
    }

    /// <summary>Gets the checkout refresh command.</summary>
    public ReactiveCommand<Unit, Unit> RefreshWorktreesCommand { get; private set; } = null!;

    /// <summary>Gets the checkout creation command.</summary>
    public ReactiveCommand<Unit, Unit> CreateWorktreeCommand { get; private set; } = null!;

    /// <summary>Gets the checkout archive/restore command.</summary>
    public ReactiveCommand<ManagedWorktree, Unit> ArchiveWorktreeCommand { get; private set; } = null!;

    /// <summary>Gets the checkout pin command.</summary>
    public ReactiveCommand<ManagedWorktree, Unit> PinWorktreeCommand { get; private set; } = null!;

    /// <summary>Gets the command to open a checkout in the file browser.</summary>
    public ReactiveCommand<ManagedWorktree, Unit> OpenWorktreeCommand { get; private set; } = null!;

    /// <summary>Initializes checkout commands on the UI scheduler.</summary>
    private void InitializeWorktreeCommands()
    {
        RefreshWorktreesCommand = ReactiveCommand.CreateFromTask(() => RunWorktreeOperationAsync(null), outputScheduler: _uiScheduler);
        CreateWorktreeCommand = ReactiveCommand.CreateFromTask(CreateWorktreeAsync, outputScheduler: _uiScheduler);
        ArchiveWorktreeCommand = ReactiveCommand.CreateFromTask<ManagedWorktree>(item => RunWorktreeOperationAsync(root => _worktrees.ToggleArchive(root, item.Id)), outputScheduler: _uiScheduler);
        PinWorktreeCommand = ReactiveCommand.CreateFromTask<ManagedWorktree>(item => RunWorktreeOperationAsync(root => _worktrees.TogglePin(root, item.Id)), outputScheduler: _uiScheduler);
        OpenWorktreeCommand = ReactiveCommand.Create<ManagedWorktree>(OpenWorktree, outputScheduler: _uiScheduler);
    }

    /// <summary>Runs an operation against a captured workspace and reports failures explicitly.</summary>
    /// <param name="operation">The mutation, or null to refresh only.</param>
    /// <returns>A task that completes after the UI is refreshed.</returns>
    private async Task RunWorktreeOperationAsync(Action<string>? operation)
    {
        if (!CanEditSettings)
        {
            Status = "Wait for the active task to finish before managing worktrees.";
            return;
        }

        string workspace = _workspace.CurrentWorkspaceRoot;
        if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace))
        {
            Status = "Open a repository folder or solution before managing worktrees.";
            return;
        }

        try
        {
            Status = "Updating local Git worktrees...";
            IReadOnlyList<ManagedWorktree> items = await Task.Run(() =>
            {
                operation?.Invoke(workspace);
                return _worktrees.List(workspace);
            });
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            if (!string.Equals(workspace, _workspace.CurrentWorkspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                Status = "Workspace changed; refresh worktrees for the new workspace.";
                return;
            }

            Replace(Worktrees, items);
            Status = "Local worktrees refreshed. Archives retain a full recovery snapshot and checkout.";
        }
        catch (Exception exception)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(default(CancellationToken));
            Status = $"Worktree operation failed: {exception.Message}";
        }
    }

    /// <summary>Captures the user-entered branch before background work begins.</summary>
    /// <returns>The creation operation.</returns>
    private Task CreateWorktreeAsync()
    {
        string branch = NewWorktreeBranch;
        return RunWorktreeOperationAsync(root => _worktrees.Create(root, branch));
    }

    /// <summary>Opens a checkout directory and reports shell failures in the status bar.</summary>
    /// <param name="item">The checkout.</param>
    private void OpenWorktree(ManagedWorktree item)
    {
        try
        {
            OpenPath(item.Path);
        }
        catch (Exception exception)
        {
            Status = $"Cannot open worktree folder: {exception.Message}";
        }
    }
}
