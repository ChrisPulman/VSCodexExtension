using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VSCodexExtension.Infrastructure;
using VSCodexExtension.Models;
namespace VSCodexExtension.Services
{
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
        private readonly JsonFileStore _store = new JsonFileStore();
        private readonly BehaviorSubject<IReadOnlyList<MemoryEntry>> _memories;
        private string? _workspaceFile;
        public MemoryStore() => _memories = new BehaviorSubject<IReadOnlyList<MemoryEntry>>(_store.ReadOrCreate<List<MemoryEntry>>(LocalPaths.MemoryFile));
        public IObservable<IReadOnlyList<MemoryEntry>> Memories => _memories.AsObservable();
        public IReadOnlyList<MemoryEntry> Snapshot => _memories.Value;
        public void LoadWorkspace(string workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot)) return;
            _workspaceFile = Path.Combine(workspaceRoot, ".codex", "memory.json");
            _memories.OnNext(_store.ReadOrCreate<List<MemoryEntry>>(LocalPaths.MemoryFile).Concat(_store.ReadOrCreate<List<MemoryEntry>>(_workspaceFile)).ToList());
        }
        public void Add(string text, string scope)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var entry = new MemoryEntry { Text = text.Trim(), Scope = scope };
            var file = scope.Equals("workspace", StringComparison.OrdinalIgnoreCase) && _workspaceFile != null ? _workspaceFile : LocalPaths.MemoryFile;
            var list = _store.ReadOrCreate<List<MemoryEntry>>(file); list.Add(entry); _store.Write(file, list);
            _memories.OnNext(Snapshot.Concat(new[] { entry }).ToList());
        }
        public void Remove(string id)
        {
            foreach (var file in new[] { LocalPaths.MemoryFile, _workspaceFile }.Where(f => !string.IsNullOrWhiteSpace(f)))
            { var list = _store.ReadOrCreate<List<MemoryEntry>>(file!); if (list.RemoveAll(x => x.Id == id) > 0) _store.Write(file!, list); }
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
}
