// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace VSCodex.Models;

/// <summary>Provides the codex Session Document implementation.</summary>
public sealed class CodexSessionDocument
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the thread Id.</summary>
    public string? ThreadId { get; set; }

    /// <summary>Gets or sets the workspace Identity Id.</summary>
    public string WorkspaceIdentityId { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Name.</summary>
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Root.</summary>
    public string WorkspaceRoot { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Solution Path.</summary>
    public string WorkspaceSolutionPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the created.</summary>
    public DateTimeOffset Created { get; set; } = TimeProvider.System.GetLocalNow();

    /// <summary>Gets or sets the updated.</summary>
    public DateTimeOffset Updated { get; set; } = TimeProvider.System.GetLocalNow();

    /// <summary>Gets the messages.</summary>
    public List<ChatMessage> Messages { get; } = [];
}
