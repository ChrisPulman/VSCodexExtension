// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the skill Definition implementation.</summary>
public sealed class SkillDefinition : ReactiveObject
{
    /// <summary>Stores whether the skill is enabled.</summary>
    private bool _isEnabled;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the root Path.</summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the markdown Path.</summary>
    public string MarkdownPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the is Enabled.</summary>
    public bool IsEnabled { get => _isEnabled; set => this.RaiseAndSetIfChanged(ref _isEnabled, value); }
}
