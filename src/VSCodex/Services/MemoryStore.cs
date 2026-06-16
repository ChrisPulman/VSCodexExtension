using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VSCodex.Models;

namespace VSCodex.Services;

public interface IMemoryStore
{
    IObservable<IReadOnlyList<MemoryEntry>> Memories { get; }
    IReadOnlyList<MemoryEntry> Snapshot { get; }
    void Add(string text, string scope);
    void Remove(string id);
    IReadOnlyList<MemoryEntry> Search(string query, int limit);
    void LoadWorkspace(string workspaceRoot);
}
public sealed class MemoryStore : IMemoryStore
{
    private readonly object _gate = new object();
    private readonly BehaviorSubject<IReadOnlyList<MemoryEntry>> _memories;
    private readonly Dictionary<string, List<MemoryEntry>> _workspaceMemories = new Dictionary<string, List<MemoryEntry>>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<MemoryEntry> _currentSnapshot = Array.Empty<MemoryEntry>();
    private string _workspaceKey = string.Empty;

    public MemoryStore() => _memories = new BehaviorSubject<IReadOnlyList<MemoryEntry>>(Array.Empty<MemoryEntry>());
    public IObservable<IReadOnlyList<MemoryEntry>> Memories => _memories.AsObservable();
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

    public void LoadWorkspace(string workspaceRoot)
    {
        IReadOnlyList<MemoryEntry> snapshot;
        lock (_gate)
        {
            _workspaceKey = workspaceRoot ?? string.Empty;
            if (!_workspaceMemories.TryGetValue(_workspaceKey, out var entries))
            {
                entries = new List<MemoryEntry>();
                _workspaceMemories[_workspaceKey] = entries;
            }

            snapshot = entries.ToList();
            _currentSnapshot = snapshot;
        }

        _memories.OnNext(snapshot);
    }

    public void Add(string text, string scope)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        IReadOnlyList<MemoryEntry> snapshot;
        lock (_gate)
        {
            var entry = new MemoryEntry { Text = text.Trim(), Scope = scope };
            if (!_workspaceMemories.TryGetValue(_workspaceKey, out var list))
            {
                list = new List<MemoryEntry>();
                _workspaceMemories[_workspaceKey] = list;
            }

            list.Add(entry);
            snapshot = list.ToList();
            _currentSnapshot = snapshot;
        }

        _memories.OnNext(snapshot);
    }

    public void Remove(string id)
    {
        IReadOnlyList<MemoryEntry> snapshot;
        lock (_gate)
        {
            if (_workspaceMemories.TryGetValue(_workspaceKey, out var list))
            {
                list.RemoveAll(x => x.Id == id);
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

    public IReadOnlyList<MemoryEntry> Search(string query, int limit)
    {
        var snapshot = Snapshot;
        var terms = (query ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0) return snapshot.Take(limit).ToList();
        return snapshot.Select(m => new { Memory = m, Score = terms.Count(t => m.Text.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) })
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenByDescending(x => x.Memory.Updated).Take(limit).Select(x => x.Memory).ToList();
    }
}
