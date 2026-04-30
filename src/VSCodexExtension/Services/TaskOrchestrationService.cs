using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VSCodexExtension.Models;

namespace VSCodexExtension.Services
{
    public interface ITaskOrchestrationService
    {
        IObservable<OrchestrationEvent> Events { get; }
        OrchestrationRunPlan? CurrentPlan { get; }
        Task<CodexRunResult> RunAsync(CodexRunRequest request);
        void Cancel();
    }

    public sealed class TaskOrchestrationService : ITaskOrchestrationService
    {
        private readonly ISettingsStore _settings;
        private readonly ICodexOrchestrator _codex;
        private readonly Subject<OrchestrationEvent> _events = new Subject<OrchestrationEvent>();
        private CancellationTokenSource? _cancellation;

        public TaskOrchestrationService(ISettingsStore settings, ICodexOrchestrator codex)
        {
            _settings = settings;
            _codex = codex;
        }

        public IObservable<OrchestrationEvent> Events => _events.AsObservable();
        public OrchestrationRunPlan? CurrentPlan { get; private set; }

        public async Task<CodexRunResult> RunAsync(CodexRunRequest request)
        {
            _cancellation = new CancellationTokenSource();
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

                var final = await RunFinalSynthesisAsync(request, plan, results).ConfigureAwait(false);
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
                if (running != null) running.Status = OrchestrationSectionStatus.Failed;
                Emit("plan-failed", ex.Message, plan, running);
                throw;
            }
        }

        public void Cancel()
        {
            _cancellation?.Cancel();
            _codex.Cancel();
        }

        private OrchestrationRunPlan BuildPlan(CodexRunRequest request)
        {
            var settings = _settings.Current;
            var configuredAgents = request.AgentRoles != null && request.AgentRoles.Count > 0
                ? request.AgentRoles
                : (IEnumerable<AgentRoleDefinition>)(settings.AgentRoles ?? new List<AgentRoleDefinition>());
            var agents = configuredAgents.Where(x => x.IsEnabled).ToList();
            if (agents.Count == 0)
            {
                agents = new List<AgentRoleDefinition>
                {
                    new AgentRoleDefinition { Name = "Planner", Role = "Planning", Instructions = "Plan the work." },
                    new AgentRoleDefinition { Name = "Builder", Role = "Implementation", Instructions = "Implement assigned work." },
                    new AgentRoleDefinition { Name = "Reviewer", Role = "Review", Instructions = "Review and validate the work." }
                };
            }

            var sections = SplitIntoSections(request.Prompt, agents).ToList();
            if (sections.Count == 0)
            {
                sections.Add(new OrchestrationTaskSection
                {
                    Index = 1,
                    Title = "Handle request",
                    Description = request.Prompt,
                    AssignedAgent = PickAgent(agents, "Builder", 0).Name
                });
            }

            return new OrchestrationRunPlan
            {
                Goal = request.Prompt,
                Strategy = request.Options.AgentStrategy,
                Agents = agents,
                Sections = sections
            };
        }

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
                candidates = new List<string>
                {
                    "Analyze requirements, repository context, risks, and acceptance criteria.",
                    "Design the implementation approach and identify files/services/UI surfaces to change.",
                    "Implement the requested capability in focused sections.",
                    "Review the implementation for correctness, safety, and integration issues.",
                    "Define and run the most relevant validation checks, then summarize evidence."
                };
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var title = candidates[i].Trim();
                if (title.Length > 90) title = title.Substring(0, 90).TrimEnd() + "...";
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

        private static IEnumerable<string> ExtractExplicitListItems(string prompt)
        {
            foreach (var raw in (prompt ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                var match = Regex.Match(line, @"^(?:[-*•]|\d+[.)])\s+(?<item>.+)$");
                if (match.Success && match.Groups["item"].Value.Trim().Length > 8)
                {
                    yield return match.Groups["item"].Value.Trim();
                }
            }
        }

        private static IEnumerable<string> ExtractSentenceSections(string prompt)
        {
            var parts = Regex.Split(prompt ?? string.Empty, @"\b(?:then|next|after that|finally|also|and then)\b", RegexOptions.IgnoreCase)
                .Select(x => x.Trim(' ', '.', ',', ';', ':'))
                .Where(x => x.Length > 24)
                .Take(8)
                .ToList();
            return parts.Count >= 2 ? parts : Enumerable.Empty<string>();
        }

        private static bool LooksLikeLargeTask(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return false;
            var words = Regex.Matches(prompt, @"\w+").Count;
            var keywords = new[] { "multiple", "large", "extensive", "implement", "create", "build", "refactor", "test", "plan", "orchestration", "agents", "steps" };
            return words > 80 || keywords.Count(k => prompt.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) >= 2;
        }

        private static AgentRoleDefinition PickAgentForIndex(IReadOnlyList<AgentRoleDefinition> agents, int index, int count)
        {
            if (index == 0) return PickAgent(agents, "Planner", index);
            if (index == count - 1) return PickAgent(agents, "Reviewer", index);
            return PickAgent(agents, "Builder", index);
        }

        private static AgentRoleDefinition PickAgent(IReadOnlyList<AgentRoleDefinition> agents, string preferredName, int fallbackIndex)
        {
            return agents.FirstOrDefault(x => x.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase))
                ?? agents.FirstOrDefault(x => x.Role.IndexOf(preferredName, StringComparison.OrdinalIgnoreCase) >= 0)
                ?? agents[Math.Abs(fallbackIndex) % agents.Count];
        }

