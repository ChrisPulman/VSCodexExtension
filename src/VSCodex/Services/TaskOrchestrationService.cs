// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VSCodex.Core.Models;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the task Orchestration Service implementation.</summary>
/// <param name="settings">The settings store.</param>
/// <param name="codex">The Codex orchestrator.</param>
public sealed class TaskOrchestrationService(ISettingsStore settings, ICodexOrchestrator codex) : ITaskOrchestrationService
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric4000 = 4000;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric8 = 8;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric80 = 80;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric90 = 90;

    /// <summary>Named string used by this type.</summary>
    private const string BuilderText = "Builder";

    /// <summary>Stores the settings.</summary>
    private readonly ISettingsStore _settings = settings;

    /// <summary>Stores the codex.</summary>
    private readonly ICodexOrchestrator _codex = codex;

    /// <summary>Matches an explicit list item.</summary>
    private readonly Regex _explicitListItemRegex = new(@"^(?:[-*•]|\d+[.)])\s+(?<item>.+)$");

    /// <summary>Splits a prompt into natural-language sections.</summary>
    private readonly Regex _sectionSeparatorRegex = new(@"\b(?:then|next|after that|finally|also|and then)\b", RegexOptions.IgnoreCase);

    /// <summary>Matches a word.</summary>
    private readonly Regex _wordRegex = new(@"\w+");

    /// <summary>Stores the events.</summary>
    private readonly Subject<OrchestrationEvent> _events = new();

    /// <summary>Stores the cancellation.</summary>
    private CancellationTokenSource? _cancellation;

    /// <summary>Gets the events.</summary>
    public IObservable<OrchestrationEvent> Events => _events.AsObservable();

    /// <summary>Gets the current plan.</summary>
    public OrchestrationRunPlan? CurrentPlan { get; private set; }

    /// <summary>Runs the operation.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    public async Task<CodexRunResult> RunAsync(CodexRunRequest request)
    {
        _cancellation = new();
        var token = _cancellation.Token;
        var plan = BuildPlan(request);
        CurrentPlan = plan;
        Emit("plan-created", $"Created orchestration plan with {plan.Sections.Count} section(s) and {plan.Agents.Count} agent(s).", plan);

        var results = new List<OrchestrationTaskSection>();
        try
        {
            foreach (var section in plan.Sections)
            {
                token.ThrowIfCancellationRequested();
                section.Status = OrchestrationSectionStatus.Running;
                Emit("section-started", $"{section.AssignedAgent} started: {section.Title}", plan, section);

                var sectionRequest = CloneForSection(request, plan, section, results);
                var sectionResult = await _codex.RunAsync(sectionRequest).ConfigureAwait(false);

                section.Result = sectionResult.FinalResponse;
                section.Status = OrchestrationSectionStatus.Completed;
                results.Add(section);
                Emit("section-completed", $"{section.AssignedAgent} completed: {section.Title}", plan, section);
            }

            var final = await RunFinalSynthesisAsync(request, results).ConfigureAwait(false);
            Emit("plan-completed", "Multi-agent orchestration completed.", plan);
            return final;
        }
        catch (OperationCanceledException)
        {
            foreach (var section in plan.Sections.Where(x => x.Status == OrchestrationSectionStatus.Pending || x.Status == OrchestrationSectionStatus.Running))
            {
                section.Status = OrchestrationSectionStatus.Cancelled;
            }

            Emit("plan-cancelled", "Multi-agent orchestration cancelled.", plan);
            return new CodexRunResult { FinalResponse = RenderPlanSummary(plan), UsedFallback = false };
        }
        catch (Exception ex)
        {
            var running = plan.Sections.FirstOrDefault(x => x.Status == OrchestrationSectionStatus.Running);
            if (running is not null)
            {
                running.Status = OrchestrationSectionStatus.Failed;
            }

            Emit("plan-failed", ex.Message, plan, running);
            throw;
        }
    }

    /// <summary>Determines whether cancel.</summary>
    public void Cancel()
    {
        _cancellation?.Cancel();
        _codex.Cancel();
    }

    /// <summary>Builds plan.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The build Plan result.</returns>
    private OrchestrationRunPlan BuildPlan(CodexRunRequest request)
    {
        var settings = _settings.Current;
        var configuredAgents = request.AgentRoles?.Count > 0
            ? request.AgentRoles
            : (IEnumerable<AgentRoleDefinition>)(settings.AgentRoles ?? new List<AgentRoleDefinition>());
        var agents = configuredAgents.Where(x => x.IsEnabled).ToList();
        if (agents.Count == 0)
        {
            agents = [
                new AgentRoleDefinition { Name = "Planner", Role = "Planning", Instructions = "Plan the work." },
                new AgentRoleDefinition { Name = BuilderText, Role = "Implementation", Instructions = "Implement assigned work." },
                new AgentRoleDefinition { Name = "Reviewer", Role = "Review", Instructions = "Review and validate the work." }
            ];
        }

        var sections = SplitIntoSections(request.Prompt, agents).ToList();
        if (sections.Count == 0)
        {
            sections.Add(new OrchestrationTaskSection
            {
                Index = 1,
                Title = "Handle request",
                Description = request.Prompt,
                AssignedAgent = PickAgent(agents, BuilderText, 0).Name
            });
        }

        var plan = new OrchestrationRunPlan
        {
            Goal = request.Prompt,
            Strategy = request.Options.AgentStrategy
        };
        plan.Agents.AddRange(agents);
        plan.Sections.AddRange(sections);
        return plan;
    }

    /// <summary>Performs the split Into Sections operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="agents">The agents.</param>
    /// <returns>The split Into Sections result.</returns>
    private IEnumerable<OrchestrationTaskSection> SplitIntoSections(string prompt, IReadOnlyList<AgentRoleDefinition> agents)
    {
        var normalized = prompt ?? string.Empty;
        var candidates = ExtractExplicitListItems(normalized).ToList();
        if (candidates.Count < 2)
        {
            candidates = ExtractSentenceSections(normalized).ToList();
        }

        if (candidates.Count < 2 && LooksLikeLargeTask(normalized))
        {
            candidates = [
                "Analyze requirements, repository context, risks, and acceptance criteria.",
                "Design the implementation approach and identify files/services/UI surfaces to change.",
                "Implement the requested capability in focused sections.",
                "Review the implementation for correctness, safety, and integration issues.",
                "Define and run the most relevant validation checks, then summarize evidence."
            ];
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var title = candidates[i].Trim();
            if (title.Length > Numeric90)
            {
                title = $"{title.Substring(0, Numeric90).TrimEnd()}...";
            }

            yield return new OrchestrationTaskSection
            {
                Index = i + 1,
                Title = title,
                Description = candidates[i].Trim(),
                AssignedAgent = PickAgentForIndex(agents, i, candidates.Count).Name,
                DependsOnSectionId = i == 0 ? string.Empty : "previous"
            };
        }
    }

    /// <summary>Performs the extract Explicit List Items operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The extract Explicit List Items result.</returns>
    private IEnumerable<string> ExtractExplicitListItems(string prompt)
    {
        foreach (var raw in (prompt ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            var match = _explicitListItemRegex.Match(line);
            if (match.Success && match.Groups["item"].Value.Trim().Length > Numeric8)
            {
                yield return match.Groups["item"].Value.Trim();
            }
        }
    }

    /// <summary>Performs the extract Sentence Sections operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The extract Sentence Sections result.</returns>
    private IEnumerable<string> ExtractSentenceSections(string prompt)
    {
        var parts = _sectionSeparatorRegex.Split(prompt ?? string.Empty)
            .Select(x => x.Trim(' ', '.', ',', ';', ':'))
            .Where(x => x.Length > 24)
            .Take(Numeric8)
            .ToList();
        return parts.Count >= 2 ? parts : Enumerable.Empty<string>();
    }

    /// <summary>Performs the looks Like Large Task operation.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <returns><see langword="true"/> when looks Like Large Task succeeds; otherwise, <see langword="false"/>.</returns>
    private bool LooksLikeLargeTask(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return false;
        }

        var words = _wordRegex.Matches(prompt).Count;
        var keywords = new[] { "multiple", "large", "extensive", "implement", "create", "build", "refactor", "test", "plan", "orchestration", "agents", "steps" };
        return words > Numeric80 || keywords.Count(k => prompt.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) >= 2;
    }

    /// <summary>Performs the pick Agent For Index operation.</summary>
    /// <param name="agents">The agents.</param>
    /// <param name="index">The index.</param>
    /// <param name="count">The count.</param>
    /// <returns>The pick Agent For Index result.</returns>
    private AgentRoleDefinition PickAgentForIndex(IReadOnlyList<AgentRoleDefinition> agents, int index, int count)
    {
        if (index == 0)
        {
            return PickAgent(agents, "Planner", index);
        }

        return index == count - 1 ? PickAgent(agents, "Reviewer", index) : PickAgent(agents, BuilderText, index);
    }

    /// <summary>Performs the pick Agent operation.</summary>
    /// <param name="agents">The agents.</param>
    /// <param name="preferredName">The preferred Name.</param>
    /// <param name="fallbackIndex">The fallback Index.</param>
    /// <returns>The pick Agent result.</returns>
    private AgentRoleDefinition PickAgent(IReadOnlyList<AgentRoleDefinition> agents, string preferredName, int fallbackIndex)
    {
        return agents.FirstOrDefault(x => x.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase))
            ?? agents.FirstOrDefault(x => x.Role.IndexOf(preferredName, StringComparison.OrdinalIgnoreCase) >= 0)
            ?? agents[Math.Abs(fallbackIndex) % agents.Count];
    }

    /// <summary>Performs the clone For Section operation.</summary>
    /// <param name="request">The request.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="section">The section.</param>
    /// <param name="completed">The completed.</param>
    /// <returns>The clone For Section result.</returns>
    private CodexRunRequest CloneForSection(CodexRunRequest request, OrchestrationRunPlan plan, OrchestrationTaskSection section, IReadOnlyList<OrchestrationTaskSection> completed)
    {
        var agent = plan.Agents.FirstOrDefault(x => x.Name.Equals(section.AssignedAgent, StringComparison.OrdinalIgnoreCase));
        var sb = new StringBuilder();
        _ = sb.AppendLine($"You are the {section.AssignedAgent} agent in a multi-agent Codex orchestration run.");
        if (agent is not null)
        {
            _ = sb.AppendLine($"Role: {agent.Role}");
            _ = sb.AppendLine($"Agent instructions: {agent.Instructions}");
        }

        _ = sb.AppendLine();
        _ = sb.AppendLine("## Overall goal");
        _ = sb.AppendLine(plan.Goal);
        _ = sb.AppendLine();
        _ = sb.AppendLine("## Current section");
        _ = sb.AppendLine($"{section.Index}. {section.Title}");
        _ = sb.AppendLine(section.Description);
        _ = sb.AppendLine();
        if (completed.Count > 0)
        {
            _ = sb.AppendLine("## Completed prior sections");
            foreach (var item in completed)
            {
                _ = sb.AppendLine($"### {item.Index}. {item.Title} ({item.AssignedAgent})");
                _ = sb.AppendLine(item.Result.Length > Numeric4000 ? item.Result.Substring(0, Numeric4000) : item.Result);
            }
        }

        _ = sb.AppendLine("Return only the output for this section. Be explicit about files changed, validation performed, and follow-up risks.");

        var options = CopyOptions(request.Options);
        if (agent is not null && !string.IsNullOrWhiteSpace(agent.Model))
        {
            options.Model = agent.Model;
        }
        else if (request.Options.BudgetDrivenModelSelection && !string.IsNullOrWhiteSpace(request.Options.BudgetModel))
        {
            options.Model = request.Options.BudgetModel;
        }

        options.ReasoningEffort = CodexModelCatalog.ResolveReasoningEffort(options.Model, options.ReasoningEffort);
        options.UseMultiAgentOrchestration = false;

        return new CodexRunRequest
        {
            Prompt = sb.ToString(),
            ThreadId = null,
            WorkspaceRoot = request.WorkspaceRoot,
            WorkspaceName = request.WorkspaceName,
            WorkspaceSolutionPath = request.WorkspaceSolutionPath,
            WorkspaceMemoryRoot = request.WorkspaceMemoryRoot,
            ReactiveMemoryContext = request.ReactiveMemoryContext,
            WorkspaceIdentity = request.WorkspaceIdentity,
            Options = options,
            Attachments = request.Attachments,
            Skills = request.Skills,
            Memories = request.Memories,
            McpServers = request.McpServers,
            WorkspaceFiles = request.WorkspaceFiles,
            AgentRoles = request.AgentRoles
        };
    }

    /// <summary>Runs final Synthesis.</summary>
    /// <param name="request">The request.</param>
    /// <param name="sections">The sections.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    private async Task<CodexRunResult> RunFinalSynthesisAsync(CodexRunRequest request, IReadOnlyList<OrchestrationTaskSection> sections)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("You are the final orchestration coordinator. Consolidate the multi-agent outputs into one final response.");
        _ = sb.AppendLine("Do not re-run implementation unless a critical gap is obvious. Summarize completed sections, changed files, validation, and residual risks.");
        _ = sb.AppendLine();
        _ = sb.AppendLine("## Original user request");
        _ = sb.AppendLine(request.Prompt);
        _ = sb.AppendLine();
        _ = sb.AppendLine("## Section outputs");
        foreach (var section in sections)
        {
            _ = sb.AppendLine($"### {section.Index}. {section.Title} — {section.AssignedAgent} — {section.Status}");
            _ = sb.AppendLine(section.Result);
            _ = sb.AppendLine();
        }

        var finalRequest = new CodexRunRequest
        {
            Prompt = sb.ToString(),
            ThreadId = request.ThreadId,
            WorkspaceRoot = request.WorkspaceRoot,
            WorkspaceName = request.WorkspaceName,
            WorkspaceSolutionPath = request.WorkspaceSolutionPath,
            WorkspaceMemoryRoot = request.WorkspaceMemoryRoot,
            ReactiveMemoryContext = request.ReactiveMemoryContext,
            WorkspaceIdentity = request.WorkspaceIdentity,
            Options = CopyOptions(request.Options),
            Attachments = request.Attachments,
            Skills = request.Skills,
            Memories = request.Memories,
            McpServers = request.McpServers,
            WorkspaceFiles = request.WorkspaceFiles,
            AgentRoles = request.AgentRoles
        };
        finalRequest.Options.UseMultiAgentOrchestration = false;
        if (!string.IsNullOrWhiteSpace(request.Options.OrchestrationModel))
        {
            finalRequest.Options.Model = request.Options.OrchestrationModel;
        }

        if (request.Options.BudgetDrivenModelSelection && !string.IsNullOrWhiteSpace(request.Options.BudgetModel))
        {
            finalRequest.Options.Model = request.Options.BudgetModel;
        }

        finalRequest.Options.ReasoningEffort = CodexModelCatalog.ResolveReasoningEffort(finalRequest.Options.Model, finalRequest.Options.ReasoningEffort);
        return await _codex.RunAsync(finalRequest).ConfigureAwait(false);
    }

    /// <summary>Copies options.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The copy Options result.</returns>
    private CodexRunOptions CopyOptions(CodexRunOptions source)
    {
        return new CodexRunOptions
        {
            Model = source.Model,
            FailoverModel = source.FailoverModel,
            ReasoningEffort = source.ReasoningEffort,
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

    /// <summary>Renders plan Summary.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The render Plan Summary result.</returns>
    private string RenderPlanSummary(OrchestrationRunPlan plan)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine($"# Orchestration {plan.Id}");
        _ = sb.AppendLine($"Goal: {plan.Goal}");
        foreach (var section in plan.Sections)
        {
            _ = sb.AppendLine($"- {section.Index}. {section.Title} [{section.AssignedAgent}] — {section.Status}");
        }

        return sb.ToString();
    }

    /// <summary>Performs the emit operation.</summary>
    /// <param name="type">The type.</param>
    /// <param name="message">The message.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="section">The section.</param>
    private void Emit(string type, string message, OrchestrationRunPlan plan, OrchestrationTaskSection? section = null)
    {
        _events.OnNext(new OrchestrationEvent
        {
            Type = type,
            Message = message,
            PlanId = plan.Id,
            SectionId = section?.Id,
            Section = section
        });
    }
}
