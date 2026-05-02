using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

public interface ICodexEnvironmentService
{
    Task<CodexEnvironmentReport> CheckAsync(ExtensionSettings settings);
    string BuildWindowsInstallInstructions(ExtensionSettings settings);
}

public sealed class CodexEnvironmentService : ICodexEnvironmentService
{
    public async Task<CodexEnvironmentReport> CheckAsync(ExtensionSettings settings)
    {
        var items = new List<PrerequisiteStatus>();
        var node = await CheckExecutableAsync(ResolveNodePath(settings.NodePath), "--version", "Node.js", "Required to run the VSCodex SDK bridge.", "winget install OpenJS.NodeJS.LTS").ConfigureAwait(false);
        items.Add(node);

        var npm = await CheckExecutableAsync(ResolveNpmPath(), "--version", "npm", "Required to install @openai/codex-sdk on Windows.", "winget install OpenJS.NodeJS.LTS").ConfigureAwait(false);
        items.Add(npm);

        var sdk = node.State == PrerequisiteState.Ready
            ? await CheckCodexSdkAsync(settings).ConfigureAwait(false)
            : Missing("Codex SDK", "Required for the primary VSCodex SDK bridge.", "Install Node.js first, then install @openai/codex-sdk.", "npm install -g @openai/codex-sdk", true);
        items.Add(sdk);

        var cli = await CheckExecutableAsync(ResolveCodexCliPath(settings.CodexCliPath), "--version", "Codex CLI fallback", "Optional fallback transport used if the SDK bridge cannot complete a request.", "npm install -g @openai/codex").ConfigureAwait(false);
        if (cli.State == PrerequisiteState.Missing || cli.State == PrerequisiteState.Error)
        {
            cli.State = PrerequisiteState.Warning;
            cli.Status = "Optional fallback not found";
            cli.Details = "The SDK bridge can still run if @openai/codex-sdk is installed. Install the CLI only if you want fallback execution.";
            cli.IsBlocking = false;
        }

        items.Add(cli);
        var report = new CodexEnvironmentReport
        {
            Items = items,
            IsSdkReady = sdk.State == PrerequisiteState.Ready,
            IsCliReady = cli.State == PrerequisiteState.Ready,
            Instructions = BuildWindowsInstallInstructions(settings)
        };

        report.Summary = report.IsSdkReady
            ? report.IsCliReady ? "Codex SDK and CLI fallback are ready." : "Codex SDK is ready. CLI fallback is optional and not installed."
            : "Codex SDK setup is required before VSCodex can run.";
        return report;
    }

    public string BuildWindowsInstallInstructions(ExtensionSettings settings)
    {
        var nodePath = string.IsNullOrWhiteSpace(settings.NodePath) ? "node" : settings.NodePath;
        var cliPath = string.IsNullOrWhiteSpace(settings.CodexCliPath) ? "codex" : settings.CodexCliPath;
        return "Windows setup for VSCodex:" + Environment.NewLine
            + "1. Install Node.js LTS: winget install OpenJS.NodeJS.LTS" + Environment.NewLine
            + "2. Restart Visual Studio so node and npm are visible on PATH." + Environment.NewLine
            + "3. Install the Codex SDK used by the VSCodex bridge: npm install -g @openai/codex-sdk" + Environment.NewLine
            + "4. Optional fallback CLI: npm install -g @openai/codex" + Environment.NewLine
            + "5. Authenticate Codex/OpenAI from PowerShell if required by your account, then reopen VSCodex." + Environment.NewLine
            + "Current Node Path: " + nodePath + Environment.NewLine
            + "Current Codex CLI Path: " + cliPath;
    }

    private static async Task<PrerequisiteStatus> CheckCodexSdkAsync(ExtensionSettings settings)
    {
        var script = settings.BridgeScriptPath;
        if (string.IsNullOrWhiteSpace(script))
        {
            script = LocalPaths.BundledBridgeScript;
        }

        if (!File.Exists(script))
        {
            return Missing("Codex SDK", "Required for the primary VSCodex SDK bridge.", "The bundled codex-bridge.mjs file was not found at " + script + ".", "Rebuild or reinstall the VSCodex VSIX.", true);
        }

        var result = await RunProcessAsync(ResolveNodePath(settings.NodePath), Quote(script) + " --check", LocalPaths.ExtensionInstallRoot, 8000).ConfigureAwait(false);
        if (!result.Started)
        {
            return Missing("Codex SDK", "Required for the primary VSCodex SDK bridge.", result.Error, "winget install OpenJS.NodeJS.LTS", true);
        }

        if (result.ExitCode == 0)
        {
            return Ready("Codex SDK", "Required for the primary VSCodex SDK bridge.", FirstLine(result.Output));
        }

        return Missing("Codex SDK", "Required for the primary VSCodex SDK bridge.", TrimForUi(result.Error + Environment.NewLine + result.Output), "npm install -g @openai/codex-sdk", true);
    }

