// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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

/// <summary>Provides the codex Environment Service implementation.</summary>
public sealed class CodexEnvironmentService : ICodexEnvironmentService
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric1200 = 1200;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric5000 = 5000;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric8000 = 8000;

    /// <summary>Named string used by this type.</summary>
    private const string VersionText = "--version";

    /// <summary>Named string used by this type.</summary>
    private const string CodexCLIFallbackText = "Codex CLI fallback";

    /// <summary>Named string used by this type.</summary>
    private const string CodexSDKText = "Codex SDK";

    /// <summary>Defines the npm executable name.</summary>
    private const string NpmExecutableName = "npm";

    /// <summary>Named string used by this type.</summary>
    private const string RequiredForThePrimaryVSCodexSDKBridgeText = "Required for the primary VSCodex SDK bridge.";

    /// <summary>Named string used by this type.</summary>
    private const string WingetInstallOpenJSNodeJSLTSText = "winget install OpenJS.NodeJS.LTS";

    /// <summary>Checks the operation.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    public async Task<CodexEnvironmentReport> CheckAsync(ExtensionSettings settings)
    {
        var items = new List<PrerequisiteStatus>();
        var node = await CheckExecutableAsync(
            ResolveNodePath(settings.NodePath),
            VersionText,
            "Node.js",
            "Required to run the VSCodex SDK bridge.",
            WingetInstallOpenJSNodeJSLTSText).ConfigureAwait(false);
        items.Add(node);

        var npm = await CheckExecutableAsync(
            ResolveNpmPath(),
            VersionText,
            NpmExecutableName,
            "Required to install @openai/codex-sdk on Windows.",
            WingetInstallOpenJSNodeJSLTSText).ConfigureAwait(false);
        items.Add(npm);

        var sdk = node.State == PrerequisiteState.Ready
            ? await CheckCodexSdkAsync(settings).ConfigureAwait(false)
            : Missing(CodexSDKText, RequiredForThePrimaryVSCodexSDKBridgeText, "Install Node.js first, then install @openai/codex-sdk.", "npm install -g @openai/codex-sdk", true);
        items.Add(sdk);

        var cli = await CheckExecutableAsync(
            ResolveCodexCliPath(settings.CodexCliPath),
            VersionText,
            CodexCLIFallbackText,
            "Optional fallback transport used if the SDK bridge cannot complete a request.",
            "npm install -g @openai/codex").ConfigureAwait(false);
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

        report.Summary = BuildReportSummary(report);
        return report;
    }

    /// <summary>Builds windows Install Instructions.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The build Windows Install Instructions result.</returns>
    public string BuildWindowsInstallInstructions(ExtensionSettings settings)
    {
        var nodePath = string.IsNullOrWhiteSpace(settings.NodePath) ? "node" : settings.NodePath;
        var cliPath = string.IsNullOrWhiteSpace(settings.CodexCliPath) ? "codex" : settings.CodexCliPath;
        return string.Join(
            Environment.NewLine,
            "Windows setup for VSCodex:",
            "1. Install Node.js LTS: winget install OpenJS.NodeJS.LTS",
            "2. Restart Visual Studio so node and npm are visible on PATH.",
            "3. Install the Codex SDK used by the VSCodex bridge: npm install -g @openai/codex-sdk",
            "4. Optional fallback CLI: npm install -g @openai/codex",
            "5. Authenticate Codex/OpenAI from PowerShell if required by your account, then reopen VSCodex.",
            $"Current Node Path: {nodePath}",
            $"Current Codex CLI Path: {cliPath}");
    }

    /// <summary>Builds the setup report summary.</summary>
    /// <param name="report">The setup report.</param>
    /// <returns>The setup summary.</returns>
    internal static string BuildReportSummary(CodexEnvironmentReport report)
    {
        if (!report.IsSdkReady)
        {
            return "Codex SDK setup is required before VSCodex can run.";
        }

        return report.IsCliReady
            ? "Codex SDK and CLI fallback are ready."
            : "Codex SDK is ready. CLI fallback is optional and not installed.";
    }

    /// <summary>Resolves the configured Codex CLI path.</summary>
    /// <param name="configuredPath">The configured path.</param>
    /// <returns>The CLI executable path.</returns>
    internal static string ResolveCodexCliPath(string configuredPath)
    {
        var appDataCommand = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "codex.cmd");
        var localAppDataExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin", "codex.exe");
        return File.Exists(appDataCommand) ? appDataCommand : ResolveExecutable(configuredPath, "codex", localAppDataExe);
    }

    /// <summary>Resolves the configured Node.js path.</summary>
    /// <param name="configuredPath">The configured path.</param>
    /// <returns>The Node.js executable path.</returns>
    internal static string ResolveNodePath(string configuredPath)
    {
        return ResolveExecutable(
            configuredPath,
            "node.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"));
    }

    /// <summary>Creates process start information.</summary>
    /// <param name="fileName">The executable name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="redirectStandardInput">Whether to redirect standard input.</param>
    /// <returns>The process start information.</returns>
    internal static ProcessStartInfo CreateProcessStartInfo(
        string fileName,
        string arguments,
        string workingDirectory,
        bool redirectStandardInput = false)
    {
        var commandFile = fileName;
        var commandArguments = arguments ?? string.Empty;
        if (IsCommandScript(fileName))
        {
            commandFile = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            string commandSuffix = string.IsNullOrWhiteSpace(arguments) ? string.Empty : $" {arguments}";
            commandArguments = $"/d /s /c call {Quote(fileName)}{commandSuffix}";
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

    /// <summary>Checks codex Sdk.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    private static async Task<PrerequisiteStatus> CheckCodexSdkAsync(ExtensionSettings settings)
    {
        var script = settings.BridgeScriptPath;
        if (string.IsNullOrWhiteSpace(script))
        {
            script = LocalPaths.BundledBridgeScript;
        }

        if (!File.Exists(script))
        {
            return Missing(CodexSDKText, RequiredForThePrimaryVSCodexSDKBridgeText, $"The bundled codex-bridge.mjs file was not found at {script}.", "Rebuild or reinstall the VSCodex VSIX.", true);
        }

        var result = await RunProcessAsync(ResolveNodePath(settings.NodePath), $"{Quote(script)} --check", LocalPaths.ExtensionInstallRoot, Numeric8000).ConfigureAwait(false);
        if (!result.Started)
        {
            return Missing(CodexSDKText, RequiredForThePrimaryVSCodexSDKBridgeText, result.Error, WingetInstallOpenJSNodeJSLTSText, true);
        }

        if (result.ExitCode == 0)
        {
            return Ready(CodexSDKText, RequiredForThePrimaryVSCodexSDKBridgeText, FirstLine(result.Output));
        }

        string details = TrimForUi(result.Error + Environment.NewLine + result.Output);
        return Missing(
            CodexSDKText,
            RequiredForThePrimaryVSCodexSDKBridgeText,
            details,
            "npm install -g @openai/codex-sdk",
            true);
    }

    /// <summary>Checks executable.</summary>
    /// <param name="fileName">The file Name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="installCommand">The install Command.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    private static async Task<PrerequisiteStatus> CheckExecutableAsync(string fileName, string arguments, string name, string description, string installCommand)
    {
        var result = await RunProcessAsync(fileName, arguments, LocalPaths.ExtensionInstallRoot, Numeric5000).ConfigureAwait(false);
        if (!result.Started)
        {
            return Missing(name, description, result.Error, installCommand, name != CodexCLIFallbackText);
        }

        return result.ExitCode == 0 ? Ready(name, description, FirstLine(result.Output)) : new PrerequisiteStatus
        {
            Name = name,
            Description = description,
            State = PrerequisiteState.Error,
            Status = "Found but failed",
            Details = TrimForUi(result.Error + Environment.NewLine + result.Output),
            InstallCommand = installCommand,
            UpdateCommand = installCommand,
            IsBlocking = name != CodexCLIFallbackText
        };
    }

    /// <summary>Reads y.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="details">The details.</param>
    /// <returns>The ready result.</returns>
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

    /// <summary>Performs the missing operation.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="details">The details.</param>
    /// <param name="installCommand">The install Command.</param>
    /// <param name="isBlocking">The is Blocking.</param>
    /// <returns>The missing result.</returns>
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
            UpdateCommand = installCommand,
            IsBlocking = isBlocking
        };
    }

    /// <summary>Runs process.</summary>
    /// <param name="fileName">The file Name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="workingDirectory">The working Directory.</param>
    /// <param name="timeoutMs">The timeout Ms.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, string workingDirectory, int timeoutMs)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();
        var process = new Process { StartInfo = CreateProcessStartInfo(fileName, arguments, workingDirectory) };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            _ = output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            _ = error.AppendLine(e.Data);
        };

        try
        {
            if (!process.Start())
            {
                return ProcessResult.NotStarted($"Process did not start: {fileName}");
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
            try
            {
                process.Kill();
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception || ex is ObjectDisposedException)
            {
                _ = ex;
            }

            return new(true, -1, output.ToString(), $"Timed out while checking {fileName}");
        }

        process.WaitForExit();
        return new(true, process.ExitCode, output.ToString(), error.ToString());
    }

    /// <summary>Resolves npm Path.</summary>
    /// <returns>The resolve Npm Path result.</returns>
    private static string ResolveNpmPath()
    {
        return ResolveExecutable("npm", "npm.cmd", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "npm.cmd"));
    }

    /// <summary>Resolves executable.</summary>
    /// <param name="configuredPath">The configured Path.</param>
    /// <param name="defaultExecutable">The default Executable.</param>
    /// <param name="commonWindowsPath">The common Windows Path.</param>
    /// <returns>The resolve Executable result.</returns>
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

    /// <summary>Determines whether is Command Script.</summary>
    /// <param name="fileName">The file Name.</param>
    /// <returns><see langword="true"/> when is Command Script succeeds; otherwise, <see langword="false"/>.</returns>
    private static bool IsCommandScript(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Performs the quote operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The quote result.</returns>
    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    /// <summary>Performs the first Line operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The first Line result.</returns>
    private static string FirstLine(string value)
    {
        var lines = (value ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 0 ? string.Empty : lines[0];
    }

    /// <summary>Performs the trim For Ui operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The trim For Ui result.</returns>
    private static string TrimForUi(string value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= Numeric1200 ? text : $"{text.Substring(0, Numeric1200)}...";
    }

    /// <summary>Provides the process Result implementation.</summary>
    private sealed class ProcessResult
    {
        /// <summary>Initializes a new instance of the <see cref="ProcessResult"/> class.</summary>
        /// <param name="started">The started.</param>
        /// <param name="exitCode">The exit Code.</param>
        /// <param name="output">The output.</param>
        /// <param name="error">The error.</param>
        public ProcessResult(bool started, int exitCode, string output, string error)
        {
            Started = started;
            ExitCode = exitCode;
            Output = output ?? string.Empty;
            Error = error ?? string.Empty;
        }

        /// <summary>Gets the started.</summary>
        public bool Started { get; }

        /// <summary>Gets the exit Code.</summary>
        public int ExitCode { get; }

        /// <summary>Gets the output.</summary>
        public string Output { get; }

        /// <summary>Gets the error.</summary>
        public string Error { get; }

        /// <summary>Performs the not Started operation.</summary>
        /// <param name="error">The error.</param>
        /// <returns>The not Started result.</returns>
        public static ProcessResult NotStarted(string error) => new(false, -1, string.Empty, error);
    }
}
