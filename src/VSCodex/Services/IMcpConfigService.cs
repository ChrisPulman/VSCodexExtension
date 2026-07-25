// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the i Mcp Config Service contract.</summary>
public interface IMcpConfigService
{
    /// <summary>Gets the servers.</summary>
    IObservable<IReadOnlyList<McpServerDefinition>> Servers { get; }

    /// <summary>Gets the snapshot.</summary>
    IReadOnlyList<McpServerDefinition> Snapshot { get; }

    /// <summary>Refreshes the operation.</summary>
    void Refresh();

    /// <summary>Saves the operation.</summary>
    /// <param name="servers">The servers.</param>
    void Save(IEnumerable<McpServerDefinition> servers);

    /// <summary>Creates template.</summary>
    /// <param name="transportType">The transport Type.</param>
    /// <returns>The create Template result.</returns>
    McpServerDefinition CreateTemplate(string transportType);
}
