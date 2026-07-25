// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the i Memory Store contract.</summary>
public interface IMemoryStore
{
    /// <summary>Gets the memories.</summary>
    IObservable<IReadOnlyList<MemoryEntry>> Memories { get; }

    /// <summary>Gets the snapshot.</summary>
    IReadOnlyList<MemoryEntry> Snapshot { get; }

    /// <summary>Adds the operation.</summary>
    /// <param name="text">The text.</param>
    /// <param name="scope">The scope.</param>
    void Add(string text, string scope);

    /// <summary>Removes the operation.</summary>
    /// <param name="id">The id.</param>
    void Remove(string id);

    /// <summary>Performs the search operation.</summary>
    /// <param name="query">The query.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The search result.</returns>
    IReadOnlyList<MemoryEntry> Search(string query, int limit);

    /// <summary>Loads workspace.</summary>
    /// <param name="workspaceRoot">The workspace Root.</param>
    void LoadWorkspace(string workspaceRoot);
}
