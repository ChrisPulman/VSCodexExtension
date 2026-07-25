// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Threading.Tasks;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Defines the Codex environment service contract.</summary>
public interface ICodexEnvironmentService
{
    /// <summary>Checks the configured Codex environment.</summary>
    /// <param name="settings">The extension settings.</param>
    /// <returns>A task whose result contains the environment report.</returns>
    Task<CodexEnvironmentReport> CheckAsync(ExtensionSettings settings);

    /// <summary>Builds Windows setup instructions.</summary>
    /// <param name="settings">The extension settings.</param>
    /// <returns>The setup instructions.</returns>
    string BuildWindowsInstallInstructions(ExtensionSettings settings);
}
