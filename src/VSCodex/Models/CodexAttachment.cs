// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.IO;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the codex Attachment implementation.</summary>
public sealed class CodexAttachment : ReactiveObject
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind.</summary>
    public string Kind { get; set; } = "file";

    /// <summary>Gets the display Name.</summary>
    public string DisplayName => System.IO.Path.GetFileName(Path);
}
