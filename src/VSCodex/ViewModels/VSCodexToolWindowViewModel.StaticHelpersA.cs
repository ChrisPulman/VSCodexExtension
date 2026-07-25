// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace VSCodex.ViewModels;

/// <summary>Provides stateless helpers for workspace and prompt operations.</summary>
public sealed partial class VSCodexToolWindowViewModel
{
    /// <summary>Performs the replace Collection operation.</summary>
    /// <typeparam name="T">The t type.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="values">The values.</param>
    /// <returns><see langword="true"/> when replace Collection succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        List<T> snapshot = (values ?? Enumerable.Empty<T>()).ToList();
        if (collection.Count == snapshot.Count && collection.Zip(snapshot, EqualityComparer<T>.Default.Equals).All((x) => x))
        {
            return false;
        }

        collection.Clear();
        foreach (T value in snapshot)
        {
            collection.Add(value);
        }

        return true;
    }

    /// <summary>Performs the distinct Options operation.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The distinct Options result.</returns>
    private static IEnumerable<string> DistinctOptions(IEnumerable<string>? values)
    {
        return (from x in values ?? Enumerable.Empty<string>()
                where !string.IsNullOrWhiteSpace(x)
                select x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Attempts to extract Voice Submit.</summary>
    /// <param name="transcript">The transcript.</param>
    /// <returns><see langword="true"/> when try Extract Voice Submit succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool TryExtractVoiceSubmit(ref string transcript)
    {
        string normalized = CompactLine(transcript, Numeric400).Trim().TrimEnd('.', '!', '?', ',');
        if (VoiceSubmitOnlyCommands.Any((command) => string.Equals(normalized, command, StringComparison.OrdinalIgnoreCase)))
        {
            transcript = string.Empty;
            return true;
        }

        foreach (string suffix in VoiceSubmitSuffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                transcript = normalized.Substring(0, normalized.Length - suffix.Length).Trim();
                return true;
            }
        }

        transcript = normalized;
        return false;
    }

    /// <summary>Performs the session Belongs To Current Workspace operation.</summary>
    /// <param name="session">The session.</param>
    /// <param name="identity">The identity.</param>
    /// <returns><see langword="true"/> when session Belongs To Current Workspace succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool SessionBelongsToCurrentWorkspace(CodexSessionDocument session, WorkspaceIdentity identity)
    {
        if (identity is null || string.IsNullOrWhiteSpace(identity.Id))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(session.WorkspaceIdentityId) && string.IsNullOrWhiteSpace(session.WorkspaceRoot))
        {
            return true;
        }

        bool matchingIdentity = string.Equals(
            session.WorkspaceIdentityId,
            identity.Id,
            StringComparison.OrdinalIgnoreCase);
        bool matchingRoot = !string.IsNullOrWhiteSpace(session.WorkspaceRoot)
            && string.Equals(session.WorkspaceRoot, identity.RootPath, StringComparison.OrdinalIgnoreCase);
        if (matchingIdentity || matchingRoot)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(session.WorkspaceSolutionPath) ? string.Equals(session.WorkspaceSolutionPath, identity.SolutionPath, StringComparison.OrdinalIgnoreCase) : false;
    }

    /// <summary>Determines whether is Known Different Workspace.</summary>
    /// <param name="session">The session.</param>
    /// <param name="identity">The identity.</param>
    /// <returns><see langword="true"/> when is Known Different Workspace succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool IsKnownDifferentWorkspace(CodexSessionDocument session, WorkspaceIdentity identity)
    {
        if (identity is null || string.IsNullOrWhiteSpace(identity.Id))
        {
            return false;
        }

        bool hasWorkspaceIdentity = !string.IsNullOrWhiteSpace(session.WorkspaceIdentityId);
        bool hasWorkspaceRoot = !string.IsNullOrWhiteSpace(session.WorkspaceRoot);
        bool hasWorkspaceSolution = !string.IsNullOrWhiteSpace(session.WorkspaceSolutionPath);
        return (hasWorkspaceIdentity || hasWorkspaceRoot || hasWorkspaceSolution)
            && !SessionBelongsToCurrentWorkspace(session, identity);
    }

    /// <summary>Applies workspace To Session.</summary>
    /// <param name="session">The session.</param>
    /// <param name="identity">The identity.</param>
    private static void ApplyWorkspaceToSession(CodexSessionDocument session, WorkspaceIdentity identity)
    {
        if (session is null || identity is null || string.IsNullOrWhiteSpace(identity.Id))
        {
            return;
        }

        session.WorkspaceIdentityId = identity.Id;
        session.WorkspaceName = identity.Name;
        session.WorkspaceRoot = identity.RootPath;
        session.WorkspaceSolutionPath = identity.SolutionPath;
    }

    /// <summary>Performs the clone Message operation.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The clone Message result.</returns>
    private static ChatMessage CloneMessage(ChatMessage source)
    {
        return new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Role = source.Role,
            Timestamp = source.Timestamp,
            Content = source.Content,
            CorrelationId = source.CorrelationId,
            IsTransient = source.IsTransient
        };
    }

    /// <summary>Determines whether cancel Rename History Item.</summary>
    /// <param name="item">The item.</param>
    private static void CancelRenameHistoryItem(SessionHistoryItem item)
    {
        if (item is null)
        {
            return;
        }

        item.DraftTitle = item.Title;
        item.IsRenaming = false;
    }

    /// <summary>Builds history Item.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The build History Item result.</returns>
    private static SessionHistoryItem BuildHistoryItem(CodexSessionDocument session)
    {
        return new SessionHistoryItem
        {
            Id = session.Id,
            ThreadId = session.ThreadId,
            Title = DeriveSessionTitle(session),
            Preview = DeriveSessionPreview(session),
            WorkspaceIdentityId = session.WorkspaceIdentityId,
            WorkspaceName = session.WorkspaceName,
            WorkspaceRoot = session.WorkspaceRoot,
            WorkspaceSolutionPath = session.WorkspaceSolutionPath,
            Updated = session.Updated,
            MessageCount = (session.Messages?.Count ?? 0)
        };
    }

    /// <summary>Performs the derive Session Title operation.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The derive Session Title result.</returns>
    private static string DeriveSessionTitle(CodexSessionDocument session)
    {
        if (!string.IsNullOrWhiteSpace(session.Title))
        {
            return CompactLine(session.Title, Numeric90);
        }

        string? firstUserMessage = session.Messages?.FirstOrDefault((ChatMessage message) => message.Role == CodexMessageRole.User && !string.IsNullOrWhiteSpace(message.Content))?.Content;
        return !string.IsNullOrWhiteSpace(firstUserMessage) ? CompactLine(firstUserMessage, Numeric90) : $"VSCodex thread {session.Created.ToLocalTime():g}";
    }

    /// <summary>Performs the derive Session Preview operation.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The derive Session Preview result.</returns>
    private static string DeriveSessionPreview(CodexSessionDocument session)
    {
        string? message = session.Messages?.LastOrDefault((ChatMessage item) => !string.IsNullOrWhiteSpace(item.Content))?.Content;
        return !string.IsNullOrWhiteSpace(message) ? CompactLine(message, Numeric180) : "No messages saved";
    }

    /// <summary>Performs the compact Line operation.</summary>
    /// <param name="value">The value.</param>
    /// <param name="maxLength">The max Length.</param>
    /// <returns>The compact Line result.</returns>
    private static string CompactLine(string? value, int maxLength)
    {
        string text = string.Join(" ", (value ?? string.Empty).Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= maxLength ? text : $"{text.Substring(0, Math.Max(0, maxLength - 1)).TrimEnd()}...";
    }

    /// <summary>Performs the contains operation.</summary>
    /// <param name="value">The value.</param>
    /// <param name="query">The query.</param>
    /// <returns><see langword="true"/> when contains succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool Contains(string? value, string query)
    {
        return (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Performs the distinct Agent Roles operation.</summary>
    /// <param name="agents">The agents.</param>
    /// <returns>The distinct Agent Roles result.</returns>
    private static IEnumerable<AgentRoleDefinition> DistinctAgentRoles(IEnumerable<AgentRoleDefinition> agents)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (AgentRoleDefinition agent in agents ?? Enumerable.Empty<AgentRoleDefinition>())
        {
            string key = (string.IsNullOrWhiteSpace(agent.Name) ? agent.Role : agent.Name);
            key = (key ?? string.Empty).Trim();
            if (key.Length != 0 && seen.Add(key))
            {
                yield return agent;
            }
        }
    }

    /// <summary>Performs the clone Agent Role operation.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The clone Agent Role result.</returns>
    private static AgentRoleDefinition CloneAgentRole(AgentRoleDefinition source)
    {
        return new AgentRoleDefinition
        {
            Name = source.Name,
            Role = source.Role,
            Instructions = source.Instructions,
            Model = source.Model,
            ModelSelectionMode = source.ModelSelectionMode,
            IsEnabled = source.IsEnabled
        };
    }

    /// <summary>Performs the default Agent Roles operation.</summary>
    /// <returns>The default Agent Roles result.</returns>
    private static List<AgentRoleDefinition> DefaultAgentRoles()
    {
        return new ExtensionSettings().AgentRoles.Select(CloneAgentRole).ToList();
    }

    /// <summary>Performs the pick Agent Name operation.</summary>
    /// <param name="agents">The agents.</param>
    /// <param name="preferredName">The preferred Name.</param>
    /// <param name="fallbackIndex">The fallback Index.</param>
    /// <returns>The pick Agent Name result.</returns>
    private static string PickAgentName(IReadOnlyList<AgentRoleDefinition> agents, string preferredName, int fallbackIndex)
    {
        if (agents.Count == 0)
        {
            return preferredName;
        }

        AgentRoleDefinition? matchingName = agents.FirstOrDefault(
            (x) => x.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase));
        AgentRoleDefinition? matchingRole = agents.FirstOrDefault(
            (x) => x.Role.IndexOf(preferredName, StringComparison.OrdinalIgnoreCase) >= 0);
        AgentRoleDefinition agent = matchingName
            ?? matchingRole
            ?? agents[Math.Abs(fallbackIndex) % agents.Count];
        return !string.IsNullOrWhiteSpace(agent.Name) ? agent.Name : preferredName;
    }

    /// <summary>Performs the access Level From Sandbox operation.</summary>
    /// <param name="sandbox">The sandbox.</param>
    /// <returns>The access Level From Sandbox result.</returns>
    private static CodexAccessLevel AccessLevelFromSandbox(SandboxMode sandbox)
    {
        return sandbox switch
        {
            SandboxMode.DangerFullAccess => CodexAccessLevel.FullAccess,
            SandboxMode.ReadOnly => CodexAccessLevel.ReadOnly,
            _ => CodexAccessLevel.Workspace,
        };
    }

    /// <summary>Performs the sandbox From Access Level operation.</summary>
    /// <param name="accessLevel">The access Level.</param>
    /// <returns>The sandbox From Access Level result.</returns>
    private static SandboxMode SandboxFromAccessLevel(CodexAccessLevel accessLevel)
    {
        return accessLevel switch
        {
            CodexAccessLevel.FullAccess => SandboxMode.DangerFullAccess,
            CodexAccessLevel.ReadOnly => SandboxMode.ReadOnly,
            _ => SandboxMode.WorkspaceWrite,
        };
    }

    /// <summary>Determines whether is Valid Skill Name.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when is Valid Skill Name succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool IsValidSkillName(string? name)
    {
        string value = (name ?? string.Empty).Trim();
        return value.Length > 0 && char.IsLetterOrDigit(value[0]) ? value.All((ch) => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-') : false;
    }

    /// <summary>Formats token Count.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The format Token Count result.</returns>
    private static string FormatTokenCount(int tokens)
    {
        if (tokens >= Numeric1000000)
        {
            return ((double)tokens / Numeric1000000).ToString("0.#M");
        }

        return tokens >= Numeric1000 ? ((double)tokens / Numeric1000).ToString("0.#k") : tokens.ToString();
    }

    /// <summary>Opens path.</summary>
    /// <param name="path">The path.</param>
    private static void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _ = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    /// <summary>Performs the quote For Cmd operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The quote For Cmd result.</returns>
    private static string QuoteForCmd(string value)
    {
        return $"\"{(value ?? string.Empty).Replace("\"", "\\\"")}\"";
    }

    /// <summary>Performs the activity Section Title operation.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The activity Section Title result.</returns>
    private static string ActivitySectionTitle(RunActivityKind kind)
    {
        return kind switch
        {
            RunActivityKind.Agent => "Agent actions",
            RunActivityKind.Mcp => "MCP usage",
            RunActivityKind.Skill => "Skill usage",
            RunActivityKind.Files => "Files changed",
            RunActivityKind.Assistant => "Assistant response",
            RunActivityKind.System => "System prompts and diagnostics",
            _ => "Activity",
        };
    }
}
