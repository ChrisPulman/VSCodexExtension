// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the session History Item implementation.</summary>
public sealed class SessionHistoryItem : ReactiveObject
{
    /// <summary>Stores whether the item is being renamed.</summary>
    private bool _isRenaming;

    /// <summary>Stores the draft title.</summary>
    private string _draftTitle = string.Empty;

    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the thread Id.</summary>
    public string? ThreadId { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the preview.</summary>
    public string Preview { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Identity Id.</summary>
    public string WorkspaceIdentityId { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Name.</summary>
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Root.</summary>
    public string WorkspaceRoot { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Solution Path.</summary>
    public string WorkspaceSolutionPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the updated.</summary>
    public DateTimeOffset Updated { get; set; }

    /// <summary>Gets or sets the message Count.</summary>
    public int MessageCount { get; set; }

    /// <summary>Gets the updated Display.</summary>
    public string UpdatedDisplay => Updated.ToLocalTime().ToString("g");

    /// <summary>Gets the message Count Display.</summary>
    public string MessageCountDisplay => MessageCount == 1 ? "1 message" : $"{MessageCount} messages";

    /// <summary>Gets the workspace Display.</summary>
    public string WorkspaceDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(WorkspaceName))
            {
                return WorkspaceName;
            }

            return string.IsNullOrWhiteSpace(WorkspaceRoot) ? "Current workspace" : WorkspaceRoot;
        }
    }

    /// <summary>Gets or sets the is Renaming.</summary>
    public bool IsRenaming { get => _isRenaming; set => this.RaiseAndSetIfChanged(ref _isRenaming, value); }

    /// <summary>Gets or sets the draft Title.</summary>
    public string DraftTitle { get => _draftTitle; set => this.RaiseAndSetIfChanged(ref _draftTitle, value ?? string.Empty); }
}
