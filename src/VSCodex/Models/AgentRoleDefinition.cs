// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Newtonsoft.Json;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the agent Role Definition implementation.</summary>
[JsonObject(MemberSerialization.OptOut)]
public sealed class AgentRoleDefinition : ReactiveObject
{
    /// <summary>Stores the model.</summary>
    private string _model = string.Empty;

    /// <summary>Stores whether the role is enabled.</summary>
    private bool _isEnabled = true;

    /// <summary>Stores the model Selection Mode.</summary>
    private AgentModelSelectionMode _modelSelectionMode = AgentModelSelectionMode.Explicit;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the role.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Gets or sets the instructions.</summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>Gets or sets the model.</summary>
    public string Model { get => _model; set => this.RaiseAndSetIfChanged(ref _model, value ?? string.Empty); }

    /// <summary>Gets or sets the model Selection Mode.</summary>
    public AgentModelSelectionMode ModelSelectionMode { get => _modelSelectionMode; set => this.RaiseAndSetIfChanged(ref _modelSelectionMode, value); }

    /// <summary>Gets or sets the is Enabled.</summary>
    public bool IsEnabled { get => _isEnabled; set => this.RaiseAndSetIfChanged(ref _isEnabled, value); }
}
