using System;
using System.Text;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VSCodex.Models;

namespace VSCodex.Services;

public interface ICodingAssistantContextService
{
    DebugContextSnapshot CaptureDebugContext();
    string BuildAskPrompt();
    string BuildExplainPrompt();
    string BuildFixPrompt();
    string BuildReviewPrompt();
    string BuildOptimizePrompt();
    string BuildDocumentationPrompt();
    string BuildDebugPrompt();
    string BuildTestPrompt();
    string BuildPlanPrompt(string userGoal, string agentSummary);
    string BuildReactiveMemorySetupPrompt();
}

public sealed class CodingAssistantContextService : ICodingAssistantContextService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkspaceContextService _workspace;

    public CodingAssistantContextService(IServiceProvider serviceProvider, IWorkspaceContextService workspace)
    {
        _serviceProvider = serviceProvider;
        _workspace = workspace;
    }

    public DebugContextSnapshot CaptureDebugContext()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var dte = _serviceProvider.GetService(typeof(DTE)) as DTE;
        var snapshot = new DebugContextSnapshot();
        try
        {
            snapshot.BreakReason = dte?.Debugger?.LastBreakReason.ToString() ?? string.Empty;
            snapshot.ExceptionDescription = dte?.Debugger?.CurrentMode == dbgDebugMode.dbgBreakMode
                ? SafeEvalException(dte)
                : string.Empty;
            snapshot.StackSummary = CaptureStack(dte);
        }
        catch (Exception ex)
        {
            snapshot.ExceptionDescription = ex.Message;
        }

        try { snapshot.Selection = _workspace.GetCurrentSelectionReference(0); } catch { }
        return snapshot;
    }

    public string BuildDebugPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var context = CaptureDebugContext();
        var sb = new StringBuilder();
        sb.AppendLine("Debug this Visual Studio context using systematic root-cause analysis before proposing fixes.");
        sb.AppendLine("Read the exception/debug context, trace likely data flow, identify evidence needed, then recommend the smallest safe fix and validation steps.");
        if (!string.IsNullOrWhiteSpace(context.BreakReason)) sb.AppendLine("Break reason: " + context.BreakReason);
        if (!string.IsNullOrWhiteSpace(context.ExceptionDescription)) sb.AppendLine("Exception: " + context.ExceptionDescription);
        if (!string.IsNullOrWhiteSpace(context.StackSummary)) { sb.AppendLine("Stack:"); sb.AppendLine(context.StackSummary); }
        if (context.Selection != null)
        {
            sb.AppendLine($"Selected code: {context.Selection.RelativePath} lines {context.Selection.StartLine}-{context.Selection.EndLine}");
            sb.AppendLine("```");
            sb.AppendLine(context.Selection.Preview);
            sb.AppendLine("```");
        }
        return sb.ToString().Trim();
    }

    public string BuildAskPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Use Codex to answer a question about the current Visual Studio context.",
            "If code is selected, focus on that selection. Otherwise inspect the active solution context and ask for any missing detail only when it blocks a correct answer.");
    }

    public string BuildExplainPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Explain the selected Visual Studio code clearly for a developer who will maintain it.",
            "Cover intent, control/data flow, key dependencies, edge cases, and any behavior that is easy to misread.");
    }

    public string BuildFixPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Fix the selected Visual Studio code with the smallest safe change.",
            "First identify the likely defect and evidence. Then propose or implement the fix, including the most relevant validation steps.");
    }

    public string BuildReviewPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Review the selected Visual Studio code.",
            "Prioritize correctness, regressions, concurrency/threading issues, API misuse, missing tests, and maintainability risks. Return findings first with file and line context when available.");
    }

    public string BuildOptimizePrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Optimize the selected Visual Studio code without changing behavior.",
            "Look for measurable performance, allocation, async/reactive, and UI-thread improvements. Explain tradeoffs and keep changes scoped.");
    }

    public string BuildDocumentationPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Generate or improve documentation for the selected Visual Studio code.",
            "Prefer concise XML documentation or nearby developer-facing comments only where they clarify behavior, contracts, or extension integration.");
    }

    public string BuildTestPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var selection = _workspace.GetCurrentSelectionReference(0);
        var sb = new StringBuilder();
        sb.AppendLine("Create focused tests for the selected Visual Studio code using test-driven-development principles.");
        sb.AppendLine("Identify behavior, edge cases, and test project/file placement. If implementation changes are needed, make the test fail first, then implement the minimal fix.");
        if (selection != null)
        {
            sb.AppendLine($"Selected code: {selection.RelativePath} lines {selection.StartLine}-{selection.EndLine}");
            sb.AppendLine("```");
            sb.AppendLine(selection.Preview);
            sb.AppendLine("```");
        }
        else
        {
            sb.AppendLine("No editor selection was available; inspect the active solution and propose the best test target.");
        }
        return sb.ToString().Trim();
    }

    public string BuildPlanPrompt(string userGoal, string agentSummary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Create an implementation plan for this Visual Studio solution.");
        sb.AppendLine("The plan must include the best use of sub-agents, with explicit Planner/Architect/Builder/Reviewer/Verifier responsibilities, expected model choice per agent, validation steps, and handoff order.");
        if (!string.IsNullOrWhiteSpace(agentSummary))
        {
            sb.AppendLine("Configured agents:");
            sb.AppendLine(agentSummary);
        }
        sb.AppendLine("Goal:");
        sb.AppendLine(string.IsNullOrWhiteSpace(userGoal) ? "Plan the selected coding task from current context." : userGoal);
        return sb.ToString().Trim();
    }

    public string BuildReactiveMemorySetupPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Verify and configure ReactiveMemory as the default Codex MCP memory system for this Visual Studio extension.");
        sb.AppendLine("Use MCP server `reactivememory` when available. First call `reactivememory_status`, then `reactivememory_react_to_prompt` for this setup request, and summarize any missing installation/configuration steps.");
        sb.AppendLine("The extension should preserve durable context by using `reactivememory_search`, `reactivememory_search_relays`, `reactivememory_add_drawer`, and `reactivememory_diary_write` with minimal user input.");
        return sb.ToString().Trim();
    }

    private string BuildSelectionCentredPrompt(string title, string instruction)
    {
        var selection = _workspace.GetCurrentSelectionReference(0);
        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine(instruction);
        if (selection != null)
        {
            sb.AppendLine($"Selected code: {selection.RelativePath} lines {selection.StartLine}-{selection.EndLine}");
            sb.AppendLine("```");
            sb.AppendLine(selection.Preview);
            sb.AppendLine("```");
        }
        else
        {
            sb.AppendLine("No editor selection was available; use the active solution/workspace context and ask for a target only if required.");
        }

        return sb.ToString().Trim();
    }

    private static string SafeEvalException(DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (dte == null) return string.Empty;
        try
        {
            var expression = dte.Debugger.GetExpression("$exception", false, 1000);
            return expression?.Value ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string CaptureStack(DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (dte == null) return string.Empty;
        try
        {
            var sb = new StringBuilder();
            var frames = dte.Debugger.CurrentThread?.StackFrames;
            if (frames == null) return string.Empty;
            var count = Math.Min(frames.Count, 20);
            for (var i = 1; i <= count; i++)
            {
                var frame = frames.Item(i);
                sb.AppendLine($"- {frame.FunctionName} {frame.Module}");
            }
            return sb.ToString().Trim();
        }
        catch { return string.Empty; }
    }
}
