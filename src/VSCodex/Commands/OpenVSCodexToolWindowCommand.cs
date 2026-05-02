using System;
using System.ComponentModel.Design;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using VSCodex.Infrastructure;
using VSCodex.Services;
using VSCodex.ToolWindows;

namespace VSCodex.Commands;

internal sealed class OpenVSCodexToolWindowCommand
{
    private readonly AsyncPackage _package;

    private OpenVSCodexToolWindowCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        _package = package ?? throw new ArgumentNullException(nameof(package));

        AddCommand(commandService, CodexCommandIds.OpenToolWindowCommandId, ExecuteOpenToolWindow);
        AddCommand(commandService, CodexCommandIds.OpenOptionsCommandId, ExecuteOpenSettings);
        AddCommand(commandService, CodexCommandIds.AskCodexCommandId, ExecuteAskCodex, QueryEditorContextCommandStatus);
        AddCommand(commandService, CodexCommandIds.ExplainSelectionCommandId, ExecuteExplainSelection, QueryEditorContextCommandStatus);
        AddCommand(commandService, CodexCommandIds.FixSelectionCommandId, ExecuteFixSelection, QueryEditorContextCommandStatus);
        AddCommand(commandService, CodexCommandIds.ReviewSelectionCommandId, ExecuteReviewSelection, QueryEditorContextCommandStatus);
        AddCommand(commandService, CodexCommandIds.OptimizeSelectionCommandId, ExecuteOptimizeSelection, QueryEditorContextCommandStatus);
        AddCommand(commandService, CodexCommandIds.GenerateDocsCommandId, ExecuteGenerateDocs, QueryEditorContextCommandStatus);
        AddCommand(commandService, CodexCommandIds.CreateTestFromSelectionCommandId, ExecuteCreateTestFromSelection, QueryEditorContextCommandStatus);
        AddCommand(commandService, CodexCommandIds.DebugWithCodexCommandId, ExecuteDebugWithCodex, QueryDebugCommandStatus);
        AddCommand(commandService, CodexCommandIds.CreatePlanCommandId, ExecuteCreatePlan, QueryEditorContextCommandStatus);
        AddCommand(commandService, CodexCommandIds.ConfigureMemoryCommandId, ExecuteConfigureMemory);
    }

    private static void AddCommand(OleMenuCommandService commandService, int commandId, EventHandler execute, EventHandler? beforeQueryStatus = null)
    {
        var command = new OleMenuCommand(execute, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), commandId));
        if (beforeQueryStatus != null)
        {
            command.BeforeQueryStatus += beforeQueryStatus;
        }

        commandService.AddCommand(command);
    }

    public static async Task InitializeAsync(AsyncPackage package)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)).ConfigureAwait(true) as OleMenuCommandService;
        if (commandService != null)
        {
            _ = new OpenVSCodexToolWindowCommand(package, commandService);
        }
    }

    private void ExecuteOpenToolWindow(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        RunVSCodexCommand(() => OpenToolWindowAsync());
    }

    private void ExecuteOpenSettings(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        RunVSCodexCommand(() => OpenToolWindowAsync(window => window.ShowSettings()));
    }

    private void ExecuteCreateTestFromSelection(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildTestPrompt());
    }

    private void ExecuteAskCodex(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildAskPrompt());
    }

    private void ExecuteExplainSelection(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildExplainPrompt());
    }

    private void ExecuteFixSelection(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildFixPrompt());
    }

    private void ExecuteReviewSelection(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildReviewPrompt());
    }

    private void ExecuteOptimizeSelection(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildOptimizePrompt());
    }

    private void ExecuteGenerateDocs(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildDocumentationPrompt());
    }

    private void ExecuteDebugWithCodex(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildDebugPrompt());
    }

    private void ExecuteCreatePlan(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildPlanPrompt("Create a plan for the current selected coding task.", string.Empty));
    }

    private void ExecuteConfigureMemory(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildReactiveMemorySetupPrompt());
    }

    private void QueryEditorContextCommandStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is OleMenuCommand command)
        {
            command.Visible = true;
            command.Enabled = HasActiveDocument();
        }
    }

    private void QueryDebugCommandStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is OleMenuCommand command)
        {
            var inBreakMode = IsDebuggerInBreakMode();
            command.Visible = true;
            command.Enabled = HasActiveDocument() || inBreakMode;
            command.Text = inBreakMode ? "Debug Exception with VSCodex" : "Debug With VSCodex";
        }
    }

    private bool HasActiveDocument()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            var dte = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
            return dte?.ActiveDocument != null;
        }
        catch
        {
            return true;
        }
    }

    private bool IsDebuggerInBreakMode()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            var dte = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
            return dte?.Debugger?.CurrentMode == dbgDebugMode.dbgBreakMode;
        }
        catch
        {
            return false;
        }
    }

    private void ShowPromptFromContext(Func<ICodingAssistantContextService, string> promptFactory)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        RunVSCodexCommand(async () =>
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            var workspace = new WorkspaceContextService(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider);
            workspace.Refresh();
            var context = new CodingAssistantContextService(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider, workspace);
            var prompt = promptFactory(context);
            await OpenToolWindowAsync(window => window.SetPrompt(prompt)).ConfigureAwait(true);
        });
    }

    private void RunVSCodexCommand(Func<Task> action)
    {
        _package.JoinableTaskFactory.RunAsync(async () =>
        {
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (_package.DisposalToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
                ActivityLog.TryLogError(nameof(OpenVSCodexToolWindowCommand), ex.ToString());
                VsShellUtilities.ShowMessageBox(
                    _package,
                    "VSCodex could not complete the command. Check the Visual Studio ActivityLog for details.\r\n\r\n" + ex.Message,
                    "VSCodex",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }).Task.FireAndForget();
    }

    private async Task OpenToolWindowAsync(Action<VSCodexToolWindowPane>? configure = null)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
        var window = await _package.ShowToolWindowAsync(typeof(VSCodexToolWindowPane), 0, true, _package.DisposalToken).ConfigureAwait(true) as VSCodexToolWindowPane;
        if (window == null || window.Frame == null)
        {
            throw new NotSupportedException("Cannot create VSCodex tool window.");
        }

        configure?.Invoke(window);
    }
}
