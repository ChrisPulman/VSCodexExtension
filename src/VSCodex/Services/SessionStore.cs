using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

public interface ISessionStore
{
    CodexSessionDocument Create();
    CodexSessionDocument? Load(string id);
    void Save(CodexSessionDocument session);
    void Delete(string id);
    IEnumerable<CodexSessionDocument> LoadRecent(int limit);
}

public sealed class SessionStore : ISessionStore
{
    private readonly JsonFileStore _store = new JsonFileStore();

    public CodexSessionDocument Create() => new CodexSessionDocument();

    public CodexSessionDocument? Load(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var path = SessionPath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return _store.ReadOrCreate<CodexSessionDocument>(path);
        }
        catch
        {
            return null;
        }
    }

    public void Save(CodexSessionDocument session)
    {
        if (session == null)
        {
            return;
        }

        Directory.CreateDirectory(LocalPaths.SessionsRoot);
        session.Updated = DateTimeOffset.Now;
        _store.Write(SessionPath(session.Id), session);
    }

    public void Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var path = SessionPath(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public IEnumerable<CodexSessionDocument> LoadRecent(int limit)
    {
        Directory.CreateDirectory(LocalPaths.SessionsRoot);
        return Directory.EnumerateFiles(LocalPaths.SessionsRoot, "*.json")
            .Select(ReadSession)
            .Where(session => session != null)
            .OrderByDescending(session => session!.Updated)
            .Take(Math.Max(1, limit))
            .Cast<CodexSessionDocument>()
            .ToList();
    }

    private CodexSessionDocument? ReadSession(string path)
    {
        try
        {
            return _store.ReadOrCreate<CodexSessionDocument>(path);
        }
        catch
        {
            return null;
        }
    }

    private static string SessionPath(string id) => Path.Combine(LocalPaths.SessionsRoot, id + ".json");
}
