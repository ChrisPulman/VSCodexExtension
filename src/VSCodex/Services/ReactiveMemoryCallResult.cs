// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Newtonsoft.Json.Linq;

namespace VSCodex.Services;

/// <summary>Provides the reactive Memory Call Result implementation.</summary>
public sealed class ReactiveMemoryCallResult
{
    /// <summary>Gets or sets the success.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the context Text.</summary>
    public string ContextText { get; set; } = string.Empty;

    /// <summary>Gets or sets the raw Result.</summary>
    public JToken? RawResult { get; set; }
}
