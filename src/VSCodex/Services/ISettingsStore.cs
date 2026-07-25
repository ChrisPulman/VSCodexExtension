// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the i Settings Store contract.</summary>
public interface ISettingsStore
{
    /// <summary>Gets the settings Changed.</summary>
    IObservable<ExtensionSettings> SettingsChanged { get; }

    /// <summary>Gets the current.</summary>
    ExtensionSettings Current { get; }

    /// <summary>Saves the operation.</summary>
    /// <param name="settings">The settings.</param>
    void Save(ExtensionSettings settings);

    /// <summary>Loads for Workspace.</summary>
    /// <param name="identity">The identity.</param>
    /// <returns>The load For Workspace result.</returns>
    ExtensionSettings LoadForWorkspace(WorkspaceIdentity identity);

    /// <summary>Saves for Workspace.</summary>
    /// <param name="identity">The identity.</param>
    /// <param name="settings">The settings.</param>
    void SaveForWorkspace(WorkspaceIdentity identity, ExtensionSettings settings);
}
