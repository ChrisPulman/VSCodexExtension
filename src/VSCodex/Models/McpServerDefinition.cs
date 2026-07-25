// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;

namespace VSCodex.Models;

/// <summary>Provides the mcp Server Definition implementation.</summary>
public sealed class McpServerDefinition : ReactiveObject
{
    /// <summary>Stores the name.</summary>
    private string _name = string.Empty;

    /// <summary>Stores the command.</summary>
    private string _command = string.Empty;

    /// <summary>Stores the arguments text.</summary>
    private string _argumentsText = string.Empty;

    /// <summary>Stores whether the server is enabled.</summary>
    private bool _isEnabled = true;

    /// <summary>Stores the health.</summary>
    private string _health = "unknown";

    /// <summary>Stores the is Required.</summary>
    private bool _isRequired;

    /// <summary>Stores the transport Type.</summary>
    private string _transportType = "stdio";

    /// <summary>Stores the url.</summary>
    private string _url = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value ?? string.Empty); }

    /// <summary>Gets or sets the transport Type.</summary>
    public string TransportType { get => _transportType; set => this.RaiseAndSetIfChanged(ref _transportType, string.IsNullOrWhiteSpace(value) ? "stdio" : value.Trim()); }

    /// <summary>Gets or sets the command.</summary>
    public string Command { get => _command; set => this.RaiseAndSetIfChanged(ref _command, value ?? string.Empty); }

    /// <summary>Gets or sets the url.</summary>
    public string Url { get => _url; set => this.RaiseAndSetIfChanged(ref _url, value ?? string.Empty); }

    /// <summary>Gets or sets the args.</summary>
    public List<string> Args { get; } = [];

    /// <summary>Gets or sets the arguments Text.</summary>
    public string ArgumentsText
    {
        get => string.IsNullOrWhiteSpace(_argumentsText) ? string.Join(Environment.NewLine, Args ?? new List<string>()) : _argumentsText;
        set
        {
            var text = value ?? string.Empty;
            _ = this.RaiseAndSetIfChanged(ref _argumentsText, text);
            Args.Clear();
            Args.AddRange(text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }

    /// <summary>Gets or sets the env.</summary>
    public Dictionary<string, string> Env { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the is Enabled.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, _isRequired || value);
    }

    /// <summary>Gets or sets the is Required.</summary>
    public bool IsRequired
    {
        get => _isRequired;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref _isRequired, value);
            if (value)
            {
                IsEnabled = true;
            }

            this.RaisePropertyChanged();
            this.RaisePropertyChanged();
        }
    }

    /// <summary>Gets the can Disable.</summary>
    public bool CanDisable => !IsRequired;

    /// <summary>Gets the can Remove.</summary>
    public bool CanRemove => !IsRequired;

    /// <summary>Gets or sets the health.</summary>
    public string Health { get => _health; set => this.RaiseAndSetIfChanged(ref _health, value ?? string.Empty); }

    /// <summary>Gets the endpoint Summary.</summary>
    public string EndpointSummary => string.Equals(TransportType, "url", StringComparison.OrdinalIgnoreCase) ? Url : Command;
}
