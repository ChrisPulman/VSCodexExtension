// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Provides the codex Run Result implementation.</summary>
public sealed class CodexRunResult
{
    /// <summary>Gets or sets the thread Id.</summary>
    public string? ThreadId { get; set; }

    /// <summary>Gets or sets the final Response.</summary>
    public string FinalResponse { get; set; } = string.Empty;

    /// <summary>Gets or sets the raw Json.</summary>
    public string RawJson { get; set; } = string.Empty;

    /// <summary>Gets or sets the used Fallback.</summary>
    public bool UsedFallback { get; set; }
}
