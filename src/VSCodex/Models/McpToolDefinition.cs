// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the mcp Tool Definition implementation.</summary>
public sealed class McpToolDefinition : ReactiveObject
{
    /// <summary>Gets or sets the server Name.</summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the input Fields.</summary>
    public List<McpToolInputField> InputFields { get; } = [];

    /// <summary>Gets the display Name.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(ServerName) ? Name : $"{ServerName}/{Name}";
}
