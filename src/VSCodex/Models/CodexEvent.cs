// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Provides the codex Event implementation.</summary>
public sealed class CodexEvent
{
    /// <summary>Gets or sets the type.</summary>
    public string Type { get; set; } = "message";

    /// <summary>Gets or sets the message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the thread Id.</summary>
    public string? ThreadId { get; set; }

    /// <summary>Gets or sets the turn Id.</summary>
    public string? TurnId { get; set; }

    /// <summary>Gets or sets the operation Id.</summary>
    public string? OperationId { get; set; }

    /// <summary>Gets or sets the raw Json.</summary>
    public string RawJson { get; set; } = string.Empty;
}
