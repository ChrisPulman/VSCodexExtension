// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;

namespace VSCodex.Core.Automations;

/// <summary>Coordinates durable execution ownership across hosts.</summary>
public interface ILocalAutomationCoordinator
{
    /// <summary>Reloads and atomically claims a due job if no live owner exists.</summary>
    /// <param name="id">The schedule identifier.</param>
    /// <param name="now">The UTC claim timestamp.</param>
    /// <param name="leaseDuration">The stale-owner recovery interval.</param>
    /// <returns>The owned snapshot, or null when busy, paused, deleted or not due.</returns>
    LocalAutomationClaim? TryClaim(string id, DateTimeOffset now, TimeSpan leaseDuration);

    /// <summary>Merges result fields into the surviving matching claim without overwriting user changes.</summary>
    /// <param name="completed">The completed execution snapshot.</param>
    /// <returns>True when merged or no longer applicable; false when the state lease is busy.</returns>
    bool TryComplete(LocalAutomation completed);
}
