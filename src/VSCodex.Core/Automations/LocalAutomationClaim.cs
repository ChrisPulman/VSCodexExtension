// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;

namespace VSCodex.Core.Automations;

/// <summary>Owns a claimed execution snapshot and its cross-process live lease.</summary>
/// <param name="job">The detached execution snapshot.</param>
/// <param name="lease">The live lease resource.</param>
public sealed class LocalAutomationClaim(LocalAutomation job, IDisposable lease) : IDisposable
{
    /// <summary>Gets the detached execution snapshot.</summary>
    public LocalAutomation Job { get; } = job;

    /// <summary>Releases live ownership after completion or interruption.</summary>
    public void Dispose() => lease.Dispose();
}
