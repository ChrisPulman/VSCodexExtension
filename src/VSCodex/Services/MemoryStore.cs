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
    private readonly BehaviorSubject<IReadOnlyList<MemoryEntry>> _memories;
    private readonly Dictionary<string, List<MemoryEntry>> _workspaceMemories = new Dictionary<string, List<MemoryEntry>>(StringComparer.OrdinalIgnoreCase);
    private string _workspaceKey = string.Empty;

    public MemoryStore() => _memories = new BehaviorSubject<IReadOnlyList<MemoryEntry>>(Array.Empty<MemoryEntry>());
    public IObservable<IReadOnlyList<MemoryEntry>> Memories => _memories.AsObservable();
    public IReadOnlyList<MemoryEntry> Snapshot => _memories.Value;
    public void LoadWorkspace(string workspaceRoot)
    {
        _workspaceKey = workspaceRoot ?? string.Empty;
        if (!_workspaceMemories.TryGetValue(_workspaceKey, out var entries))
        {
            entries = new List<MemoryEntry>();
            _workspaceMemories[_workspaceKey] = entries;
        }

        _memories.OnNext(entries.ToList());
    }
    public void Add(string text, string scope)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var entry = new MemoryEntry { Text = text.Trim(), Scope = scope };
        if (!_workspaceMemories.TryGetValue(_workspaceKey, out var list))
        {
            list = new List<MemoryEntry>();
            _workspaceMemories[_workspaceKey] = list;
        }

        list.Add(entry);
        _memories.OnNext(list.ToList());
    }
    public void Remove(string id)
    {
        if (_workspaceMemories.TryGetValue(_workspaceKey, out var list))
        {
            list.RemoveAll(x => x.Id == id);
            _memories.OnNext(list.ToList());
            return;
        }

        _memories.OnNext(Snapshot.Where(x => x.Id != id).ToList());
    }
    public IReadOnlyList<MemoryEntry> Search(string query, int limit)
    {
        var terms = (query ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0) return Snapshot.Take(limit).ToList();
        return Snapshot.Select(m => new { Memory = m, Score = terms.Count(t => m.Text.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) })
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenByDescending(x => x.Memory.Updated).Take(limit).Select(x => x.Memory).ToList();
    }
}
