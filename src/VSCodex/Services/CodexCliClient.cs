// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VSCodex.Core.Models;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the Codex CLI fallback client.</summary>
/// <param name="settings">The extension settings store.</param>
public sealed class CodexCliClient(ISettingsStore settings) : ICodexClient
{
    /// <summary>Defines the maximum UI error length.</summary>
    private const int MaximumUiErrorLength = 1600;

    /// <summary>Stores the settings.</summary>
    private readonly ISettingsStore _settings = settings;

    /// <summary>Publishes CLI events.</summary>
    private readonly Subject<CodexEvent> _events = new();

    /// <summary>Stores the active CLI process.</summary>
    private Process? _active;

    /// <summary>Gets CLI events.</summary>
    public IObservable<CodexEvent> Events => _events.AsObservable();

    /// <summary>Runs a request through the CLI fallback.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task whose result contains the run result.</returns>
    public async Task<CodexRunResult> RunAsync(CodexRunRequest request)
    {
        ValidateWorkspace(request);
        Process process = StartProcess(CreateStartInfo(request));
        _active = process;
        StringBuilder output = new();
        StringBuilder error = new();
        SubscribeToOutput(process, output, error);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        Exception? stdinException = await WritePromptAsync(process, request.Prompt).ConfigureAwait(continueOnCapturedContext: false);
        await Task.Run(() => process.WaitForExit()).ConfigureAwait(continueOnCapturedContext: false);
        ThrowIfRunFailed(process, output, error, stdinException);
        return new CodexRunResult { FinalResponse = output.ToString(), UsedFallback = true };
    }

    /// <summary>Gets CLI rate limits.</summary>
    /// <returns>A completed task with no CLI rate limit payload.</returns>
    public Task<JObject?> GetRateLimitsAsync() => Task.FromResult<JObject?>(null);

    /// <summary>Reports that CLI steering is unsupported.</summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="prompt">The steering prompt.</param>
    /// <returns>A failed task.</returns>
    public Task<JObject> SteerAsync(string threadId, string prompt)
    {
        return Task.FromException<JObject>(new NotSupportedException("The Codex CLI fallback does not support steering an active turn."));
    }

    /// <summary>Interrupts the active CLI process.</summary>
    /// <param name="threadId">The optional thread identifier.</param>
    /// <returns>A completed interruption response.</returns>
    public Task<JObject> InterruptAsync(string? threadId)
    {
        CancelActiveRun();
        return Task.FromResult(new JObject { ["interrupted"] = true });
    }

    /// <summary>Reports that server requests are unsupported by the CLI.</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="result">The response payload.</param>
    /// <returns>A failed task.</returns>
    public Task<JObject> RespondToServerRequestAsync(string requestId, JObject result)
    {
        return Task.FromException<JObject>(new NotSupportedException("The Codex CLI fallback does not expose app-server approval requests."));
    }

    /// <summary>Cancels the active CLI process without blocking.</summary>
    public void CancelActiveRun()
    {
        try
        {
            if (_active?.HasExited == false)
            {
                _active.Kill();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception || ex is ObjectDisposedException)
        {
            _events.OnNext(new CodexEvent { Type = "cli-stop-error", Message = ex.Message });
        }
    }

    /// <summary>Builds CLI arguments for a request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The CLI argument text.</returns>
    private string BuildArguments(CodexRunRequest request)
    {
        StringBuilder arguments = new("exec ");
        AppendModelArguments(arguments, request);
        AppendWorkspaceArguments(arguments, request.WorkspaceRoot);
        foreach (CodexAttachment attachment in request.Attachments)
        {
            if (attachment.Kind == "image")
            {
                _ = arguments.Append("--image ").Append(Quote(attachment.Path)).Append(' ');
            }
        }

        _ = arguments.Append('-');
        return arguments.ToString();
    }

    /// <summary>Quotes a CLI argument.</summary>
    /// <param name="value">The value to quote.</param>
    /// <returns>The quoted value.</returns>
    private string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    /// <summary>Converts an approval policy to a CLI setting.</summary>
    /// <param name="policy">The policy.</param>
    /// <returns>The CLI approval setting.</returns>
    private string ToCliApproval(ApprovalPolicy policy)
    {
        return policy switch
        {
            ApprovalPolicy.Untrusted => "untrusted",
            ApprovalPolicy.OnFailure => "on-failure",
            ApprovalPolicy.Never => "never",
            _ => "on-request"
        };
    }

    /// <summary>Converts a sandbox mode to a CLI setting.</summary>
    /// <param name="sandboxMode">The sandbox mode.</param>
    /// <returns>The CLI sandbox setting.</returns>
    private string ToCliSandbox(SandboxMode sandboxMode)
    {
        return sandboxMode switch
        {
            SandboxMode.DangerFullAccess => "danger-full-access",
            SandboxMode.ReadOnly => "read-only",
            _ => "workspace-write"
        };
    }

    /// <summary>Validates that a request has a workspace.</summary>
    /// <param name="request">The request.</param>
    private void ValidateWorkspace(CodexRunRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.WorkspaceRoot))
        {
            return;
        }