    private static async Task<PrerequisiteStatus> CheckExecutableAsync(string fileName, string arguments, string name, string description, string installCommand)
    {
        var result = await RunProcessAsync(fileName, arguments, LocalPaths.ExtensionInstallRoot, 5000).ConfigureAwait(false);
        if (!result.Started)
        {
            return Missing(name, description, result.Error, installCommand, name != "Codex CLI fallback");
        }

        if (result.ExitCode == 0)
        {
            return Ready(name, description, FirstLine(result.Output));
        }

        return new PrerequisiteStatus
        {
            Name = name,
            Description = description,
            State = PrerequisiteState.Error,
            Status = "Found but failed",
            Details = TrimForUi(result.Error + Environment.NewLine + result.Output),
            InstallCommand = installCommand,
            IsBlocking = name != "Codex CLI fallback"
        };
    }

    private static PrerequisiteStatus Ready(string name, string description, string details)
    {
        return new PrerequisiteStatus
        {
            Name = name,
            Description = description,
            State = PrerequisiteState.Ready,
            Status = "Ready",
            Details = string.IsNullOrWhiteSpace(details) ? "Detected." : details
        };
    }

    private static PrerequisiteStatus Missing(string name, string description, string details, string installCommand, bool isBlocking)
    {
        return new PrerequisiteStatus
        {
            Name = name,
            Description = description,
            State = PrerequisiteState.Missing,
            Status = "Missing",
            Details = TrimForUi(details),
            InstallCommand = installCommand,
            IsBlocking = isBlocking
        };
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, string workingDirectory, int timeoutMs)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();
        var process = new Process { StartInfo = CreateProcessStartInfo(fileName, arguments, workingDirectory) };

        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        try
        {
            if (!process.Start())
            {
                return ProcessResult.NotStarted("Process did not start: " + fileName);
            }
        }
        catch (Win32Exception ex)
        {
            return ProcessResult.NotStarted(ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            return ProcessResult.NotStarted(ex.Message);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var exited = await Task.Run(() => process.WaitForExit(timeoutMs)).ConfigureAwait(false);
        if (!exited)
        {
            try { process.Kill(); } catch { }
            return new ProcessResult(true, -1, output.ToString(), "Timed out while checking " + fileName);
        }

        process.WaitForExit();
        return new ProcessResult(true, process.ExitCode, output.ToString(), error.ToString());
    }

    internal static string ResolveCodexCliPath(string configuredPath)
    {
        var appDataCommand = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "codex.cmd");
        var localAppDataExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin", "codex.exe");
        if (File.Exists(appDataCommand))
        {
            return appDataCommand;
        }

        return ResolveExecutable(configuredPath, "codex", localAppDataExe);
    }

    internal static string ResolveNodePath(string configuredPath)
    {
        return ResolveExecutable(configuredPath, "node.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"));
    }

    private static string ResolveNpmPath()
    {
        return ResolveExecutable("npm", "npm.cmd", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "npm.cmd"));
    }

    internal static ProcessStartInfo CreateProcessStartInfo(string fileName, string arguments, string workingDirectory, bool redirectStandardInput = false)
    {
        var commandFile = fileName;
        var commandArguments = arguments ?? string.Empty;
        if (IsCommandScript(fileName))
        {
            commandFile = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            commandArguments = "/d /s /c call " + QuoteForCmd(fileName) + (string.IsNullOrWhiteSpace(arguments) ? string.Empty : " " + arguments);
        }

        return new ProcessStartInfo
        {
            FileName = commandFile,
            Arguments = commandArguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
    }

    private static string ResolveExecutable(string configuredPath, string defaultExecutable, string commonWindowsPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && (Path.IsPathRooted(configuredPath) || configuredPath.Contains("\\") || configuredPath.Contains("/")))
        {
            return configuredPath;
        }

        if (File.Exists(commonWindowsPath))
        {
            return commonWindowsPath;
        }

        return string.IsNullOrWhiteSpace(configuredPath) ? defaultExecutable : configuredPath;
    }

    private static bool IsCommandScript(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private static string QuoteForCmd(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private static string FirstLine(string value)
    {
        var lines = (value ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 0 ? string.Empty : lines[0];
    }
    private static string TrimForUi(string value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= 1200 ? text : text.Substring(0, 1200) + "...";
    }

    private sealed class ProcessResult
    {
        public ProcessResult(bool started, int exitCode, string output, string error)
        {
            Started = started;
            ExitCode = exitCode;
            Output = output ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public bool Started { get; }
        public int ExitCode { get; }
        public string Output { get; }
        public string Error { get; }

        public static ProcessResult NotStarted(string error) => new ProcessResult(false, -1, string.Empty, error);
    }
}
