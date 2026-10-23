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

    /// <summary>Defines the Windows backslash escaping multiplier.</summary>
    private const int EscapeMultiplier = 2;

    /// <summary>Stores the settings.</summary>
    private readonly ISettingsStore _settings = settings;

    /// <summary>Publishes CLI events.</summary>
    private readonly Subject<CodexEvent> _events = new();

    /// <summary>Owns the active process tree.</summary>
    private CliProcessLifetime? _activeLifetime;

    /// <summary>Records an explicit stop.</summary>
    private volatile bool _cancelled;

    /// <summary>Gets CLI events.</summary>
    public IObservable<CodexEvent> Events => _events.AsObservable();

    /// <summary>Runs a request through the CLI fallback.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task whose result contains the run result.</returns>
    public async Task<CodexRunResult> RunAsync(CodexRunRequest request)
    {
        ValidateWorkspace(request);
        using Process process = StartProcess(CreateStartInfo(request));
        using var lifetime = CliProcessLifetime.Attach(process);
        _activeLifetime = lifetime;
        _cancelled = false;
        var parser = new CodexCliJsonParser(request.ThreadId);
        StringBuilder error = new();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                return;
            }

            foreach (CodexEvent item in parser.ParseLine(args.Data))
            {
                _events.OnNext(item);
            }
        };
        process.ErrorDataReceived += (_, args) => HandleStandardError(args, error);
        try
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Exception? stdinException = await WritePromptAsync(process, request.Prompt).ConfigureAwait(false);
            await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
            if (_cancelled)
            {
                throw new OperationCanceledException("Codex CLI execution was stopped.");
            }

            if (process.ExitCode != 0 || stdinException is not null || parser.Failure is not null || !parser.Completed)
            {
                string details = TrimForUi(parser.Failure ?? error.ToString());
                throw new CodexRunFailureException($"Codex CLI exited with code {process.ExitCode}: {details}", stdinException ?? new InvalidOperationException(details));
            }

            return new CodexRunResult { ThreadId = parser.ThreadId, FinalResponse = parser.FinalResponse, RawJson = parser.RawJson, UsedFallback = true };
        }
        finally
        {
            _activeLifetime = null;
        }
    }

    /// <summary>Gets CLI rate limits.</summary>
    /// <returns>A completed task with no CLI rate limit payload.</returns>
    public Task<JObject?> GetRateLimitsAsync() => Task.FromResult<JObject?>(null);

    /// <summary>Gets no model catalog because CLI execution has no discovery endpoint.</summary>
    /// <returns>A null catalog.</returns>
    public Task<JObject?> GetModelsAsync() => GetRateLimitsAsync();

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
    public Task<JObject> RespondToServerRequestAsync(JToken requestId, JObject result)
    {
        return Task.FromException<JObject>(new NotSupportedException("The Codex CLI fallback does not expose app-server approval requests."));
    }

    /// <summary>Cancels the active CLI process without blocking.</summary>
    public void CancelActiveRun()
    {
        _cancelled = true;
        _activeLifetime?.Dispose();
    }

    /// <summary>Builds CLI arguments for a request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The CLI argument text.</returns>
    private string BuildArguments(CodexRunRequest request)
    {
        StringBuilder arguments = new("exec ");
        AppendModelArguments(arguments, request);
        AppendWorkspaceArguments(arguments, request.WorkspaceRoot);
        if (!string.IsNullOrWhiteSpace(request.ThreadId))
        {
            _ = arguments.Append("resume ");
        }

        _ = arguments.Append("--json ");
        foreach (CodexAttachment attachment in request.Attachments)
        {
            if (attachment.Kind == "image")
            {
                _ = arguments.Append("--image ").Append(Quote(attachment.Path)).Append(' ');
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ThreadId))
        {
            _ = arguments.Append(Quote(request.ThreadId!)).Append(' ');
        }

        _ = arguments.Append('-');
        return arguments.ToString();
    }

    /// <summary>Quotes a CLI argument.</summary>
    /// <param name="value">The value to quote.</param>
    /// <returns>The quoted value.</returns>
    private string Quote(string value)
    {
        var quoted = new StringBuilder("\"");
        int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }

            _ = quoted.Append('\\', character == '\"' ? (slashes * EscapeMultiplier) + 1 : slashes).Append(character);

            slashes = 0;
        }

        return quoted.Append('\\', slashes * EscapeMultiplier).Append('\"').ToString();
    }

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
            using (var stdin = Utf8ProcessStreams.CreateWriter(process.StandardInput.BaseStream))
            {
                await stdin.WriteAsync(prompt ?? string.Empty).ConfigureAwait(continueOnCapturedContext: false);
            }

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

    /// <summary>Trims text for UI presentation.</summary>
    /// <param name="value">The value to trim.</param>
    /// <returns>The trimmed text.</returns>
    private string TrimForUi(object value)
    {
        string text = (value?.ToString() ?? string.Empty).Trim();
        return text.Length > MaximumUiErrorLength ? $"{text.Remove(MaximumUiErrorLength)}..." : text;
    }
}
