// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Newtonsoft.Json.Linq;

namespace VSCodex.Models;

/// <summary>Provides the mcp Tool Invocation implementation.</summary>
public sealed class McpToolInvocation
{
    /// <summary>Gets or sets the tool Name.</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Gets or sets the arguments.</summary>
    public JObject Arguments { get; } = new();
}
