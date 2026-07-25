// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VSCodex.Core.Chats;

/// <summary>Captures an immutable prompt and the workspace/run inputs that applied when it was submitted.</summary>
public sealed class TurnEnvelope
{
    /// <summary>Initializes a new instance of the <see cref="TurnEnvelope"/> class.</summary>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="workspaceId">The submission-time workspace identity.</param>
    /// <param name="contextSnapshot">The submission-time Visual Studio context.</param>
    /// <param name="settingsSnapshot">The submission-time run settings.</param>
    public TurnEnvelope(
        string prompt,
        string workspaceId,
        string contextSnapshot,
        string settingsSnapshot)
        : this(prompt, workspaceId, contextSnapshot, settingsSnapshot, [], null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TurnEnvelope"/> class with attachments.</summary>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="workspaceId">The submission-time workspace identity.</param>
    /// <param name="contextSnapshot">The submission-time Visual Studio context.</param>
    /// <param name="settingsSnapshot">The submission-time run settings.</param>
    /// <param name="attachments">The submission-time attachment paths.</param>
    public TurnEnvelope(
        string prompt,
        string workspaceId,
        string contextSnapshot,
        string settingsSnapshot,
        IEnumerable<string> attachments)
        : this(prompt, workspaceId, contextSnapshot, settingsSnapshot, attachments, null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TurnEnvelope"/> class.</summary>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="workspaceId">The submission-time workspace identity.</param>
    /// <param name="contextSnapshot">The submission-time Visual Studio context.</param>
    /// <param name="settingsSnapshot">The submission-time run settings.</param>
    /// <param name="attachments">The submission-time attachment paths.</param>
    /// <param name="id">The stable local turn identifier.</param>
    /// <param name="createdAt">The submission time.</param>
    public TurnEnvelope(
        string prompt,
        string workspaceId,
        string contextSnapshot,
        string settingsSnapshot,
        IEnumerable<string>? attachments,
        string? id,
        DateTimeOffset? createdAt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("A turn prompt is required.", nameof(prompt));
        }

        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id!;
        Prompt = prompt;
        WorkspaceId = workspaceId ?? string.Empty;
        ContextSnapshot = contextSnapshot ?? string.Empty;
        SettingsSnapshot = settingsSnapshot ?? string.Empty;
        Attachments = new ReadOnlyCollection<string>((attachments ?? Enumerable.Empty<string>()).ToList());
        CreatedAt = createdAt ?? TimeProvider.System.GetUtcNow();
    }

    /// <summary>Gets the stable local turn identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the user prompt.</summary>
    public string Prompt { get; }

    /// <summary>Gets the workspace identity captured at submission time.</summary>
    public string WorkspaceId { get; }

    /// <summary>Gets the immutable Visual Studio context snapshot.</summary>
    public string ContextSnapshot { get; }

    /// <summary>Gets the immutable run-settings snapshot.</summary>
    public string SettingsSnapshot { get; }

    /// <summary>Gets the attachment paths captured at submission time.</summary>
    public IReadOnlyList<string> Attachments { get; }

    /// <summary>Gets the time at which the prompt was submitted.</summary>
    public DateTimeOffset CreatedAt { get; }
}
