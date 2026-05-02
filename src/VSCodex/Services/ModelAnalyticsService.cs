using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VSCodex.Models;

namespace VSCodex.Services;

public interface IModelAnalyticsService
{
    IReadOnlyList<ModelProfile> Profiles { get; }
    ModelUsageEstimate Estimate(CodexRunRequest request);
}

public sealed class ModelAnalyticsService : IModelAnalyticsService
{
    private readonly IReadOnlyList<ModelProfile> _profiles = new[]
    {
        new ModelProfile { Id = "gpt-5.5", DisplayName = "GPT-5.5", InputPricePerMillion = 5.00d, OutputPricePerMillion = 30.00d, ContextWindowTokens = 1000000, BestForComplexity = ModelTaskComplexity.High, Notes = "Flagship model for complex coding and professional work." },
        new ModelProfile { Id = "gpt-5.4", DisplayName = "GPT-5.4", InputPricePerMillion = 2.50d, OutputPricePerMillion = 15.00d, ContextWindowTokens = 1000000, BestForComplexity = ModelTaskComplexity.High, Notes = "More affordable frontier model for coding and professional work." },
        new ModelProfile { Id = "gpt-5.4-mini", DisplayName = "GPT-5.4 Mini", InputPricePerMillion = 0.75d, OutputPricePerMillion = 4.50d, ContextWindowTokens = 400000, BestForComplexity = ModelTaskComplexity.Medium, Notes = "Lower-cost model suitable for focused edits, tests, and sub-agent tasks." },
        new ModelProfile { Id = "gpt-5.3-codex", DisplayName = "GPT-5.3 Codex", InputPricePerMillion = 1.75d, OutputPricePerMillion = 14.00d, ContextWindowTokens = 400000, BestForComplexity = ModelTaskComplexity.High, IsCodexOptimized = true, Notes = "Codex-optimized model for agentic coding tasks." },
        new ModelProfile { Id = "gpt-5.2-codex", DisplayName = "GPT-5.2 Codex", InputPricePerMillion = 1.75d, OutputPricePerMillion = 14.00d, ContextWindowTokens = 400000, BestForComplexity = ModelTaskComplexity.High, IsCodexOptimized = true, Notes = "Codex-optimized long-horizon coding model." },
        new ModelProfile { Id = "gpt-5.1-codex", DisplayName = "GPT-5.1 Codex", InputPricePerMillion = 1.25d, OutputPricePerMillion = 10.00d, ContextWindowTokens = 400000, BestForComplexity = ModelTaskComplexity.Medium, IsCodexOptimized = true, Notes = "Lower-cost Codex-optimized coding model." },
        new ModelProfile { Id = "gpt-5-codex", DisplayName = "GPT-5 Codex", InputPricePerMillion = 1.25d, OutputPricePerMillion = 10.00d, ContextWindowTokens = 400000, BestForComplexity = ModelTaskComplexity.Medium, IsCodexOptimized = true, Notes = "Codex-optimized GPT-5 model." }
    };

    public IReadOnlyList<ModelProfile> Profiles => _profiles;

    public ModelUsageEstimate Estimate(CodexRunRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var inputTokens = EstimateInputTokens(request);
        var outputTokens = EstimateOutputTokens(request, inputTokens);
        var primary = FindProfile(request.Options.Model);
        var budget = FindProfile(request.Options.BudgetModel);
        var failover = FindProfile(request.Options.FailoverModel);
        var complexity = ClassifyComplexity(request);
        var recommended = RecommendModel(complexity, primary, budget, inputTokens);
        var primaryCost = EstimateCost(primary, inputTokens, outputTokens);
        var budgetCost = EstimateCost(budget, inputTokens, outputTokens);
        var savings = primaryCost <= 0d ? 0d : Math.Max(0d, (primaryCost - budgetCost) / primaryCost * 100d);

        return new ModelUsageEstimate
        {
            EstimatedInputTokens = inputTokens,
            EstimatedOutputTokens = outputTokens,
            PrimaryModel = primary.Id,
            FailoverModel = failover.Id,
            BudgetModel = budget.Id,
            RecommendedModel = recommended.Id,
            PrimaryEstimatedCost = primaryCost,
            BudgetEstimatedCost = budgetCost,
            EstimatedSavingsPercent = savings,
            Complexity = complexity,
            RecommendationReason = BuildReason(complexity, recommended, primary, budget, savings),
            Summary = BuildSummary(primary, failover, budget, recommended, inputTokens, outputTokens, primaryCost, budgetCost, savings)
        };
    }

