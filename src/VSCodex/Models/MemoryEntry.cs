// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the memory Entry implementation.</summary>
public sealed class MemoryEntry : ReactiveObject
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the scope.</summary>
    public string Scope { get; set; } = "user";

    /// <summary>Gets or sets the created.</summary>
    public DateTimeOffset Created { get; set; } = TimeProvider.System.GetLocalNow();

    /// <summary>Gets or sets the updated.</summary>
    public DateTimeOffset Updated { get; set; } = TimeProvider.System.GetLocalNow();
}
