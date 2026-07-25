// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.ObjectModel;
using System.IO;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the run Activity Node implementation.</summary>
public sealed class RunActivityNode : ReactiveObject
{
    /// <summary>Stores the title.</summary>
    private string _title = string.Empty;

    /// <summary>Stores the elapsed text.</summary>
    private string _elapsedText = string.Empty;

    /// <summary>Stores the detail.</summary>
    private string _detail = string.Empty;

    /// <summary>Stores the is Expanded.</summary>
    private bool _isExpanded = true;

    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the kind.</summary>
    public RunActivityKind Kind { get; set; } = RunActivityKind.Agent;

    /// <summary>Gets the children.</summary>
    public ObservableCollection<RunActivityNode> Children { get; } = new();

    /// <summary>Gets or sets the started At.</summary>
    public DateTimeOffset StartedAt { get; set; } = TimeProvider.System.GetLocalNow();

    /// <summary>Gets or sets the completed At.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the file Path.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the is Deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Gets the can Open File.</summary>
    public bool CanOpenFile => !IsDeleted && !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath);

    /// <summary>Gets the has Detail.</summary>
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>Gets the is File Node.</summary>
    public bool IsFileNode => Kind == RunActivityKind.File;

    /// <summary>Gets the timestamp Text.</summary>
    public string TimestampText => StartedAt == default ? string.Empty : StartedAt.LocalDateTime.ToString("HH:mm:ss");

    /// <summary>Gets the header Text.</summary>
    public string HeaderText => string.IsNullOrWhiteSpace(ElapsedText) ? Title : $"{Title}  {ElapsedText}";

    /// <summary>Gets or sets the title.</summary>
    public string Title
    {
        get => _title;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _title, value ?? string.Empty);
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets or sets the detail.</summary>
    public string Detail
    {
        get => _detail;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _detail, value ?? string.Empty);
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets or sets the elapsed Text.</summary>
    public string ElapsedText
    {
        get => _elapsedText;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _elapsedText, value ?? string.Empty);
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets or sets the is Expanded.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }
}
