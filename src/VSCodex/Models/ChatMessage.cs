// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using Newtonsoft.Json;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the chat Message implementation.</summary>
[JsonObject(MemberSerialization.OptOut)]
public sealed class ChatMessage : ReactiveObject
{
    /// <summary>Stores the content.</summary>
    private string _content = string.Empty;

    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the role.</summary>
    public CodexMessageRole Role { get; set; }

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTimeOffset Timestamp { get; set; } = TimeProvider.System.GetLocalNow();

    /// <summary>Gets or sets the content.</summary>
    public string Content { get => _content; set => this.RaiseAndSetIfChanged(ref _content, value); }

    /// <summary>Gets or sets the correlation Id.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets the is Transient.</summary>
    public bool IsTransient { get; set; }
}
