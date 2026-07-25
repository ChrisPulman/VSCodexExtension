// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the i Mcp Tool Catalog Service contract.</summary>
public interface IMcpToolCatalogService
{
    /// <summary>Performs the discover Tools operation.</summary>
    /// <param name="server">The server.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    Task<IReadOnlyList<McpToolDefinition>> DiscoverToolsAsync(McpServerDefinition server);

    /// <summary>Performs the invoke Tool operation.</summary>
    /// <param name="server">The server.</param>
    /// <param name="toolName">The tool Name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    Task<JObject?> InvokeToolAsync(McpServerDefinition server, string toolName, JObject arguments);

    /// <summary>Performs the invoke Tools operation.</summary>
    /// <param name="server">The server.</param>
    /// <param name="invocations">The invocations.</param>
    /// <param name="timeout">The timeout.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    Task<IReadOnlyList<JObject?>> InvokeToolsAsync(McpServerDefinition server, IReadOnlyList<McpToolInvocation> invocations, TimeSpan timeout);

    /// <summary>Builds invocation Prompt.</summary>
    /// <param name="server">The server.</param>
    /// <param name="tool">The tool.</param>
    /// <returns>The build Invocation Prompt result.</returns>
    string BuildInvocationPrompt(McpServerDefinition server, McpToolDefinition tool);
}