        private static CodexRunRequest CloneForSection(CodexRunRequest request, OrchestrationRunPlan plan, OrchestrationTaskSection section, IReadOnlyList<OrchestrationTaskSection> completed)
        {
            var agent = plan.Agents.FirstOrDefault(x => x.Name.Equals(section.AssignedAgent, StringComparison.OrdinalIgnoreCase));
            var sb = new StringBuilder();
            sb.AppendLine($"You are the {section.AssignedAgent} agent in a multi-agent Codex orchestration run.");
            if (agent != null)
            {
                sb.AppendLine($"Role: {agent.Role}");
                sb.AppendLine($"Agent instructions: {agent.Instructions}");
            }
            sb.AppendLine();
            sb.AppendLine("## Overall goal");
            sb.AppendLine(plan.Goal);
            sb.AppendLine();
            sb.AppendLine("## Current section");
            sb.AppendLine($"{section.Index}. {section.Title}");
            sb.AppendLine(section.Description);
            sb.AppendLine();
            if (completed.Count > 0)
            {
                sb.AppendLine("## Completed prior sections");
                foreach (var item in completed)
                {
                    sb.AppendLine($"### {item.Index}. {item.Title} ({item.AssignedAgent})");
                    sb.AppendLine(item.Result.Length > 4000 ? item.Result.Substring(0, 4000) : item.Result);
                }
            }
            sb.AppendLine("Return only the output for this section. Be explicit about files changed, validation performed, and follow-up risks.");

            var options = CopyOptions(request.Options);
            if (agent != null && !string.IsNullOrWhiteSpace(agent.Model)) options.Model = agent.Model;
            else if (request.Options.BudgetDrivenModelSelection && !string.IsNullOrWhiteSpace(request.Options.BudgetModel)) options.Model = request.Options.BudgetModel;
            options.UseMultiAgentOrchestration = false;

            return new CodexRunRequest
            {
                Prompt = sb.ToString(),
                ThreadId = null,
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

        private async Task<CodexRunResult> RunFinalSynthesisAsync(CodexRunRequest request, OrchestrationRunPlan plan, IReadOnlyList<OrchestrationTaskSection> sections)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are the final orchestration coordinator. Consolidate the multi-agent outputs into one final response.");
            sb.AppendLine("Do not re-run implementation unless a critical gap is obvious. Summarize completed sections, changed files, validation, and residual risks.");
            sb.AppendLine();
            sb.AppendLine("## Original user request");
            sb.AppendLine(request.Prompt);
            sb.AppendLine();
            sb.AppendLine("## Section outputs");
            foreach (var section in sections)
            {
                sb.AppendLine($"### {section.Index}. {section.Title} — {section.AssignedAgent} — {section.Status}");
                sb.AppendLine(section.Result);
                sb.AppendLine();
            }

            var finalRequest = new CodexRunRequest
            {
                Prompt = sb.ToString(),
                ThreadId = request.ThreadId,
                WorkspaceRoot = request.WorkspaceRoot,
                Options = CopyOptions(request.Options),
                Attachments = request.Attachments,
                Skills = request.Skills,
                Memories = request.Memories,
                McpServers = request.McpServers,
                WorkspaceFiles = request.WorkspaceFiles,
                AgentRoles = request.AgentRoles
            };
            finalRequest.Options.UseMultiAgentOrchestration = false;
            if (!string.IsNullOrWhiteSpace(request.Options.OrchestrationModel)) finalRequest.Options.Model = request.Options.OrchestrationModel;
            if (request.Options.BudgetDrivenModelSelection && !string.IsNullOrWhiteSpace(request.Options.BudgetModel)) finalRequest.Options.Model = request.Options.BudgetModel;
            return await _codex.RunAsync(finalRequest).ConfigureAwait(false);
        }

        private static CodexRunOptions CopyOptions(CodexRunOptions source)
        {
            return new CodexRunOptions
            {
                Model = source.Model,
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

        private string RenderPlanSummary(OrchestrationRunPlan plan)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Orchestration {plan.Id}");
            sb.AppendLine($"Goal: {plan.Goal}");
            foreach (var section in plan.Sections)
            {
                sb.AppendLine($"- {section.Index}. {section.Title} [{section.AssignedAgent}] — {section.Status}");
            }
            return sb.ToString();
        }

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
}
