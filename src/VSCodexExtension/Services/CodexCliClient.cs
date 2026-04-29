using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using VSCodexExtension.Models;
namespace VSCodexExtension.Services
{
    public sealed class CodexCliClient : ICodexClient
    {
        private readonly ISettingsStore _settings; private readonly Subject<CodexEvent> _events = new Subject<CodexEvent>(); private Process? _active;
        public CodexCliClient(ISettingsStore settings) => _settings = settings;
        public IObservable<CodexEvent> Events => _events.AsObservable();
        public async Task<CodexRunResult> RunAsync(CodexRunRequest request)
        {
            var args = new StringBuilder(); args.Append("exec ");
            if (!string.IsNullOrWhiteSpace(request.Options.Model)) args.Append("--model ").Append(Quote(request.Options.Model)).Append(' ');
            if (!string.IsNullOrWhiteSpace(request.Options.Profile)) args.Append("--profile ").Append(Quote(request.Options.Profile)).Append(' ');
            args.Append("--approval-policy ").Append(ToCliApproval(request.Options.ApprovalPolicy)).Append(' ');
            args.Append("--sandbox ").Append(ToCliSandbox(request.Options.SandboxMode)).Append(' ');
            foreach (var image in request.Attachments.Where(x => x.Kind == "image")) args.Append("--image ").Append(Quote(image.Path)).Append(' ');
            args.Append(Quote(request.Prompt));
            var psi = new ProcessStartInfo { FileName = _settings.Current.CodexCliPath, Arguments = args.ToString(), WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkspaceRoot) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : request.WorkspaceRoot, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            var output = new StringBuilder(); _active = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Codex CLI.");
            _active.OutputDataReceived += (_, e) => { if (e.Data != null) { output.AppendLine(e.Data); _events.OnNext(new CodexEvent { Type = "stdout", Message = e.Data }); } };
            _active.ErrorDataReceived += (_, e) => { if (e.Data != null) _events.OnNext(new CodexEvent { Type = "stderr", Message = e.Data }); };
            _active.BeginOutputReadLine(); _active.BeginErrorReadLine(); await Task.Run(() => _active.WaitForExit()).ConfigureAwait(false);
            return new CodexRunResult { FinalResponse = output.ToString(), UsedFallback = true };
        }
        public void CancelActiveRun() { try { if (_active != null && !_active.HasExited) _active.Kill(); } catch { } }
        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
        private static string ToCliApproval(ApprovalPolicy p) => p == ApprovalPolicy.Never ? "never" : p == ApprovalPolicy.OnFailure ? "on-failure" : p == ApprovalPolicy.Untrusted ? "untrusted" : "on-request";
        private static string ToCliSandbox(SandboxMode s) => s == SandboxMode.ReadOnly ? "read-only" : s == SandboxMode.DangerFullAccess ? "danger-full-access" : "workspace-write";
    }
}
