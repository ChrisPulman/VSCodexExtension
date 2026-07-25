// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Linq;
using System.Text;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the prompt Builder implementation.</summary>
public sealed class PromptBuilder
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric12 = 12;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric12000 = 12_000;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric4000 = 4000;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric8 = 8;

    /// <summary>Builds the operation.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The build result.</returns>
    public string Build(CodexRunRequest request)
    {
        var sb = new StringBuilder();
        var identity = request.WorkspaceIdentity ?? new WorkspaceIdentity();
        AppendWorkspaceDetails(sb, request, identity);
        AppendReactiveMemoryDetails(sb, request, identity);
        AppendRecoveredMemory(sb, request);
        AppendRelevantMemory(sb, request);
        AppendSkills(sb, request);
        AppendMcpServers(sb, request);
        AppendWorkspaceContext(sb, request);
        AppendAttachments(sb, request);
        AppendModeInstruction(sb, request);
        _ = sb.AppendLine("## User request");
        _ = sb.AppendLine(request.Prompt);
        return sb.ToString();
    }

    /// <summary>Appends workspace details.</summary>
    /// <param name="sb">The builder.</param>
    /// <param name="request">The request.</param>
    /// <param name="identity">The workspace identity.</param>
    private void AppendWorkspaceDetails(StringBuilder sb, CodexRunRequest request, WorkspaceIdentity identity)
    {
        _ = sb.AppendLine("You are Codex running inside Visual Studio. Prefer deterministic, minimal, buildable changes.");
        _ = sb.AppendLine($"Mode: {request.Options.Mode}");
        _ = sb.AppendLine($"Workspace root: {request.WorkspaceRoot}");
        if (!string.IsNullOrWhiteSpace(identity.Id))
        {
            _ = sb.AppendLine($"Workspace identity: {identity.Name} ({identity.Id})");
        }

        if (!string.IsNullOrWhiteSpace(identity.SolutionRelativePath))
        {
            _ = sb.AppendLine($"Solution: {identity.SolutionRelativePath}");
        }

        if (!string.IsNullOrWhiteSpace(request.WorkspaceMemoryRoot))
        {
            _ = sb.AppendLine($"Project memory root: {request.WorkspaceMemoryRoot}");
        }

        _ = sb.AppendLine($"Approval policy: {request.Options.ApprovalPolicy}; Sandbox: {request.Options.SandboxMode}");
        if (!string.IsNullOrWhiteSpace(request.Options.FailoverModel))
        {
            _ = sb.AppendLine($"Model failover: {request.Options.FailoverModel}");
        }

        _ = sb.AppendLine();
    }

    /// <summary>Appends ReactiveMemory details.</summary>
    /// <param name="sb">The builder.</param>
    /// <param name="request">The request.</param>
    /// <param name="identity">The workspace identity.</param>
    private void AppendReactiveMemoryDetails(StringBuilder sb, CodexRunRequest request, WorkspaceIdentity identity)
    {
        var reactiveMemory = request.McpServers.FirstOrDefault(
            server => server.IsEnabled
                && (server.Name.IndexOf("reactivememory", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || server.Command.IndexOf("ReactiveMemory", System.StringComparison.OrdinalIgnoreCase) >= 0));
        if (reactiveMemory is null)
        {
            return;
        }

        _ = sb.AppendLine("## ReactiveMemory MCP hooks");
        _ = sb.AppendLine($"Use MCP server `{reactiveMemory.Name}` as the durable memory system.");
        if (!string.IsNullOrWhiteSpace(identity.Id))
        {
            _ = sb.AppendLine($"Scope all memory operations to workspace identity `{identity.Id}` for `{identity.Name}` at `{identity.RootPath}`.");
        }

        _ = sb.AppendLine("At session start call `reactivememory_status`.");
        _ = sb.AppendLine(
            "For every user prompt, call `reactivememory_react_to_prompt` before answering so related memories, "
            + "entities, duplicates, and checkpoints are handled automatically.");
        _ = sb.AppendLine(
            "Before relying on persisted facts, call `reactivememory_search`, `reactivememory_search_relays`, "
            + "or `reactivememory_facts_query` and prefer retrieved data over assumptions.");
        _ = sb.AppendLine(
            "When durable context, project decisions, code patterns, or changed facts appear, use "
            + "`reactivememory_check_duplicate`, `reactivememory_add_drawer`, `reactivememory_facts_invalidate`, "
            + "and `reactivememory_facts_add` as appropriate.");
        _ = sb.AppendLine("After a meaningful interaction, call `reactivememory_diary_write` to preserve the session summary with minimal user input.");
        _ = sb.AppendLine();
    }

    /// <summary>Appends recovered memory.</summary>
    /// <param name="sb">The builder.</param>
    /// <param name="request">The request.</param>
    private void AppendRecoveredMemory(StringBuilder sb, CodexRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReactiveMemoryContext))
        {
            return;
        }

        _ = sb.AppendLine("## Recovered ReactiveMemory context");
        _ = sb.AppendLine("Use this durable project memory as context for the current turn. If it conflicts with the user's explicit request or current files, prefer the current request and files.");
        _ = sb.AppendLine(request.ReactiveMemoryContext.Length > Numeric12000 ? request.ReactiveMemoryContext.Substring(0, Numeric12000) : request.ReactiveMemoryContext);
        _ = sb.AppendLine();
    }

    /// <summary>Appends relevant memory.</summary>
    /// <param name="sb">The builder.</param>
    /// <param name="request">The request.</param>
    private void AppendRelevantMemory(StringBuilder sb, CodexRunRequest request)
    {
        if (!request.Options.IncludeMemory || !request.Memories.Any())
        {
            return;
        }

        _ = sb.AppendLine("## Relevant memory");
        foreach (var memory in request.Memories.Take(Numeric12))
        {
            _ = sb.AppendLine($"- [{memory.Scope}] {memory.Text}");
        }

        _ = sb.AppendLine();
    }

    /// <summary>Appends skills.</summary>
    /// <param name="sb">The builder.</param>
    /// <param name="request">The request.</param>
    private void AppendSkills(StringBuilder sb, CodexRunRequest request)
    {
        if (!request.Options.IncludeSkills || !request.Skills.Any())
        {
            return;
        }

        _ = sb.AppendLine("## Enabled skills");
        foreach (var skill in request.Skills.Where(x => x.IsEnabled).Take(Numeric8))
        {
            _ = sb.AppendLine($"### {skill.Name}");
            _ = sb.AppendLine(skill.Description);
            _ = sb.AppendLine(skill.Content.Length > Numeric4000 ? skill.Content.Substring(0, Numeric4000) : skill.Content);
            _ = sb.AppendLine();
        }
    }

    /// <summary>Appends MCP servers.</summary>
    /// <param name="sb">The builder.</param>
    /// <param name="request">The request.</param>
    private void AppendMcpServers(StringBuilder sb, CodexRunRequest request)
    {
        if (!request.Options.IncludeMcpServers || !request.McpServers.Any())
        {
            return;
        }

        _ = sb.AppendLine("## MCP servers available through local Codex config");
        foreach (var server in request.McpServers.Where(x => x.IsEnabled))
        {
            _ = sb.AppendLine($"- {server.Name}: {server.Command} {string.Join(" ", server.Args)} ({server.Health})");
        }

        _ = sb.AppendLine();
    }

    /// <summary>Appends workspace context.</summary>
    /// <param name="sb">The builder.</param>
    /// <param name="request">The request.</param>
    private void AppendWorkspaceContext(StringBuilder sb, CodexRunRequest request)
    {
        if (!request.Options.IncludeWorkspaceContext || !request.WorkspaceFiles.Any())
        {
            return;
        }

        _ = sb.AppendLine("## Referenced workspace context");
        foreach (var f in request.WorkspaceFiles)
        {
            var key = string.IsNullOrWhiteSpace(f.ReferenceKey) ? $"@{f.RelativePath}" : f.ReferenceKey;
            if (string.Equals(f.ReferenceKind, "selection", System.StringComparison.OrdinalIgnoreCase))
            {
                _ = sb.AppendLine($"### {key}");
                _ = sb.AppendLine($"Source: {f.RelativePath}, lines {f.StartLine}-{f.EndLine}");
            }
            else
            {
                _ = sb.AppendLine($"### {key}");
                _ = sb.AppendLine($"Source: {f.RelativePath}");
            }

            _ = sb.AppendLine("```");
            _ = sb.AppendLine(f.Preview);
            _ = sb.AppendLine("```");
        }

        _ = sb.AppendLine();
    }

    /// <summary>Appends attachments.</summary>
    /// <param name="sb">The builder.</param>
    /// <param name="request">The request.</param>
    private void AppendAttachments(StringBuilder sb, CodexRunRequest request)
    {
        if (!request.Attachments.Any())
        {
            return;
        }

        _ = sb.AppendLine("## Attachments");
        foreach (var attachment in request.Attachments)
        {
            _ = sb.AppendLine($"- {attachment.Kind}: {attachment.Path}");
        }

        _ = sb.AppendLine();
    }

    /// <summary>Appends the mode instruction.</summary>
    /// <param name="sb">The builder.</param>
    /// <param name="request">The request.</param>
    private void AppendModeInstruction(StringBuilder sb, CodexRunRequest request)
    {
        if (request.Options.Mode == CodexRunMode.Plan)
        {
            _ = sb.AppendLine("Return an actionable implementation plan first. Do not edit files unless explicitly asked to build/implement.");
        }
        else if (request.Options.Mode == CodexRunMode.Build)
        {
            _ = sb.AppendLine("Implement the requested changes. Keep edits scoped, run relevant checks, and report changed files.");
        }
    }
}
