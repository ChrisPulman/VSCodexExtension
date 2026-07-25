// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace VSCodex.Models;

/// <summary>Provides the workspace File Reference implementation.</summary>
public sealed class WorkspaceFileReference
{
    /// <summary>Gets or sets the path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the relative Path.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the preview.</summary>
    public string Preview { get; set; } = string.Empty;

    /// <summary>Gets or sets the reference Kind.</summary>
    public string ReferenceKind { get; set; } = "file";

    /// <summary>Gets or sets the reference Key.</summary>
    public string ReferenceKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the start Line.</summary>
    public int StartLine { get; set; }

    /// <summary>Gets or sets the end Line.</summary>
    public int EndLine { get; set; }

    /// <summary>Gets the display Name.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(ReferenceKey) ? RelativePath : ReferenceKey;
}
