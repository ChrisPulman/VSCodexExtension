// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Newtonsoft.Json.Linq;

namespace VSCodex.Models;

/// <summary>Provides the reactive Memory Pause Checkpoint Result implementation.</summary>
public sealed class ReactiveMemoryPauseCheckpointResult
{
    /// <summary>Gets or sets the success.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the is Cancelled.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>Gets or sets the message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the error Code.</summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the checkpoint.</summary>
    public ReactiveMemoryPauseCheckpoint? Checkpoint { get; set; }

    /// <summary>Gets the raw result.</summary>
    public JObject? RawResult { get; private set; }

    /// <summary>Sets the raw result.</summary>
    /// <param name="rawResult">The raw result.</param>
    public void SetRawResult(JObject? rawResult) => RawResult = rawResult;
}
