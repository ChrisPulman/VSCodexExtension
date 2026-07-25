// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace VSCodex.Models;

/// <summary>Provides the codex Environment Report implementation.</summary>
public sealed class CodexEnvironmentReport
{
    /// <summary>Gets or sets the items.</summary>
    public IReadOnlyList<PrerequisiteStatus> Items { get; set; } = Array.Empty<PrerequisiteStatus>();

    /// <summary>Gets or sets the is Sdk Ready.</summary>
    public bool IsSdkReady { get; set; }

    /// <summary>Gets or sets the is Cli Ready.</summary>
    public bool IsCliReady { get; set; }

    /// <summary>Gets or sets the summary.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the instructions.</summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>Gets the can Run Sdk Bridge.</summary>
    public bool CanRunSdkBridge => IsSdkReady;
}
