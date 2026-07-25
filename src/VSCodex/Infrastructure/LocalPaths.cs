// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Linq;

namespace VSCodex.Infrastructure;

/// <summary>Provides the local Paths implementation.</summary>
public static class LocalPaths
{
    /// <summary>Gets the extension Install Root.</summary>
    public static string ExtensionInstallRoot
    {
        get
        {
            var assemblyPath = typeof(LocalPaths).Assembly.Location;
            return string.IsNullOrWhiteSpace(assemblyPath)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetDirectoryName(assemblyPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    /// <summary>Gets the bundled Bridge Script.</summary>
    public static string BundledBridgeScript => Path.Combine(ExtensionInstallRoot, "Resources", "codex-bridge.mjs");

    /// <summary>Gets the app Root.</summary>
    public static string AppRoot => Ensure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSCodex"));

    /// <summary>Gets the settings File.</summary>
    public static string SettingsFile => Path.Combine(AppRoot, "settings.json");

    /// <summary>Gets the workspace State Root.</summary>
    public static string WorkspaceStateRoot => Ensure(Path.Combine(AppRoot, "workspaces"));

    /// <summary>Gets the sessions Root.</summary>
    public static string SessionsRoot => Ensure(Path.Combine(AppRoot, "sessions"));

    /// <summary>Gets the attachments Root.</summary>
    public static string AttachmentsRoot => Ensure(Path.Combine(AppRoot, "attachments"));

    /// <summary>Gets the user Codex Root.</summary>
    public static string UserCodexRoot => Ensure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"));

    /// <summary>Gets the user Codex Config.</summary>
    public static string UserCodexConfig => Path.Combine(UserCodexRoot, "config.toml");

    /// <summary>Gets the user Skills Root.</summary>
    public static string UserSkillsRoot => Ensure(Path.Combine(UserCodexRoot, "skills"));

    /// <summary>Performs the workspace Settings File operation.</summary>
    /// <param name="workspaceId">The workspace Id.</param>
    /// <returns>The workspace Settings File result.</returns>
    public static string WorkspaceSettingsFile(string workspaceId) => Path.Combine(Ensure(Path.Combine(WorkspaceStateRoot, SafeFileName(workspaceId))), "settings.json");

    /// <summary>Ensures the operation.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The ensure result.</returns>
    public static string Ensure(string path)
    {
        _ = Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Performs the safe File Name operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The safe File Name result.</returns>
    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string((value ?? string.Empty).Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "default" : safe;
    }
}
