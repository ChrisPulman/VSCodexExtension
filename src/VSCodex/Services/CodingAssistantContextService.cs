// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Text;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the coding Assistant Context Service implementation.</summary>
/// <param name="serviceProvider">The Visual Studio service provider.</param>
/// <param name="workspace">The workspace context service.</param>
public sealed class CodingAssistantContextService(IServiceProvider serviceProvider, IWorkspaceContextService workspace) : ICodingAssistantContextService
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric1000 = 1000;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric12000 = 12_000;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric20 = 20;

    /// <summary>Stores the service Provider.</summary>
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <summary>Stores the workspace.</summary>
    private readonly IWorkspaceContextService _workspace = workspace;

    /// <summary>Performs the capture Debug Context operation.</summary>
    /// <returns>The capture Debug Context result.</returns>
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

        try
        {
            snapshot.Selection = _workspace.GetCurrentSelectionReference(0);
        }
        catch (Exception ex)
        {
            _ = ActivityLog.TryLogWarning(nameof(CodingAssistantContextService), ex.Message);
        }

        return snapshot;
    }

    /// <summary>Builds debug Prompt.</summary>
    /// <returns>The build Debug Prompt result.</returns>
    public string BuildDebugPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var context = CaptureDebugContext();
        var sb = new StringBuilder();
        _ = sb.AppendLine("Debug this Visual Studio context using systematic root-cause analysis before proposing fixes.");
        _ = sb.AppendLine("Read the exception/debug context, trace likely data flow, identify evidence needed, then recommend the smallest safe fix and validation steps.");
        if (!string.IsNullOrWhiteSpace(context.BreakReason))
        {
            _ = sb.AppendLine($"Break reason: {context.BreakReason}");
        }

        if (!string.IsNullOrWhiteSpace(context.ExceptionDescription))
        {
            _ = sb.AppendLine($"Exception: {context.ExceptionDescription}");
        }

        if (!string.IsNullOrWhiteSpace(context.StackSummary))
        {
            _ = sb.AppendLine("Stack:");
            _ = sb.AppendLine(context.StackSummary);
        }

        if (context.Selection is not null)
        {
            _ = sb.AppendLine($"Selected code: {context.Selection.RelativePath} lines {context.Selection.StartLine}-{context.Selection.EndLine}");
            _ = sb.AppendLine("```");
            _ = sb.AppendLine(context.Selection.Preview);
            _ = sb.AppendLine("```");
        }

        return sb.ToString().Trim();
    }

    /// <summary>Builds ask Prompt.</summary>
    /// <returns>The build Ask Prompt result.</returns>
    public string BuildAskPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Use Codex to answer a question about the current Visual Studio context.",
            "If code is selected, focus on that selection. Otherwise inspect the active solution context and ask for any missing detail only when it blocks a correct answer.");
    }

    /// <summary>Builds explain Prompt.</summary>
    /// <returns>The build Explain Prompt result.</returns>
    public string BuildExplainPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Explain the selected Visual Studio code clearly for a developer who will maintain it.",
            "Cover intent, control/data flow, key dependencies, edge cases, and any behavior that is easy to misread.");
    }

    /// <summary>Builds fix Prompt.</summary>
    /// <returns>The build Fix Prompt result.</returns>
    public string BuildFixPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Fix the selected Visual Studio code with the smallest safe change.",
            "First identify the likely defect and evidence. Then propose or implement the fix, including the most relevant validation steps.");
    }

    /// <summary>Builds review Prompt.</summary>
    /// <returns>The build Review Prompt result.</returns>
    public string BuildReviewPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Review the selected Visual Studio code.",
            "Prioritize correctness, regressions, concurrency/threading issues, API misuse, missing tests, "
            + "and maintainability risks. Return findings first with file and line context when available.");
    }

    /// <summary>Builds optimize Prompt.</summary>
    /// <returns>The build Optimize Prompt result.</returns>
    public string BuildOptimizePrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Optimize the selected Visual Studio code without changing behavior.",
            "Look for measurable performance, allocation, async/reactive, and UI-thread improvements. Explain tradeoffs and keep changes scoped.");
    }

    /// <summary>Builds documentation Prompt.</summary>
    /// <returns>The build Documentation Prompt result.</returns>
    public string BuildDocumentationPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return BuildSelectionCentredPrompt(
            "Generate or improve documentation for the selected Visual Studio code.",
            "Prefer concise XML documentation or nearby developer-facing comments only where they clarify behavior, contracts, or extension integration.");
    }

    /// <summary>Builds test Prompt.</summary>
    /// <returns>The build Test Prompt result.</returns>
    public string BuildTestPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var selection = _workspace.GetCurrentSelectionReference(0);
        var sb = new StringBuilder();
        _ = sb.AppendLine("Create focused tests for the selected Visual Studio code using test-driven-development principles.");
        _ = sb.AppendLine("Identify behavior, edge cases, and test project/file placement. If implementation changes are needed, make the test fail first, then implement the minimal fix.");
        if (selection is not null)
        {
            _ = sb.AppendLine($"Selected code: {selection.RelativePath} lines {selection.StartLine}-{selection.EndLine}");
            _ = sb.AppendLine("```");
            _ = sb.AppendLine(selection.Preview);
            _ = sb.AppendLine("```");
        }
        else
        {
            _ = sb.AppendLine("No editor selection was available; inspect the active solution and propose the best test target.");
        }

        return sb.ToString().Trim();
    }

    /// <summary>Builds test Failure Prompt.</summary>
    /// <returns>The build Test Failure Prompt result.</returns>
    public string BuildTestFailurePrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var debug = CaptureDebugContext();
        var selection = _workspace.GetCurrentSelectionReference(Numeric12000);
        var sb = new StringBuilder();
        _ = sb.AppendLine("Fix the active Visual Studio test failure or the most relevant failing test for this context.");
        _ = sb.AppendLine(
            "Use the active debugger exception, stack frame, selected code, and solution context to identify "
            + "the failing behavior, then propose the smallest safe fix and the exact tests to rerun.");
        if (!string.IsNullOrWhiteSpace(debug.BreakReason))
        {
            _ = sb.AppendLine($"Break reason: {debug.BreakReason}");
        }

        if (!string.IsNullOrWhiteSpace(debug.ExceptionDescription))
        {
            _ = sb.AppendLine($"Exception: {debug.ExceptionDescription}");
        }

        if (!string.IsNullOrWhiteSpace(debug.StackSummary))
        {
            _ = sb.AppendLine("Stack:");
            _ = sb.AppendLine(debug.StackSummary);
        }

        if (selection is not null)
        {
            _ = sb.AppendLine($"Selected code: {selection.RelativePath} lines {selection.StartLine}-{selection.EndLine}");
            _ = sb.AppendLine("```");
            _ = sb.AppendLine(selection.Preview);
            _ = sb.AppendLine("```");
        }
        else
        {
            _ = sb.AppendLine("No editor selection was available; inspect the active solution, test projects, recent failures, and active document context.");
        }

        return sb.ToString().Trim();
    }

    /// <summary>Builds plan Prompt.</summary>
    /// <param name="userGoal">The user Goal.</param>
    /// <param name="agentSummary">The agent Summary.</param>
    /// <returns>The build Plan Prompt result.</returns>
    public string BuildPlanPrompt(string userGoal, string agentSummary)
    {
        var sb = new StringBuilder();
        _ = sb.AppendLine("Create an implementation plan for this Visual Studio solution.");
        _ = sb.AppendLine(
            "The plan must include the best use of sub-agents, with explicit Planner/Architect/Builder/Reviewer/Verifier "
            + "responsibilities, expected model choice per agent, validation steps, and handoff order.");
        if (!string.IsNullOrWhiteSpace(agentSummary))
        {
            _ = sb.AppendLine("Configured agents:");
            _ = sb.AppendLine(agentSummary);
        }

        _ = sb.AppendLine("Goal:");
        _ = sb.AppendLine(string.IsNullOrWhiteSpace(userGoal) ? "Plan the selected coding task from current context." : userGoal);
        return sb.ToString().Trim();
    }

    /// <summary>Builds reactive Memory Setup Prompt.</summary>
    /// <returns>The build Reactive Memory Setup Prompt result.</returns>
    public string BuildReactiveMemorySetupPrompt()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _workspace.RefreshWorkspaceIdentity();
        var identity = _workspace.CurrentWorkspaceIdentity;
        var selection = _workspace.GetCurrentSelectionReference(0);
        var sb = new StringBuilder();
        _ = sb.AppendLine("Verify and configure ReactiveMemory as the default Codex MCP memory system for this Visual Studio extension.");
        _ = sb.AppendLine("Use the currently loaded Visual Studio solution as the setup target. Do not answer generically.");
        _ = sb.AppendLine("Current solution context:");
        _ = sb.AppendLine($"- Solution: {(string.IsNullOrWhiteSpace(_workspace.CurrentSolutionPath) ? "<none loaded>" : _workspace.CurrentSolutionPath)}");
        _ = sb.AppendLine($"- Workspace root: {(string.IsNullOrWhiteSpace(_workspace.CurrentWorkspaceRoot) ? "<unknown>" : _workspace.CurrentWorkspaceRoot)}");
        _ = sb.AppendLine($"- Workspace name: {(string.IsNullOrWhiteSpace(_workspace.CurrentWorkspaceName) ? "<unknown>" : _workspace.CurrentWorkspaceName)}");
        _ = sb.AppendLine($"- Workspace identity: {(string.IsNullOrWhiteSpace(identity.Id) ? "<not available>" : identity.Id)}");
        _ = sb.AppendLine($"- Memory root: {(string.IsNullOrWhiteSpace(_workspace.CurrentWorkspaceMemoryRoot) ? "<not available>" : _workspace.CurrentWorkspaceMemoryRoot)}");
        if (selection is not null)
        {
            _ = sb.AppendLine($"- Selected code: {selection.RelativePath} lines {selection.StartLine}-{selection.EndLine}");
        }

        _ = sb.AppendLine(
            "Use MCP server `reactivememory` when available. First call `reactivememory_status`, then "
            + "`reactivememory_react_to_prompt` for this setup request, and summarize any missing installation/configuration steps.");
        _ = sb.AppendLine(
            "The extension should preserve durable context by using `reactivememory_search`, "
            + "`reactivememory_search_relays`, `reactivememory_add_drawer`, and `reactivememory_diary_write` with minimal user input.");
        return sb.ToString().Trim();
    }

    /// <summary>Builds selection Centred Prompt.</summary>
    /// <param name="title">The title.</param>
    /// <param name="instruction">The instruction.</param>
    /// <returns>The build Selection Centred Prompt result.</returns>
    private string BuildSelectionCentredPrompt(string title, string instruction)
    {
        var selection = _workspace.GetCurrentSelectionReference(0);
        var sb = new StringBuilder();
        _ = sb.AppendLine(title);
        _ = sb.AppendLine(instruction);
        if (selection is not null)
        {
            _ = sb.AppendLine($"Selected code: {selection.RelativePath} lines {selection.StartLine}-{selection.EndLine}");
            _ = sb.AppendLine("```");
            _ = sb.AppendLine(selection.Preview);
            _ = sb.AppendLine("```");
        }
        else
        {
            _ = sb.AppendLine("No editor selection was available; use the active solution/workspace context and ask for a target only if required.");
        }

        return sb.ToString().Trim();
    }

    /// <summary>Performs the safe Eval Exception operation.</summary>
    /// <param name="dte">The dte.</param>
    /// <returns>The safe Eval Exception result.</returns>
    private string SafeEvalException(DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (dte is null)
        {
            return string.Empty;
        }

        try
        {
            var expression = dte.Debugger.GetExpression("$exception", false, Numeric1000);
            return expression?.Value ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Performs the capture Stack operation.</summary>
    /// <param name="dte">The dte.</param>
    /// <returns>The capture Stack result.</returns>
    private string CaptureStack(DTE? dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (dte is null)
        {
            return string.Empty;
        }

        try
        {
            var sb = new StringBuilder();
            var frames = dte.Debugger.CurrentThread?.StackFrames;
            if (frames is null)
            {
                return string.Empty;
            }

            var count = Math.Min(frames.Count, Numeric20);
            for (var i = 1; i <= count; i++)
            {
                var frame = frames.Item(i);
                _ = sb.AppendLine($"- {frame.FunctionName} {frame.Module}");
            }

            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
