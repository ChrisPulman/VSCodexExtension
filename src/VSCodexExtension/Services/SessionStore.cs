using System;
using System.Collections.Generic;
using System.IO;
using VSCodexExtension.Infrastructure;
using VSCodexExtension.Models;
namespace VSCodexExtension.Services
{
    public interface ISessionStore { CodexSessionDocument Create(); void Save(CodexSessionDocument session); IEnumerable<CodexSessionDocument> LoadRecent(int limit); }
    public sealed class SessionStore : ISessionStore
    {
        private readonly JsonFileStore _store = new JsonFileStore();
        public CodexSessionDocument Create() => new CodexSessionDocument();
        public void Save(CodexSessionDocument session) { session.Updated = DateTimeOffset.Now; _store.Write(Path.Combine(LocalPaths.SessionsRoot, session.Id + ".json"), session); }
        public IEnumerable<CodexSessionDocument> LoadRecent(int limit)
        { foreach (var file in Directory.EnumerateFiles(LocalPaths.SessionsRoot, "*.json")) { CodexSessionDocument? s = null; try { s = _store.ReadOrCreate<CodexSessionDocument>(file); } catch { } if (s != null) yield return s; } }
    }
}
