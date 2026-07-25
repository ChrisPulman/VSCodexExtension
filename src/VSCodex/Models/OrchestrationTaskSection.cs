// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the orchestration Task Section implementation.</summary>
public sealed class OrchestrationTaskSection : ReactiveObject
{
    /// <summary>Stores the status.</summary>
    private OrchestrationSectionStatus _status = OrchestrationSectionStatus.Pending;

    /// <summary>Stores the result.</summary>
    private string _result = string.Empty;

    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the index.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the assigned Agent.</summary>
    public string AssignedAgent { get; set; } = string.Empty;

    /// <summary>Gets or sets the depends On Section Id.</summary>
    public string DependsOnSectionId { get; set; } = string.Empty;

    /// <summary>Gets or sets the status.</summary>
    public OrchestrationSectionStatus Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value); }

    /// <summary>Gets or sets the result.</summary>
    public string Result { get => _result; set => this.RaiseAndSetIfChanged(ref _result, value); }
}
