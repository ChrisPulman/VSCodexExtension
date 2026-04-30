using System;
using System.Text;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VSCodexExtension.Models;

namespace VSCodexExtension.Services
{
    public interface ICodingAssistantContextService
    {
        DebugContextSnapshot CaptureDebugContext();
        string BuildDebugPrompt();
        string BuildTestPrompt();
        string BuildPlanPrompt(string userGoal, string agentSummary);
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

            try { snapshot.Selection = _workspace.GetCurrentSelectionReference(12000); } catch { }
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

        public string BuildTestPrompt()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var selection = _workspace.GetCurrentSelectionReference(12000);
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
}
