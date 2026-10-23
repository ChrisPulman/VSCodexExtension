// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;

namespace VSCodex.Core.Automations;

/// <summary>Stores one explicitly enabled local recurring prompt and its latest notification.</summary>
public sealed class LocalAutomation
{
    /// <summary>Gets or sets the stable identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the saved prompt.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Gets or sets the captured workspace directory.</summary>
    public string Workspace { get; set; } = string.Empty;

    /// <summary>Gets or sets the interval in minutes.</summary>
    public int IntervalMinutes { get; set; }

    /// <summary>Gets or sets the next due UTC timestamp.</summary>
    public DateTimeOffset NextDue { get; set; }

    /// <summary>Gets or sets a value indicating whether the user opted into execution.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the latest completion timestamp.</summary>
    public DateTimeOffset? LastCompleted { get; set; }

    /// <summary>Gets or sets the latest response or error.</summary>
    public string LastResult { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the latest execution failed.</summary>
    public bool LastFailed { get; set; }

    /// <summary>Gets or sets a value indicating whether the latest result is unread.</summary>
    public bool Unread { get; set; }

    /// <summary>Gets or sets the owner token of the currently claimed execution.</summary>
    public string ClaimId { get; set; } = string.Empty;

    /// <summary>Gets or sets the stale-owner recovery deadline.</summary>
    public DateTimeOffset? ClaimExpires { get; set; }
}
