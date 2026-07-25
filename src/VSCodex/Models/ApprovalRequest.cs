// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the approval Request implementation.</summary>
public sealed class ApprovalRequest : ReactiveObject
{
    /// <summary>Stores whether the request is pending.</summary>
    private bool _isPending = true;

    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the method.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Gets or sets the tool Name.</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Gets or sets the arguments Preview.</summary>
    public string ArgumentsPreview { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the is Pending.</summary>
    public bool IsPending { get => _isPending; set => this.RaiseAndSetIfChanged(ref _isPending, value); }
}
