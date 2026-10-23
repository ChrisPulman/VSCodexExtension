// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VSCodex.Services;

/// <summary>Public app-server thread management, independent of turn transports.</summary>
public interface IThreadCatalogClient
{
    /// <summary>Lists all pages of active or archived threads.</summary>
    /// <param name="archived">Whether to list archived threads.</param>
    /// <returns>The catalog.</returns>
    Task<JObject> ListThreadsAsync(bool archived);

    /// <summary>Reads persisted conversation turns.</summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <returns>The thread and turns.</returns>
    Task<JObject> ReadThreadAsync(string threadId);

    /// <summary>Forks a persisted conversation into a new thread.</summary>
    /// <param name="threadId">The source thread identifier.</param>
    /// <returns>The forked thread.</returns>
    Task<JObject> ForkThreadAsync(string threadId);

    /// <summary>Archives or restores a persisted conversation.</summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="archived">Whether to archive.</param>
    /// <returns>The server response.</returns>
    Task<JObject> SetThreadArchivedAsync(string threadId, bool archived);
}
