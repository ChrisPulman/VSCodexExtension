// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the mcp Tool Input Field implementation.</summary>
public sealed class McpToolInputField : ReactiveObject
{
    /// <summary>Stores the value.</summary>
    private string _value = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the type.</summary>
    public string Type { get; set; } = "string";

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the is Required.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Gets or sets the value.</summary>
    public string Value { get => _value; set => this.RaiseAndSetIfChanged(ref _value, value ?? string.Empty); }

    /// <summary>Gets the display Label.</summary>
    public string DisplayLabel => IsRequired ? Name : $"{Name} option";
}
