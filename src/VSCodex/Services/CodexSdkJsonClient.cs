// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VSCodex.Core.Models;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the Codex SDK JSON client implementation.</summary>
/// <param name="settings">The extension settings store.</param>
public sealed class CodexSdkJsonClient(ISettingsStore settings) : ICodexClient, IDisposable
{
    /// <summary>Defines the bridge startup delay in milliseconds.</summary>
    private const int BridgeStartupDelayMilliseconds = 150;

    /// <summary>Defines the command property name.</summary>
    private const string CommandPropertyName = "command";

    /// <summary>Defines the thread identifier property name.</summary>
    private const string ThreadIdPropertyName = "threadId";

    /// <summary>Stores the settings.</summary>
    private readonly ISettingsStore _settings = settings;

    /// <summary>Stores published bridge events.</summary>
    private readonly Subject<CodexEvent> _events = new();

    /// <summary>Stores pending bridge requests.</summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pending = new();

    /// <summary>Stores the bridge process.</summary>
    private Process? _process;

    /// <summary>Stores the bridge standard input.</summary>
    private StreamWriter? _stdin;

    /// <summary>Gets the bridge events.</summary>
    public IObservable<CodexEvent> Events => _events.AsObservable();

    /// <summary>Runs the request through the SDK bridge.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task whose result contains the run result.</returns>
    public async Task<CodexRunResult> RunAsync(CodexRunRequest request)
    {
        ValidateWorkspace(request);
        await EnsureBridgeAsync().ConfigureAwait(continueOnCapturedContext: false);
        JObject response = await SendAsync(CreateRunPayload(request)).ConfigureAwait(continueOnCapturedContext: false);
        string rawJson = ToCompactJson(response);
        return new CodexRunResult
        {
            ThreadId = response.Value<string>(ThreadIdPropertyName),
            FinalResponse = response.Value<string>("finalResponse") ?? rawJson,
            RawJson = rawJson
        };
    }

