// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the rate Limit Window Status implementation.</summary>
public sealed class RateLimitWindowStatus : ReactiveObject
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric100 = 100;

    /// <summary>Stores the remaining text.</summary>
    private string _remaining = "Waiting for Codex telemetry";

    /// <summary>Stores the reset text.</summary>
    private string _resetText = string.Empty;

    /// <summary>Stores the usage Percent.</summary>
    private int _usagePercent;

    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the remaining.</summary>
    public string Remaining { get => _remaining; set => this.RaiseAndSetIfChanged(ref _remaining, value ?? string.Empty); }

    /// <summary>Gets or sets the usage Percent.</summary>
    public int UsagePercent { get => _usagePercent; set => this.RaiseAndSetIfChanged(ref _usagePercent, Math.Max(0, Math.Min(Numeric100, value))); }

    /// <summary>Gets or sets the reset Text.</summary>
    public string ResetText { get => _resetText; set => this.RaiseAndSetIfChanged(ref _resetText, value ?? string.Empty); }
}
