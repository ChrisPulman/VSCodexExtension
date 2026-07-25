// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the session Store implementation.</summary>
/// <param name="timeProvider">The clock used for session timestamps.</param>
public sealed class SessionStore(TimeProvider timeProvider) : ISessionStore
{
    /// <summary>Stores the store.</summary>
    private readonly JsonFileStore _store = new();

    /// <summary>Provides testable access to the system clock.</summary>
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>Creates the operation.</summary>
    /// <returns>The create result.</returns>
    public CodexSessionDocument Create() => new();

    /// <summary>Loads the operation.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The load result.</returns>
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

    /// <summary>Saves the operation.</summary>
    /// <param name="session">The session.</param>
    public void Save(CodexSessionDocument session)
    {
        if (session is null)
        {
            return;
        }

        _ = Directory.CreateDirectory(LocalPaths.SessionsRoot);
        session.Updated = _timeProvider.GetLocalNow();
        _store.Write(SessionPath(session.Id), session);
    }

    /// <summary>Deletes the operation.</summary>
    /// <param name="id">The id.</param>
    public void Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var path = SessionPath(id);
        if (!File.Exists(path))
        {
            return;
        }

        File.Delete(path);
    }

    /// <summary>Loads recent.</summary>
    /// <param name="limit">The limit.</param>
    /// <returns>The load Recent result.</returns>
    public IEnumerable<CodexSessionDocument> LoadRecent(int limit)
    {
        _ = Directory.CreateDirectory(LocalPaths.SessionsRoot);
        return Directory.EnumerateFiles(LocalPaths.SessionsRoot, "*.json")
            .Select(ReadSession)
            .Where(session => session is not null)
            .OrderByDescending(session => session!.Updated)
            .Take(Math.Max(1, limit))
            .Cast<CodexSessionDocument>()
            .ToList();
    }

    /// <summary>Performs the session Path operation.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The session Path result.</returns>
    private static string SessionPath(string id) => Path.Combine(LocalPaths.SessionsRoot, $"{id}.json");

    /// <summary>Reads session.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The read Session result.</returns>
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
}
