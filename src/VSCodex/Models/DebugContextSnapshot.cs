// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Provides the debug Context Snapshot implementation.</summary>
public sealed class DebugContextSnapshot
{
    /// <summary>Gets or sets the break Reason.</summary>
    public string BreakReason { get; set; } = string.Empty;

    /// <summary>Gets or sets the exception Description.</summary>
    public string ExceptionDescription { get; set; } = string.Empty;

    /// <summary>Gets or sets the stack Summary.</summary>
    public string StackSummary { get; set; } = string.Empty;

    /// <summary>Gets or sets the selection.</summary>
    public WorkspaceFileReference? Selection { get; set; }
}
