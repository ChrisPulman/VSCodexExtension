// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace VSCodex.Models;

/// <summary>Provides the codex Run Request implementation.</summary>
public sealed class CodexRunRequest
{
    /// <summary>Gets or sets the operation Id.</summary>
    public string OperationId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the prompt.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Gets or sets the thread Id.</summary>
    public string? ThreadId { get; set; }

    /// <summary>Gets or sets the workspace Root.</summary>
    public string WorkspaceRoot { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Name.</summary>
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Solution Path.</summary>
    public string WorkspaceSolutionPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Memory Root.</summary>
    public string WorkspaceMemoryRoot { get; set; } = string.Empty;

    /// <summary>Gets or sets the reactive Memory Context.</summary>
    public string ReactiveMemoryContext { get; set; } = string.Empty;

    /// <summary>Gets or sets the workspace Identity.</summary>
    public WorkspaceIdentity WorkspaceIdentity { get; set; } = new();

    /// <summary>Gets or sets the options.</summary>
    public CodexRunOptions Options { get; set; } = new();

    /// <summary>Gets or sets the attachments.</summary>
    public IReadOnlyList<CodexAttachment> Attachments { get; set; } = Array.Empty<CodexAttachment>();

    /// <summary>Gets or sets the skills.</summary>
    public IReadOnlyList<SkillDefinition> Skills { get; set; } = Array.Empty<SkillDefinition>();

    /// <summary>Gets or sets the memories.</summary>
    public IReadOnlyList<MemoryEntry> Memories { get; set; } = Array.Empty<MemoryEntry>();

    /// <summary>Gets or sets the mcp Servers.</summary>
    public IReadOnlyList<McpServerDefinition> McpServers { get; set; } = Array.Empty<McpServerDefinition>();

    /// <summary>Gets or sets the workspace Files.</summary>
    public IReadOnlyList<WorkspaceFileReference> WorkspaceFiles { get; set; } = Array.Empty<WorkspaceFileReference>();

    /// <summary>Gets or sets the agent Roles.</summary>
    public IReadOnlyList<AgentRoleDefinition> AgentRoles { get; set; } = Array.Empty<AgentRoleDefinition>();
}
