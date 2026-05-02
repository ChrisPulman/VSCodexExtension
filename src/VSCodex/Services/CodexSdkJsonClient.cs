using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

public interface ICodexClient { IObservable<CodexEvent> Events { get; } Task<CodexRunResult> RunAsync(CodexRunRequest request); void CancelActiveRun(); }
public sealed class CodexSdkJsonClient : ICodexClient, IDisposable
{
    private readonly ISettingsStore _settings;
    private readonly Subject<CodexEvent> _events = new Subject<CodexEvent>();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _pending = new ConcurrentDictionary<string, TaskCompletionSource<JObject>>();
    private Process? _process; private StreamWriter? _stdin;
    public CodexSdkJsonClient(ISettingsStore settings) => _settings = settings;
    public IObservable<CodexEvent> Events => _events.AsObservable();
    public async Task<CodexRunResult> RunAsync(CodexRunRequest request)
    {
        await EnsureBridgeAsync().ConfigureAwait(false);
        var payload = new JObject
        {
            ["command"] = string.IsNullOrWhiteSpace(request.ThreadId) ? "startAndRun" : "resumeAndRun",
            ["threadId"] = request.ThreadId,
            ["prompt"] = request.Prompt,
            ["model"] = request.Options.Model,
            ["reasoningEffort"] = request.Options.ReasoningEffort,
            ["verbosity"] = request.Options.Verbosity,
            ["serviceTier"] = request.Options.ServiceTier,
            ["profile"] = request.Options.Profile,
            ["approvalPolicy"] = request.Options.ApprovalPolicy.ToString(),
            ["sandboxMode"] = request.Options.SandboxMode.ToString(),
            ["workspaceRoot"] = request.WorkspaceRoot,
            ["images"] = JArray.FromObject(request.Attachments)
        };
        var response = await SendAsync(payload).ConfigureAwait(false);
        return new CodexRunResult { ThreadId = response.Value<string>("threadId"), FinalResponse = response.Value<string>("finalResponse") ?? ToCompactJson(response), RawJson = ToCompactJson(response) };
    }
    public void CancelActiveRun() { _ = SendAsync(new JObject { ["command"] = "cancel" }); }
    private async Task<JObject> SendAsync(JObject payload)
    {
        if (_stdin == null) throw new InvalidOperationException("Codex SDK bridge is not running.");
        var id = Guid.NewGuid().ToString("N"); payload["id"] = id;
        var tcs = new TaskCompletionSource<JObject>(); _pending[id] = tcs;
        await _stdin.WriteLineAsync(ToCompactJson(payload)).ConfigureAwait(false);
        await _stdin.FlushAsync().ConfigureAwait(false);
        return await tcs.Task.ConfigureAwait(false);
    }
    private async Task EnsureBridgeAsync()
    {
        if (_process != null && !_process.HasExited) return;
        var script = _settings.Current.BridgeScriptPath; if (string.IsNullOrWhiteSpace(script)) script = LocalPaths.BundledBridgeScript;
        if (!File.Exists(script)) throw new FileNotFoundException("Codex SDK bridge script was not found.", script);
        var psi = CodexEnvironmentService.CreateProcessStartInfo(CodexEnvironmentService.ResolveNodePath(_settings.Current.NodePath), Quote(script), LocalPaths.ExtensionInstallRoot, redirectStandardInput: true);
        try
        {
            _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start node bridge.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Node.js executable was not found. Install Node.js LTS on Windows with `winget install OpenJS.NodeJS.LTS`, restart Visual Studio, or set the VSCodex Node Path setting to the full node.exe path. Current value: " + _settings.Current.NodePath, ex);
        }

        _stdin = _process.StandardInput;
        _ = Task.Run(() => PumpStdout(_process.StandardOutput)); _ = Task.Run(() => PumpStderr(_process.StandardError)); await Task.Delay(150).ConfigureAwait(false);
    }
    private void PumpStdout(StreamReader reader)
    {
        string? line; while ((line = reader.ReadLine()) != null)
        {
            try
            {
                var obj = JObject.Parse(line); var id = obj.Value<string>("id"); var type = obj.Value<string>("type") ?? "event";
                if (type == "response" && id != null && _pending.TryRemove(id, out var tcs)) tcs.TrySetResult((JObject)(obj["result"] ?? new JObject()));
                else if (type == "error" && id != null && _pending.TryRemove(id, out var errorTcs)) errorTcs.TrySetException(new InvalidOperationException(obj.Value<string>("message") ?? ToCompactJson(obj)));
                else _events.OnNext(new CodexEvent { Type = type, Message = obj.Value<string>("message") ?? ToCompactJson(obj), ThreadId = obj.Value<string>("threadId"), RawJson = ToCompactJson(obj) });
            }
            catch (Exception ex) { _events.OnNext(new CodexEvent { Type = "bridge-output", Message = line + "\n" + ex.Message }); }
        }
    }
    private void PumpStderr(StreamReader reader) { string? line; while ((line = reader.ReadLine()) != null) _events.OnNext(new CodexEvent { Type = "stderr", Message = line }); }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private static string ToCompactJson(JToken token) => JsonConvert.SerializeObject(token);
    public void Dispose() { try { _process?.Kill(); } catch { } _events.Dispose(); }
}