    private static int EstimateInputTokens(CodexRunRequest request)
    {
        var chars = SafeLength(request.Prompt);
        chars += request.WorkspaceFiles.Sum(x => SafeLength(x.Preview));
        chars += request.Memories.Sum(x => SafeLength(x.Text));
        chars += request.Skills.Where(x => x.IsEnabled).Sum(x => Math.Min(SafeLength(x.Content), 4000));
        chars += request.McpServers.Where(x => x.IsEnabled).Sum(x => SafeLength(x.Name) + SafeLength(x.Command) + x.Args.Sum(SafeLength));
        chars += request.Attachments.Sum(x => SafeLength(x.Path) + 128);
        return Math.Max(1, (int)Math.Ceiling(chars / 4d));
    }

    private static int EstimateOutputTokens(CodexRunRequest request, int inputTokens)
    {
        var modeMultiplier = request.Options.Mode == CodexRunMode.Build ? 0.45d : request.Options.Mode == CodexRunMode.Plan ? 0.30d : 0.20d;
        return Math.Max(700, Math.Min(12000, (int)Math.Ceiling(inputTokens * modeMultiplier) + 900));
    }

    private ModelProfile FindProfile(string model)
    {
        var profile = _profiles.FirstOrDefault(x => x.Id.Equals(model ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (profile != null) return profile;
        var fallback = _profiles.First(x => x.Id.Equals("gpt-5.5", StringComparison.OrdinalIgnoreCase));
        return new ModelProfile
        {
            Id = string.IsNullOrWhiteSpace(model) ? fallback.Id : model.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(model) ? fallback.DisplayName : model.Trim(),
            InputPricePerMillion = fallback.InputPricePerMillion,
            OutputPricePerMillion = fallback.OutputPricePerMillion,
            ContextWindowTokens = fallback.ContextWindowTokens,
            BestForComplexity = fallback.BestForComplexity,
            Notes = "Unknown model id; estimated with GPT-5.5 pricing until the profile is added."
        };
    }

    private static ModelTaskComplexity ClassifyComplexity(CodexRunRequest request)
    {
        var prompt = request.Prompt ?? string.Empty;
        var highRiskTerms = new[] { "architecture", "security", "authentication", "authorization", "migration", "production", "release", "debug", "exception", "threading", "concurrency", "memory", "mcp", "visual studio extension", "vsix", "reactiveui", "refactor" };
        var mediumTerms = new[] { "test", "review", "explain", "document", "optimize", "selection", "single file", "focused" };
        var words = prompt.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

        if (request.Options.Mode == CodexRunMode.Build && (words > 80 || highRiskTerms.Any(x => prompt.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)))
        {
            return ModelTaskComplexity.High;
        }

        if (words < 80 && mediumTerms.Any(x => prompt.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return ModelTaskComplexity.Low;
        }

        return words > 180 ? ModelTaskComplexity.High : ModelTaskComplexity.Medium;
    }

    private static ModelProfile RecommendModel(ModelTaskComplexity complexity, ModelProfile primary, ModelProfile budget, int inputTokens)
    {
        if (complexity == ModelTaskComplexity.High || inputTokens > Math.Min(250000, budget.ContextWindowTokens - 25000))
        {
            return primary;
        }

        return budget;
    }

    private static double EstimateCost(ModelProfile profile, int inputTokens, int outputTokens)
    {
        return inputTokens / 1000000d * profile.InputPricePerMillion
            + outputTokens / 1000000d * profile.OutputPricePerMillion;
    }

    private static string BuildReason(ModelTaskComplexity complexity, ModelProfile recommended, ModelProfile primary, ModelProfile budget, double savings)
    {
        if (recommended.Id.Equals(primary.Id, StringComparison.OrdinalIgnoreCase))
        {
            return $"Use {primary.Id} because this request is classified as {complexity.ToString().ToLowerInvariant()} complexity.";
        }

        return $"Use {budget.Id} for this {complexity.ToString().ToLowerInvariant()} complexity request; estimated savings are {savings.ToString("F0", CultureInfo.InvariantCulture)}%.";
    }

    private static string BuildSummary(ModelProfile primary, ModelProfile failover, ModelProfile budget, ModelProfile recommended, int inputTokens, int outputTokens, double primaryCost, double budgetCost, double savings)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "Estimated {0:N0} input / {1:N0} output tokens. Primary {2} about ${3:F4}; budget {4} about ${5:F4}; failover {6}. Recommendation: {7} ({8:F0}% possible savings).",
            inputTokens,
            outputTokens,
            primary.Id,
            primaryCost,
            budget.Id,
            budgetCost,
            failover.Id,
            recommended.Id,
            savings);
    }

    private static int SafeLength(string? value) => value == null ? 0 : value.Length;
}
