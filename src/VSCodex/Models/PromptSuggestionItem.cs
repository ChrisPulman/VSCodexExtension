// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the prompt Suggestion Item implementation.</summary>
public sealed class PromptSuggestionItem : ReactiveObject
{
    /// <summary>Gets or sets the kind.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the display Text.</summary>
    public string DisplayText { get; set; } = string.Empty;

    /// <summary>Gets or sets the detail.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets the insert Text.</summary>
    public string InsertText { get; set; } = string.Empty;

    /// <summary>Gets or sets the target Tab.</summary>
    public string TargetTab { get; set; } = string.Empty;
}
