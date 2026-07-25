// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the i Session Store contract.</summary>
public interface ISessionStore
{
    /// <summary>Creates the operation.</summary>
    /// <returns>The create result.</returns>
    CodexSessionDocument Create();

    /// <summary>Loads the operation.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The load result.</returns>
    CodexSessionDocument? Load(string id);

    /// <summary>Saves the operation.</summary>
    /// <param name="session">The session.</param>
    void Save(CodexSessionDocument session);

    /// <summary>Deletes the operation.</summary>
    /// <param name="id">The id.</param>
    void Delete(string id);

    /// <summary>Loads recent.</summary>
    /// <param name="limit">The limit.</param>
    /// <returns>The load Recent result.</returns>
    IEnumerable<CodexSessionDocument> LoadRecent(int limit);
}
