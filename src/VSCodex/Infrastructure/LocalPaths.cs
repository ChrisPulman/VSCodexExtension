using System;
using System.IO;

namespace VSCodex.Infrastructure;

public static class LocalPaths
{
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

    public static string BundledBridgeScript => Path.Combine(ExtensionInstallRoot, "Resources", "codex-bridge.mjs");
    public static string AppRoot => Ensure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSCodex"));
    public static string SettingsFile => Path.Combine(AppRoot, "settings.json");
    public static string MemoryFile => Path.Combine(AppRoot, "memory.json");
    public static string SessionsRoot => Ensure(Path.Combine(AppRoot, "sessions"));
    public static string AttachmentsRoot => Ensure(Path.Combine(AppRoot, "attachments"));
    public static string UserCodexRoot => Ensure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"));
    public static string UserCodexConfig => Path.Combine(UserCodexRoot, "config.toml");
    public static string UserSkillsRoot => Ensure(Path.Combine(UserCodexRoot, "skills"));
    public static string Ensure(string path) { Directory.CreateDirectory(path); return path; }
}
