// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VSCodex.Core.Models;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Coordinates SDK and CLI Codex transports.</summary>
public sealed class CodexOrchestrator : ICodexOrchestrator
{
    /// <summary>Stores the prompt builder.</summary>
    private readonly PromptBuilder _promptBuilder = new();

    /// <summary>Stores the SDK client.</summary>
    private readonly ICodexClient _sdk;

    /// <summary>Stores the CLI client.</summary>
    private readonly ICodexClient _cli;

    /// <summary>Publishes transport events.</summary>
    private readonly Subject<CodexEvent> _events = new();

    /// <summary>Initializes a new instance of the <see cref="CodexOrchestrator"/> class.</summary>
    /// <param name="sdk">The SDK client.</param>
    /// <param name="cli">The CLI client.</param>
    public CodexOrchestrator(ICodexClient sdk, ICodexClient cli)
    {
        _sdk = sdk;
        _cli = cli;
        _ = _sdk.Events.Merge(_cli.Events).Subscribe(_events);
    }

    /// <summary>Gets transport events.</summary>
    public IObservable<CodexEvent> Events => _events.AsObservable();

    /// <summary>Runs a Codex request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task whose result contains the run result.</returns>
    public async Task<CodexRunResult> RunAsync(CodexRunRequest request)
    {
        CodexRunRequest enrichedRequest = EnrichRequest(request);
        if (request.Options.Transport == CodexTransportKind.CliFallback)
        {
            return await _cli.RunAsync(enrichedRequest).ConfigureAwait(continueOnCapturedContext: false);
        }

        try
        {
            return await _sdk.RunAsync(enrichedRequest).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception sdkException)
        {
            return await RunWithFallbackAsync(enrichedRequest, sdkException).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    /// <summary>Gets rate limits from the preferred transport.</summary>
    /// <returns>A task whose result contains the rate limits.</returns>
    public async Task<JObject?> GetRateLimitsAsync()
    {
        try
        {
            return await _sdk.GetRateLimitsAsync().ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception)
        {
            return await _cli.GetRateLimitsAsync().ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    /// <summary>Steers an active SDK turn.</summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="prompt">The steering prompt.</param>
    /// <returns>A task that completes after steering.</returns>
    public async Task SteerAsync(string threadId, string prompt)
    {
        _ = await _sdk.SteerAsync(threadId, prompt).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>Interrupts an active SDK turn.</summary>
    /// <param name="threadId">The optional thread identifier.</param>
    /// <returns>A task that completes after interruption.</returns>
    public async Task InterruptAsync(string? threadId)
    {
        _ = await _sdk.InterruptAsync(threadId).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>Responds to an SDK server request.</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="approve">Whether to approve the request.</param>
    /// <returns>A task that completes after the response is sent.</returns>
    public async Task RespondToServerRequestAsync(string requestId, bool approve)
    {
        var response = new JObject { ["decision"] = approve ? "accept" : "decline" };
        _ = await _sdk.RespondToServerRequestAsync(requestId, response)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>Cancels active SDK and CLI work without blocking.</summary>
    public void Cancel()
    {
        _sdk.CancelActiveRun();
        _cli.CancelActiveRun();
    }

    /// <summary>Builds a failover request when a distinct model is configured.</summary>
    /// <param name="request">The source request.</param>
    /// <returns>The failover request, or <see langword="null"/>.</returns>
    private static CodexRunRequest? BuildFailoverRequest(CodexRunRequest request)
    {
        string failoverModel = request.Options.FailoverModel;
        bool hasDistinctFailover = !string.IsNullOrWhiteSpace(failoverModel)
            && !failoverModel.Equals(request.Options.Model, StringComparison.OrdinalIgnoreCase);
        return hasDistinctFailover ? CloneRequest(request, CreateFailoverOptions(request.Options, failoverModel)) : null;
    }

    /// <summary>Creates options for the configured failover model.</summary>
    /// <param name="source">The source options.</param>
    /// <param name="failoverModel">The failover model.</param>
    /// <returns>The failover options.</returns>
    private static CodexRunOptions CreateFailoverOptions(CodexRunOptions source, string failoverModel)
    {
        return new CodexRunOptions
        {
            Model = failoverModel,
            FailoverModel = failoverModel,
            ReasoningEffort = CodexModelCatalog.ResolveReasoningEffort(failoverModel, source.ReasoningEffort),
            Verbosity = source.Verbosity,
            ServiceTier = source.ServiceTier,
            Profile = source.Profile,
            ApprovalPolicy = source.ApprovalPolicy,
            SandboxMode = source.SandboxMode,
            Mode = source.Mode,
            Transport = source.Transport,
            IncludeWorkspaceContext = source.IncludeWorkspaceContext,
            IncludeMemory = source.IncludeMemory,
            IncludeSkills = source.IncludeSkills,
            IncludeMcpServers = source.IncludeMcpServers,
            UseMultiAgentOrchestration = source.UseMultiAgentOrchestration,
            MaxAgentConcurrency = source.MaxAgentConcurrency,
            AgentStrategy = source.AgentStrategy,
            OrchestrationModel = source.OrchestrationModel,
            BudgetDrivenModelSelection = source.BudgetDrivenModelSelection,
            BudgetModel = source.BudgetModel
        };
    }

    /// <summary>Clones a request with different options.</summary>
    /// <param name="source">The source request.</param>
    /// <param name="options">The replacement options.</param>
    /// <param name="prompt">The optional replacement prompt.</param>
    /// <returns>The cloned request.</returns>
    private static CodexRunRequest CloneRequest(CodexRunRequest source, CodexRunOptions options, string? prompt = null)
    {
        return new CodexRunRequest
        {
            OperationId = source.OperationId,
            Prompt = prompt ?? source.Prompt,
            ThreadId = source.ThreadId,
            WorkspaceRoot = source.WorkspaceRoot,
            WorkspaceName = source.WorkspaceName,
            WorkspaceSolutionPath = source.WorkspaceSolutionPath,
            WorkspaceMemoryRoot = source.WorkspaceMemoryRoot,
            ReactiveMemoryContext = source.ReactiveMemoryContext,
            WorkspaceIdentity = source.WorkspaceIdentity,
            Options = options,
            Attachments = source.Attachments,
            Skills = source.Skills,
            Memories = source.Memories,
            McpServers = source.McpServers,
            WorkspaceFiles = source.WorkspaceFiles,
            AgentRoles = source.AgentRoles
        };
    }

    /// <summary>Enriches a request with its composed prompt.</summary>
    /// <param name="request">The source request.</param>
    /// <returns>The enriched request.</returns>
    private CodexRunRequest EnrichRequest(CodexRunRequest request)
    {
        return CloneRequest(request, request.Options, _promptBuilder.Build(request));
    }

    /// <summary>Runs the SDK fallback sequence.</summary>
    /// <param name="request">The enriched request.</param>
    /// <param name="sdkException">The SDK failure.</param>
    /// <returns>A task whose result contains the fallback result.</returns>
    private async Task<CodexRunResult> RunWithFallbackAsync(CodexRunRequest request, Exception sdkException)
    {
        CodexRunRequest? failoverRequest = BuildFailoverRequest(request);
        if (failoverRequest is null)
        {
            PublishFallbackEvent("fallback", $"SDK bridge failed; using CLI fallback: {sdkException.Message}");
            return await RunCliFallbackAsync(request, sdkException).ConfigureAwait(continueOnCapturedContext: false);
        }

        PublishFallbackEvent(
            "fallback-model",
            $"SDK bridge failed for {request.Options.Model}; retrying failover model {failoverRequest.Options.Model}: {sdkException.Message}");
        try
        {
            return await _sdk.RunAsync(failoverRequest).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception failoverException)
        {
            PublishFallbackEvent("fallback", $"SDK failover model also failed; using CLI fallback: {failoverException.Message}");
            return await RunCliFallbackAsync(failoverRequest, failoverException).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    /// <summary>Publishes an orchestration fallback event.</summary>
    /// <param name="type">The event type.</param>
    /// <param name="message">The event message.</param>
    private void PublishFallbackEvent(string type, string message)
    {
        _events.OnNext(new CodexEvent { Type = type, Message = message });
    }

    /// <summary>Runs the CLI fallback and preserves both transport errors.</summary>
    /// <param name="request">The request.</param>
    /// <param name="sdkException">The SDK failure.</param>
    /// <returns>A task whose result contains the CLI result.</returns>
    private async Task<CodexRunResult> RunCliFallbackAsync(CodexRunRequest request, Exception sdkException)
    {
        try
        {
            return await _cli.RunAsync(request).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception cliException)
        {
            throw new InvalidOperationException(CreateTransportFailureMessage(sdkException, cliException), cliException);
        }
    }

    /// <summary>Creates a combined SDK and CLI failure message.</summary>
    /// <param name="sdkException">The SDK failure.</param>
    /// <param name="cliException">The CLI failure.</param>
    /// <returns>The combined failure message.</returns>
    private string CreateTransportFailureMessage(Exception sdkException, Exception cliException)
    {
        var message = new StringBuilder();
        _ = message.Append("VSCodex could not run because the Codex SDK bridge failed and the optional Codex CLI fallback could not ");
        _ = message.Append("complete the request. Open the VSCodex Settings tab, run Check setup, verify the Codex SDK with ");
        _ = message.Append("`npm install -g @openai/codex-sdk`, and only set a Codex profile when it exists in ");
        _ = message.Append("%USERPROFILE%\\.codex\\config.toml. Optional CLI fallback install: `npm install -g @openai/codex`. ");
        _ = message.Append($"SDK failure: {sdkException.Message} CLI failure: {cliException.Message}");
        return message.ToString();
    }
}
