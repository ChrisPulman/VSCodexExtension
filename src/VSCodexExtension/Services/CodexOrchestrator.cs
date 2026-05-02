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
            catch (Exception ex) when (request.Options.Transport == CodexTransportKind.SdkBridge)
            {
                var failover = BuildFailoverRequest(enriched);
                if (failover != null)
                {
                    _events.OnNext(new CodexEvent { Type = "fallback-model", Message = $"SDK bridge failed for {request.Options.Model}; retrying failover model {failover.Options.Model}: {ex.Message}" });
                    try { return await _sdk.RunAsync(failover).ConfigureAwait(false); }
                    catch (Exception failoverEx)
                    {
                        _events.OnNext(new CodexEvent { Type = "fallback", Message = "SDK failover model also failed; using CLI fallback: " + failoverEx.Message });
                        return await RunCliFallbackAsync(failover, failoverEx).ConfigureAwait(false);
                    }
                }

                _events.OnNext(new CodexEvent { Type = "fallback", Message = "SDK bridge failed; using CLI fallback: " + ex.Message });
                return await RunCliFallbackAsync(enriched, ex).ConfigureAwait(false);
            }
        }
        public void Cancel() { _sdk.CancelActiveRun(); _cli.CancelActiveRun(); }

        private async Task<CodexRunResult> RunCliFallbackAsync(CodexRunRequest request, Exception sdkException)
        {
            try
            {
                return await _cli.RunAsync(request).ConfigureAwait(false);
            }
            catch (Exception cliException)
            {
                throw new InvalidOperationException("VSCodex could not run because the Codex SDK bridge failed and the optional Codex CLI fallback is unavailable. Open the VSCodex Settings tab, run Check setup, and install @openai/codex-sdk on Windows with `npm install -g @openai/codex-sdk`. Optional CLI fallback install: `npm install -g @openai/codex`. SDK failure: " + sdkException.Message + " CLI failure: " + cliException.Message, cliException);
            }
        }

        private static CodexRunRequest? BuildFailoverRequest(CodexRunRequest request)
        {
            var failoverModel = request.Options.FailoverModel;
            if (string.IsNullOrWhiteSpace(failoverModel) || failoverModel.Equals(request.Options.Model, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var options = new CodexRunOptions
            {
                Model = failoverModel,
                FailoverModel = failoverModel,
                ReasoningEffort = request.Options.ReasoningEffort,
                Verbosity = request.Options.Verbosity,
                ServiceTier = request.Options.ServiceTier,
                Profile = request.Options.Profile,
                ApprovalPolicy = request.Options.ApprovalPolicy,
                SandboxMode = request.Options.SandboxMode,
                Mode = request.Options.Mode,
                Transport = request.Options.Transport,
                IncludeWorkspaceContext = request.Options.IncludeWorkspaceContext,
                IncludeMemory = request.Options.IncludeMemory,
                IncludeSkills = request.Options.IncludeSkills,
                IncludeMcpServers = request.Options.IncludeMcpServers,
                UseMultiAgentOrchestration = request.Options.UseMultiAgentOrchestration,
                MaxAgentConcurrency = request.Options.MaxAgentConcurrency,
                AgentStrategy = request.Options.AgentStrategy,
                OrchestrationModel = request.Options.OrchestrationModel,
                BudgetDrivenModelSelection = request.Options.BudgetDrivenModelSelection,
                BudgetModel = request.Options.BudgetModel
            };

            return new CodexRunRequest
            {
                Prompt = request.Prompt,
                ThreadId = request.ThreadId,
                WorkspaceRoot = request.WorkspaceRoot,
                Options = options,
                Attachments = request.Attachments,
                Skills = request.Skills,
                Memories = request.Memories,
                McpServers = request.McpServers,
                WorkspaceFiles = request.WorkspaceFiles,
                AgentRoles = request.AgentRoles
            };
        }
    }
}
