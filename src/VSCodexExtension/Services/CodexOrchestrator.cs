using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using VSCodexExtension.Models;
namespace VSCodexExtension.Services
{
    public interface ICodexOrchestrator { IObservable<CodexEvent> Events { get; } Task<CodexRunResult> RunAsync(CodexRunRequest request); void Cancel(); }
    public sealed class CodexOrchestrator : ICodexOrchestrator
    {
        private readonly PromptBuilder _promptBuilder = new PromptBuilder(); private readonly ICodexClient _sdk; private readonly ICodexClient _cli; private readonly Subject<CodexEvent> _events = new Subject<CodexEvent>();
        public CodexOrchestrator(ICodexClient sdk, ICodexClient cli) { _sdk = sdk; _cli = cli; _sdk.Events.Merge(_cli.Events).Subscribe(_events); }
        public IObservable<CodexEvent> Events => _events.AsObservable();
        public async Task<CodexRunResult> RunAsync(CodexRunRequest request)
        {
            var enriched = new CodexRunRequest { Prompt = _promptBuilder.Build(request), ThreadId = request.ThreadId, WorkspaceRoot = request.WorkspaceRoot, Options = request.Options, Attachments = request.Attachments, Skills = request.Skills, Memories = request.Memories, McpServers = request.McpServers, WorkspaceFiles = request.WorkspaceFiles, AgentRoles = request.AgentRoles };
            try { return await (request.Options.Transport == CodexTransportKind.CliFallback ? _cli : _sdk).RunAsync(enriched).ConfigureAwait(false); }
            catch (Exception ex) when (request.Options.Transport == CodexTransportKind.SdkBridge) { _events.OnNext(new CodexEvent { Type = "fallback", Message = "SDK bridge failed; using CLI fallback: " + ex.Message }); return await _cli.RunAsync(enriched).ConfigureAwait(false); }
        }
        public void Cancel() { _sdk.CancelActiveRun(); _cli.CancelActiveRun(); }
    }
}
