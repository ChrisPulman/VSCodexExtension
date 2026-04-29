using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VSCodexExtension.Models;
namespace VSCodexExtension.Services
{
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
            return new CodexRunResult { ThreadId = response.Value<string>("threadId"), FinalResponse = response.Value<string>("finalResponse") ?? response.ToString(Formatting.None), RawJson = response.ToString(Formatting.None) };
        }
        public void CancelActiveRun() { _ = SendAsync(new JObject { ["command"] = "cancel" }); }
        private Task<JObject> SendAsync(JObject payload)
        {
            if (_stdin == null) throw new InvalidOperationException("Codex SDK bridge is not running.");
            var id = Guid.NewGuid().ToString("N"); payload["id"] = id;
            var tcs = new TaskCompletionSource<JObject>(); _pending[id] = tcs;
            _stdin.WriteLine(payload.ToString(Formatting.None)); _stdin.Flush(); return tcs.Task;
        }
        private async Task EnsureBridgeAsync()
        {
            if (_process != null && !_process.HasExited) return;
            var script = _settings.Current.BridgeScriptPath; if (string.IsNullOrWhiteSpace(script)) script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "codex-bridge.mjs");
            if (!File.Exists(script)) throw new FileNotFoundException("Codex SDK bridge script was not found.", script);
            var psi = new ProcessStartInfo { FileName = _settings.Current.NodePath, Arguments = Quote(script), UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory };
            _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start node bridge."); _stdin = _process.StandardInput;
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
                    else if (type == "error" && id != null && _pending.TryRemove(id, out var errorTcs)) errorTcs.TrySetException(new InvalidOperationException(obj.Value<string>("message") ?? obj.ToString(Formatting.None)));
                    else _events.OnNext(new CodexEvent { Type = type, Message = obj.Value<string>("message") ?? obj.ToString(Formatting.None), ThreadId = obj.Value<string>("threadId"), RawJson = obj.ToString(Formatting.None) });
                }
                catch (Exception ex) { _events.OnNext(new CodexEvent { Type = "bridge-output", Message = line + "\n" + ex.Message }); }
            }
        }
        private void PumpStderr(StreamReader reader) { string? line; while ((line = reader.ReadLine()) != null) _events.OnNext(new CodexEvent { Type = "stderr", Message = line }); }
        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
        public void Dispose() { try { _process?.Kill(); } catch { } _events.Dispose(); }
    }
}
