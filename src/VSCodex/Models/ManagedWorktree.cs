// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace VSCodex.Models;

/// <summary>Describes a Git checkout and its extension ownership.</summary>
public sealed class ManagedWorktree
{
    /// <summary>Gets or sets the durable ownership identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the common Git directory.</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>Gets or sets the checkout directory.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the original checkout directory.</summary>
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the branch.</summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the checkout is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets whether the checkout is pinned.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Gets or sets whether Git protects this checkout.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Gets or sets whether this extension created the checkout.</summary>
    public bool IsManaged { get; set; }

    /// <summary>Gets or sets the latest verified recovery snapshot.</summary>
    public string SnapshotPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the durable destination of an interrupted move.</summary>
    public string PendingPath { get; set; } = string.Empty;

    /// <summary>Gets a concise visible ownership and protection status.</summary>
    public string Description => GetDescription();

    /// <summary>Builds the visible protection status.</summary>
    /// <returns>The status text.</returns>
    private string GetDescription()
    {
        if (IsArchived)
        {
            return "Archived (recoverable checkout)";
        }

        if (IsPinned)
        {
            return "Pinned";
        }

        if (IsLocked)
        {
            return "Protected by Git lock";
        }

        return IsManaged ? "Managed checkout" : "External or primary checkout (protected)";
    }
}
