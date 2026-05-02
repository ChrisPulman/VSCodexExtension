using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using VSCodex.Models;

namespace VSCodex.Services;

public sealed class CodexCliClient : ICodexClient
{
    private readonly ISettingsStore _settings; private readonly Subject<CodexEvent> _events = new Subject<CodexEvent>(); private Process? _active;
    public CodexCliClient(ISettingsStore settings) => _settings = settings;
    public IObservable<CodexEvent> Events => _events.AsObservable();
    public async Task<CodexRunResult> RunAsync(CodexRunRequest request)
    {
        var args = new StringBuilder(); args.Append("exec ");
        if (!string.IsNullOrWhiteSpace(request.Options.Model)) args.Append("--model ").Append(Quote(request.Options.Model)).Append(' ');
        if (ShouldPassProfile(request.Options.Profile)) args.Append("--profile ").Append(Quote(request.Options.Profile)).Append(' ');
        args.Append("--config ").Append(Quote("approval_policy=" + ToCliApproval(request.Options.ApprovalPolicy))).Append(' ');
        if (!string.IsNullOrWhiteSpace(request.Options.ReasoningEffort)) args.Append("--config ").Append(Quote("model_reasoning_effort=" + request.Options.ReasoningEffort)).Append(' ');
        args.Append("--sandbox ").Append(ToCliSandbox(request.Options.SandboxMode)).Append(' ');
        if (!string.IsNullOrWhiteSpace(request.WorkspaceRoot)) args.Append("--cd ").Append(Quote(request.WorkspaceRoot)).Append(' ');
        args.Append("--skip-git-repo-check ");
        foreach (var image in request.Attachments.Where(x => x.Kind == "image")) args.Append("--image ").Append(Quote(image.Path)).Append(' ');
        args.Append('-');
        var psi = CodexEnvironmentService.CreateProcessStartInfo(CodexEnvironmentService.ResolveCodexCliPath(_settings.Current.CodexCliPath), args.ToString(), string.IsNullOrWhiteSpace(request.WorkspaceRoot) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : request.WorkspaceRoot, redirectStandardInput: true);
        var output = new StringBuilder();
        var error = new StringBuilder();
        try
        {
            _active = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Codex CLI.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Codex CLI executable was not found. This is the optional VSCodex fallback transport. Install it on Windows with `npm install -g @openai/codex`, restart Visual Studio, or set the VSCodex Codex CLI Path setting to the full codex.cmd path. Current value: " + _settings.Current.CodexCliPath, ex);
        }
        _active.OutputDataReceived += (_, e) => { if (e.Data != null && !IsProcessTerminationNoise(e.Data)) { output.AppendLine(e.Data); _events.OnNext(new CodexEvent { Type = "stdout", Message = e.Data }); } };
        _active.ErrorDataReceived += (_, e) => { if (e.Data != null) { error.AppendLine(e.Data); _events.OnNext(new CodexEvent { Type = "stderr", Message = e.Data }); } };
        _active.BeginOutputReadLine(); _active.BeginErrorReadLine();
        Exception? stdinException = null;
        try
        {
            await _active.StandardInput.WriteAsync(request.Prompt ?? string.Empty).ConfigureAwait(false);
            _active.StandardInput.Close();
        }
        catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException)
        {
            stdinException = ex;
            try { _active.StandardInput.Close(); } catch { }
        }

        await Task.Run(() => _active.WaitForExit()).ConfigureAwait(false);
        if (_active.ExitCode != 0 || stdinException != null)
        {
            var message = "Codex CLI fallback exited with code " + _active.ExitCode + ": " + TrimForUi(error + Environment.NewLine + output);
            if (stdinException != null)
            {
                message += " Stdin write failed because codex exited early: " + stdinException.Message;
            }

            throw new InvalidOperationException(message, stdinException);
        }

        return new CodexRunResult { FinalResponse = output.ToString(), UsedFallback = true };
    }
    public void CancelActiveRun() { try { if (_active != null && !_active.HasExited) _active.Kill(); } catch { } }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private static bool ShouldPassProfile(string profile) => !string.IsNullOrWhiteSpace(profile) && !profile.Equals("default", StringComparison.OrdinalIgnoreCase);
    private static bool IsProcessTerminationNoise(string line) => line.StartsWith("SUCCESS: The process with PID ", StringComparison.OrdinalIgnoreCase) && line.EndsWith(" has been terminated.", StringComparison.OrdinalIgnoreCase);
    private static string ToCliApproval(ApprovalPolicy p) => p == ApprovalPolicy.Never ? "never" : p == ApprovalPolicy.OnFailure ? "on-failure" : p == ApprovalPolicy.Untrusted ? "untrusted" : "on-request";
    private static string ToCliSandbox(SandboxMode s) => s == SandboxMode.ReadOnly ? "read-only" : s == SandboxMode.DangerFullAccess ? "danger-full-access" : "workspace-write";
    private static string TrimForUi(object value)
    {
        var text = (value?.ToString() ?? string.Empty).Trim();
        return text.Length <= 1600 ? text : text.Substring(0, 1600) + "...";
    }
}
