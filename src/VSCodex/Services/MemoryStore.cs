// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the memory Store implementation.</summary>
public sealed class MemoryStore : IMemoryStore
{
    /// <summary>Stores the gate.</summary>
    private readonly object _gate = new();

    /// <summary>Stores the memories.</summary>
    private readonly BehaviorSubject<IReadOnlyList<MemoryEntry>> _memories;

    /// <summary>Stores the workspace Memories.</summary>
    private readonly Dictionary<string, List<MemoryEntry>> _workspaceMemories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stores the current Snapshot.</summary>
    private IReadOnlyList<MemoryEntry> _currentSnapshot = [];

    /// <summary>Stores the workspace Key.</summary>
    private string _workspaceKey = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="MemoryStore"/> class.</summary>
    public MemoryStore() => _memories = new([]);

    /// <summary>Gets the memories.</summary>
    public IObservable<IReadOnlyList<MemoryEntry>> Memories => _memories.AsObservable();

    /// <summary>Gets the snapshot.</summary>
    public IReadOnlyList<MemoryEntry> Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _currentSnapshot.ToList();
            }
        }
    }

    /// <summary>Loads workspace.</summary>
    /// <param name="workspaceRoot">The workspace Root.</param>
    public void LoadWorkspace(string workspaceRoot)
    {
        IReadOnlyList<MemoryEntry> snapshot;
        lock (_gate)
        {
            _workspaceKey = workspaceRoot ?? string.Empty;
            if (!_workspaceMemories.TryGetValue(_workspaceKey, out var entries))
            {
                entries = new();
                _workspaceMemories[_workspaceKey] = entries;
            }

            snapshot = entries.ToList();
            _currentSnapshot = snapshot;
        }

        _memories.OnNext(snapshot);
    }

    /// <summary>Adds the operation.</summary>
    /// <param name="text">The text.</param>
    /// <param name="scope">The scope.</param>
    public void Add(string text, string scope)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        IReadOnlyList<MemoryEntry> snapshot;
        lock (_gate)
        {
            var entry = new MemoryEntry { Text = text.Trim(), Scope = scope };
            if (!_workspaceMemories.TryGetValue(_workspaceKey, out var list))
            {
                list = new();
                _workspaceMemories[_workspaceKey] = list;
            }

            list.Add(entry);
            snapshot = list.ToList();
            _currentSnapshot = snapshot;
        }

        _memories.OnNext(snapshot);
    }

    /// <summary>Removes the operation.</summary>
    /// <param name="id">The id.</param>
    public void Remove(string id)
    {
        IReadOnlyList<MemoryEntry> snapshot;
        lock (_gate)
        {
            if (_workspaceMemories.TryGetValue(_workspaceKey, out var list))
            {
                _ = list.RemoveAll(x => x.Id == id);
                snapshot = list.ToList();
            }
            else
            {
                snapshot = _currentSnapshot.Where(x => x.Id != id).ToList();
            }

            _currentSnapshot = snapshot;
        }

        _memories.OnNext(snapshot);
    }

    /// <summary>Performs the search operation.</summary>
    /// <param name="query">The query.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The search result.</returns>
    public IReadOnlyList<MemoryEntry> Search(string query, int limit)
    {
        var snapshot = Snapshot;
        var terms = (query ?? string.Empty).Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return terms.Length == 0 ? snapshot.Take(limit).ToList() : snapshot.Select(m => (Memory: m, Score: terms.Count(t => m.Text.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)))
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenByDescending(x => x.Memory.Updated).Take(limit).Select(x => x.Memory).ToList();
    }
}
