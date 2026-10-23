// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;

namespace VSCodex.Models;

/// <summary>Provides the prerequisite Status implementation.</summary>
public sealed class PrerequisiteStatus : ReactiveObject
{
    /// <summary>Stores the state.</summary>
    private PrerequisiteState _state = PrerequisiteState.Missing;

    /// <summary>Stores the details.</summary>
    private string _details = string.Empty;

    /// <summary>Stores the status.</summary>
    private string _status = string.Empty;

    /// <summary>Stores the installation command.</summary>
    private string _installCommand = string.Empty;

    /// <summary>Stores the update command.</summary>
    private string _updateCommand = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the state.</summary>
    public PrerequisiteState State
    {
        get => _state;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _state, value);
            this.RaisePropertyChanged(nameof(CanCopyCommand));
            this.RaisePropertyChanged(nameof(CanUpdate));
            this.RaisePropertyChanged(nameof(ActionButtonText));
        }
    }

    /// <summary>Gets or sets the status.</summary>
    public string Status
    {
        get => _status;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _status, value ?? string.Empty);
            this.RaisePropertyChanged(nameof(ActionButtonText));
        }
    }

    /// <summary>Gets or sets the details.</summary>
    public string Details { get => _details; set => this.RaiseAndSetIfChanged(ref _details, value ?? string.Empty); }

    /// <summary>Gets or sets the install Command.</summary>
    public string InstallCommand
    {
        get => _installCommand;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _installCommand, value ?? string.Empty);
            this.RaisePropertyChanged(nameof(ActionCommand));
            this.RaisePropertyChanged(nameof(CanCopyCommand));
            this.RaisePropertyChanged(nameof(CanUpdate));
        }
    }

    /// <summary>Gets or sets the update Command.</summary>
    public string UpdateCommand
    {
        get => _updateCommand;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _updateCommand, value ?? string.Empty);
            this.RaisePropertyChanged(nameof(ActionCommand));
            this.RaisePropertyChanged(nameof(CanCopyCommand));
            this.RaisePropertyChanged(nameof(CanUpdate));
        }
    }

    /// <summary>Gets or sets the is Blocking.</summary>
    public bool IsBlocking { get; set; }

    /// <summary>Gets the can Copy Command.</summary>
    public bool CanCopyCommand => State != PrerequisiteState.Ready && !string.IsNullOrWhiteSpace(ActionCommand);

    /// <summary>Gets the can Update.</summary>
    public bool CanUpdate => State != PrerequisiteState.Ready && !string.IsNullOrWhiteSpace(ActionCommand);

    /// <summary>Gets the action Command.</summary>
    public string ActionCommand => string.IsNullOrWhiteSpace(UpdateCommand) ? InstallCommand : UpdateCommand;

    /// <summary>Gets the action Button Text.</summary>
    public string ActionButtonText => State == PrerequisiteState.Missing || HasMissingStatus(Status) ? "Install" : "Update";

    /// <summary>Checks the setup error marker using the available framework API.</summary>
    /// <param name="status">The current setup message.</param>
    /// <returns>Whether setup reported a missing component.</returns>
    private static bool HasMissingStatus(string status)
    {
#if NETFRAMEWORK
        return status.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0;
#else
        return status.Contains("not found", StringComparison.OrdinalIgnoreCase);
#endif
    }
}