    /// <summary>Gets rate limits from the SDK bridge.</summary>
    /// <returns>A task whose result contains the rate limits.</returns>
    public async Task<JObject?> GetRateLimitsAsync()
    {
        await EnsureBridgeAsync().ConfigureAwait(continueOnCapturedContext: false);
        return await SendAsync(new JObject { [CommandPropertyName] = "getRateLimits" })
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>Steers an active SDK turn.</summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="prompt">The steering prompt.</param>
    /// <returns>A task whose result contains the bridge response.</returns>
    public async Task<JObject> SteerAsync(string threadId, string prompt)
    {
        await EnsureBridgeAsync().ConfigureAwait(continueOnCapturedContext: false);
        return await SendAsync(new JObject
        {
            [CommandPropertyName] = "steer",
            [nameof(threadId)] = threadId,
            [nameof(prompt)] = prompt
        }).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>Interrupts an active SDK turn.</summary>
    /// <param name="threadId">The optional thread identifier.</param>
    /// <returns>A task whose result contains the bridge response.</returns>
    public async Task<JObject> InterruptAsync(string? threadId)
    {
        await EnsureBridgeAsync().ConfigureAwait(continueOnCapturedContext: false);
        return await SendAsync(new JObject
        {
            [CommandPropertyName] = "interrupt",
            [nameof(threadId)] = threadId
        }).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>Responds to an SDK bridge server request.</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="result">The response payload.</param>
    /// <returns>A task whose result contains the bridge response.</returns>
    public async Task<JObject> RespondToServerRequestAsync(string requestId, JObject result)
    {
        await EnsureBridgeAsync().ConfigureAwait(continueOnCapturedContext: false);
        return await SendAsync(new JObject
        {
            [CommandPropertyName] = "respondServerRequest",
            [nameof(requestId)] = requestId,
            [nameof(result)] = result
        }).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>Cancels the active run without blocking the caller.</summary>
    public void CancelActiveRun() => _ = ObserveInterruptAsync();

    /// <summary>Releases process resources.</summary>
    public void Dispose()
    {
        TryKillProcess();
        _events.Dispose();
    }

    /// <summary>Creates a run payload.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The bridge payload.</returns>
    private static JObject CreateRunPayload(CodexRunRequest request)
    {
        return new JObject
        {
            [CommandPropertyName] = string.IsNullOrWhiteSpace(request.ThreadId) ? "startAndRun" : "resumeAndRun",
            [ThreadIdPropertyName] = request.ThreadId,
            ["prompt"] = request.Prompt,
            ["model"] = request.Options.Model,
            ["reasoningEffort"] = CodexModelCatalog.ResolveReasoningEffort(request.Options.Model, request.Options.ReasoningEffort),
            ["verbosity"] = request.Options.Verbosity,
            ["serviceTier"] = request.Options.ServiceTier,
            ["profile"] = request.Options.Profile,
            ["approvalPolicy"] = request.Options.ApprovalPolicy.ToString(),
            ["sandboxMode"] = request.Options.SandboxMode.ToString(),
            ["workspaceRoot"] = request.WorkspaceRoot,
            ["workspaceName"] = request.WorkspaceName,
            ["workspaceSolutionPath"] = request.WorkspaceSolutionPath,
            ["workspaceMemoryRoot"] = request.WorkspaceMemoryRoot,
            ["workspaceIdentity"] = JObject.FromObject(request.WorkspaceIdentity ?? new WorkspaceIdentity()),
            ["operationId"] = request.OperationId,
            ["images"] = JArray.FromObject(request.Attachments)
        };
    }

    /// <summary>Quotes a process argument.</summary>
    /// <param name="value">The value to quote.</param>
    /// <returns>The quoted value.</returns>
    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    /// <summary>Serializes a JSON token without formatting.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The compact JSON text.</returns>
    private static string ToCompactJson(JToken token) => JsonConvert.SerializeObject(token);

    /// <summary>Validates that a request has a workspace.</summary>
    /// <param name="request">The request.</param>
    private static void ValidateWorkspace(CodexRunRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.WorkspaceRoot))
        {
            return;
        }

        throw new InvalidOperationException("VSCodex cannot run because Visual Studio has not provided a solution or project workspace root yet.");
    }

    /// <summary>Observes a non-blocking interruption request.</summary>
    /// <returns>A task that completes after interruption handling.</returns>
    private async Task ObserveInterruptAsync()
    {
        try
        {
            await InterruptAsync(null).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is ObjectDisposedException)
        {
            _events.OnNext(new CodexEvent { Type = "interrupt-error", Message = ex.Message });
        }
    }

    /// <summary>Sends a request to the SDK bridge.</summary>
    /// <param name="payload">The request payload.</param>
    /// <returns>A task whose result contains the bridge response.</returns>
    private async Task<JObject> SendAsync(JObject payload)
    {
        if (_stdin is null)
        {
            throw new InvalidOperationException("Codex SDK bridge is not running.");
        }

        string id = Guid.NewGuid().ToString("N");
        payload[nameof(id)] = id;
        TaskCompletionSource<JObject> completionSource = new();
        _pending[id] = completionSource;
        await _stdin.WriteLineAsync(ToCompactJson(payload)).ConfigureAwait(continueOnCapturedContext: false);
        await _stdin.FlushAsync().ConfigureAwait(continueOnCapturedContext: false);
        return await completionSource.Task.ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>Ensures that the SDK bridge is running.</summary>
    /// <returns>A task that completes after bridge initialization.</returns>
    private async Task EnsureBridgeAsync()
    {
        if (_process?.HasExited == false)
        {
            return;
        }

        _process = StartBridge(ResolveBridgeScript());
        _stdin = _process.StandardInput;
        _ = Task.Run(() => PumpStdout(_process.StandardOutput));
        _ = Task.Run(() => PumpStderr(_process.StandardError));
        await Task.Delay(BridgeStartupDelayMilliseconds).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>Resolves the SDK bridge script path.</summary>
    /// <returns>The bridge script path.</returns>
    private string ResolveBridgeScript()
    {
        string script = _settings.Current.BridgeScriptPath;
        script = string.IsNullOrWhiteSpace(script) ? LocalPaths.BundledBridgeScript : script;
        if (!File.Exists(script))
        {
            throw new FileNotFoundException("Codex SDK bridge script was not found.", script);
        }

        return script;
    }

    /// <summary>Starts the SDK bridge process.</summary>
    /// <param name="script">The bridge script path.</param>
    /// <returns>The started bridge process.</returns>
    private Process StartBridge(string script)
    {
        ProcessStartInfo startInfo = CodexEnvironmentService.CreateProcessStartInfo(
            CodexEnvironmentService.ResolveNodePath(_settings.Current.NodePath),
            Quote(script),
            LocalPaths.ExtensionInstallRoot,
            redirectStandardInput: true);
        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start node bridge.");
        }
        catch (Win32Exception innerException)
        {
            var message = new StringBuilder();
            _ = message.Append("Node.js executable was not found. Install Node.js LTS on Windows with ");
            _ = message.Append("`winget install OpenJS.NodeJS.LTS`, restart Visual Studio, or set the VSCodex Node Path setting ");
            _ = message.Append("to the full node.exe path. Current value: ");
            _ = message.Append(_settings.Current.NodePath);
            throw new InvalidOperationException(message.ToString(), innerException);
        }
    }

    /// <summary>Pumps and processes standard output.</summary>
    /// <param name="reader">The output reader.</param>
    private void PumpStdout(StreamReader reader)
    {
        string line;
        while ((line = reader.ReadLine()) is not null)
        {
            ProcessBridgeOutput(line);
        }
    }

    /// <summary>Processes one bridge output line.</summary>
    /// <param name="line">The output line.</param>
    private void ProcessBridgeOutput(string line)
    {
        try
        {
            JObject output = JObject.Parse(line);
            if (!TryCompletePendingRequest(output))
            {
                PublishBridgeEvent(output);
            }
        }
        catch (Exception ex)
        {
            _events.OnNext(new CodexEvent { Type = "bridge-output", Message = $"{line}\n{ex.Message}" });
        }
    }

    /// <summary>Completes a pending request when the output is a response.</summary>
    /// <param name="output">The bridge output.</param>
    /// <returns><see langword="true"/> when a pending request was completed.</returns>
    private bool TryCompletePendingRequest(JObject output)
    {
        string? id = output.Value<string>("id");
        if (id is null || !_pending.TryRemove(id, out TaskCompletionSource<JObject>? completionSource))
        {
            return false;
        }

        if (output.Value<string>("type") == "error")
        {
            _ = completionSource.TrySetException(new InvalidOperationException(output.Value<string>("message") ?? ToCompactJson(output)));
        }
        else
        {
            _ = completionSource.TrySetResult((JObject)(output["result"] ?? new JObject()));
        }

        return true;
    }

    /// <summary>Publishes a bridge event.</summary>
    /// <param name="output">The bridge output.</param>
    private void PublishBridgeEvent(JObject output)
    {
        _events.OnNext(new CodexEvent
        {
            Type = output.Value<string>("type") ?? "event",
            Message = output.Value<string>("message") ?? ToCompactJson(output),
            ThreadId = output.Value<string>(ThreadIdPropertyName),
            TurnId = output.Value<string>("turnId"),
            OperationId = output.Value<string>("operationId"),
            RawJson = ToCompactJson(output)
        });
    }

    /// <summary>Pumps standard error into events.</summary>
    /// <param name="reader">The error reader.</param>
    private void PumpStderr(StreamReader reader)
    {
        string line;
        while ((line = reader.ReadLine()) is not null)
        {
            _events.OnNext(new CodexEvent { Type = "stderr", Message = line });
        }
    }

    /// <summary>Attempts to stop the bridge process.</summary>
    private void TryKillProcess()
    {
        try
        {
            if (_process?.HasExited == false)
            {
                _process.Kill();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception || ex is ObjectDisposedException)
        {
            _events.OnNext(new CodexEvent { Type = "bridge-stop-error", Message = ex.Message });
        }
    }
}