        throw new InvalidOperationException("VSCodex cannot run because Visual Studio has not provided a solution or project workspace root yet.");
    }

    /// <summary>Creates the process start information.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The process start information.</returns>
    private ProcessStartInfo CreateStartInfo(CodexRunRequest request)
    {
        string cliPath = CodexEnvironmentService.ResolveCodexCliPath(_settings.Current.CodexCliPath);
        return CodexEnvironmentService.CreateProcessStartInfo(
            cliPath,
            BuildArguments(request),
            request.WorkspaceRoot,
            redirectStandardInput: true);
    }

    /// <summary>Starts the CLI process.</summary>
    /// <param name="startInfo">The process start information.</param>
    /// <returns>The started process.</returns>
    private Process StartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Codex CLI.");
        }
        catch (Win32Exception innerException)
        {
            var message = new StringBuilder();
            _ = message.Append("Codex CLI executable was not found. This is the optional VSCodex fallback transport. ");
            _ = message.Append("Install it on Windows with `npm install -g @openai/codex`, restart Visual Studio, or set the ");
            _ = message.Append("VSCodex Codex CLI Path setting to the full codex.cmd path. Current value: ");
            _ = message.Append(_settings.Current.CodexCliPath);
            throw new InvalidOperationException(message.ToString(), innerException);
        }
    }

    /// <summary>Appends model-related CLI arguments.</summary>
    /// <param name="arguments">The argument builder.</param>
    /// <param name="request">The request.</param>
    private void AppendModelArguments(StringBuilder arguments, CodexRunRequest request)
    {
        string reasoningEffort = CodexModelCatalog.ResolveReasoningEffort(request.Options.Model, request.Options.ReasoningEffort);
        if (!string.IsNullOrWhiteSpace(request.Options.Model))
        {
            _ = arguments.Append("--model ").Append(Quote(request.Options.Model)).Append(' ');
        }

        if (ShouldPassProfile(request.Options.Profile))
        {
            _ = arguments.Append("--profile ").Append(Quote(request.Options.Profile)).Append(' ');
        }

        _ = arguments.Append("--config ")
            .Append(Quote($"approval_policy={ToCliApproval(request.Options.ApprovalPolicy)}"))
            .Append(' ');
        if (!string.IsNullOrWhiteSpace(reasoningEffort))
        {
            _ = arguments.Append("--config ")
                .Append(Quote($"model_reasoning_effort={reasoningEffort}"))
                .Append(' ');
        }

        _ = arguments.Append("--sandbox ").Append(ToCliSandbox(request.Options.SandboxMode)).Append(' ');
    }

    /// <summary>Appends workspace-related CLI arguments.</summary>
    /// <param name="arguments">The argument builder.</param>
    /// <param name="workspaceRoot">The workspace root.</param>
    private void AppendWorkspaceArguments(StringBuilder arguments, string workspaceRoot)
    {
        _ = arguments.Append("--cd ").Append(Quote(workspaceRoot)).Append(' ');
        _ = arguments.Append("--skip-git-repo-check ");
    }

    /// <summary>Determines whether a profile should be passed to the CLI.</summary>
    /// <param name="profile">The profile name.</param>
    /// <returns><see langword="true"/> when the profile should be passed.</returns>
    private bool ShouldPassProfile(string profile)
    {
        return !string.IsNullOrWhiteSpace(profile)
            && !profile.Equals("default", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Determines whether a line is normal process-termination noise.</summary>
    /// <param name="line">The output line.</param>
    /// <returns><see langword="true"/> when the line is termination noise.</returns>
    private bool IsProcessTerminationNoise(string line)
    {
        return line.StartsWith("SUCCESS: The process with PID ", StringComparison.OrdinalIgnoreCase)
            && line.EndsWith(" has been terminated.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Subscribes to process output streams.</summary>
    /// <param name="process">The process.</param>
    /// <param name="output">The standard output buffer.</param>
    /// <param name="error">The standard error buffer.</param>
    private void SubscribeToOutput(Process process, StringBuilder output, StringBuilder error)
    {
        process.OutputDataReceived += (_, eventArgs) => HandleStandardOutput(eventArgs, output);
        process.ErrorDataReceived += (_, eventArgs) => HandleStandardError(eventArgs, error);
    }

    /// <summary>Handles one standard output event.</summary>
    /// <param name="eventArgs">The output event.</param>
    /// <param name="output">The standard output buffer.</param>
    private void HandleStandardOutput(DataReceivedEventArgs eventArgs, StringBuilder output)
    {
        if (eventArgs.Data is null || IsProcessTerminationNoise(eventArgs.Data))
        {
            return;
        }

        _ = output.AppendLine(eventArgs.Data);
        _events.OnNext(new CodexEvent { Type = "stdout", Message = eventArgs.Data });
    }

    /// <summary>Handles one standard error event.</summary>
    /// <param name="eventArgs">The error event.</param>
    /// <param name="error">The standard error buffer.</param>
    private void HandleStandardError(DataReceivedEventArgs eventArgs, StringBuilder error)
    {
        if (eventArgs.Data is null)
        {
            return;
        }

        _ = error.AppendLine(eventArgs.Data);
        _events.OnNext(new CodexEvent { Type = "stderr", Message = eventArgs.Data });
    }

    /// <summary>Writes the prompt and closes standard input.</summary>
    /// <param name="process">The CLI process.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <returns>A task whose result contains an expected stdin failure, if any.</returns>
    private async Task<Exception?> WritePromptAsync(Process process, string? prompt)
    {
        try
        {
            await process.StandardInput.WriteAsync(prompt ?? string.Empty).ConfigureAwait(continueOnCapturedContext: false);
            process.StandardInput.Close();
            return null;
        }
        catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException)
        {
            TryCloseStandardInput(process);
            return ex;
        }
    }

    /// <summary>Closes standard input when the CLI has already exited.</summary>
    /// <param name="process">The CLI process.</param>
    private void TryCloseStandardInput(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException)
        {
            _ = ex;
        }
    }

    /// <summary>Throws when CLI execution did not complete successfully.</summary>
    /// <param name="process">The completed CLI process.</param>
    /// <param name="output">The standard output buffer.</param>
    /// <param name="error">The standard error buffer.</param>
    /// <param name="stdinException">The optional stdin exception.</param>
    private void ThrowIfRunFailed(Process process, StringBuilder output, StringBuilder error, Exception? stdinException)
    {
        if (process.ExitCode == 0 && stdinException is null)
        {
            return;
        }

        string details = TrimForUi(error + Environment.NewLine + output);
        string message = $"Codex CLI fallback exited with code {process.ExitCode}: {details}";
        if (stdinException is not null)
        {
            message = $"{message} Stdin write failed because codex exited early: {stdinException.Message}";
        }

        throw new InvalidOperationException(message, stdinException);
    }

    /// <summary>Trims text for UI presentation.</summary>
    /// <param name="value">The value to trim.</param>
    /// <returns>The trimmed text.</returns>
    private string TrimForUi(object value)
    {
        string text = (value?.ToString() ?? string.Empty).Trim();
        return text.Length > MaximumUiErrorLength ? $"{text.Substring(0, MaximumUiErrorLength)}..." : text;
    }
}
