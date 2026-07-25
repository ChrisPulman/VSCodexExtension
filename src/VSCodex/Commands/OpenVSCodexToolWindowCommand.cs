// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.Design;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VSCodex.Infrastructure;
using VSCodex.Options;
using VSCodex.Services;
using VSCodex.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace VSCodex.Commands;

/// <summary>Provides the open VS Codex Tool Window Command implementation.</summary>
internal sealed class OpenVSCodexToolWindowCommand
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric10 = 10;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric250 = 250;

    /// <summary>Stores the initialized.</summary>
    private static bool _initialized;

    /// <summary>Stores the package.</summary>
    private readonly AsyncPackage _package;

    /// <summary>Initializes a new instance of the <see cref="OpenVSCodexToolWindowCommand"/> class.</summary>
    /// <param name="package">The package.</param>
    /// <param name="commandService">The command Service.</param>
    private OpenVSCodexToolWindowCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        _package = package ?? throw new ArgumentNullException(nameof(package));

        AddCommand(commandService, CodexCommandIds.OpenToolWindowCommandId, ExecuteOpenToolWindow, QueryOpenToolWindowCommandStatus);
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
        AddCommand(commandService, CodexCommandIds.FixActiveExceptionCommandId, ExecuteFixActiveException, QueryActiveExceptionCommandStatus);
        AddCommand(commandService, CodexCommandIds.FixActiveErrorCommandId, ExecuteFixActiveError, QueryActiveErrorCommandStatus);
        AddCommand(commandService, CodexCommandIds.FixTestFailureCommandId, ExecuteFixTestFailure, QueryTestFailureCommandStatus);
    }

    /// <summary>Initializes the operation.</summary>
    /// <param name="package">The package.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async Task InitializeAsync(AsyncPackage package)
    {
        if (_initialized)
        {
            return;
        }

        for (var attempt = 1; attempt <= Numeric10 && !_initialized; attempt++)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)).ConfigureAwait(true) as OleMenuCommandService;
            if (commandService is not null)
            {
                _ = new OpenVSCodexToolWindowCommand(package, commandService);
                _initialized = true;
                _ = ActivityLog.TryLogInformation(nameof(OpenVSCodexToolWindowCommand), "Registered VSCodex Visual Studio commands.");
                return;
            }

            _ = ActivityLog.TryLogWarning(nameof(OpenVSCodexToolWindowCommand), "IMenuCommandService was unavailable while registering VSCodex commands; retrying.");
            await Task.Delay(TimeSpan.FromMilliseconds(Numeric250), package.DisposalToken).ConfigureAwait(false);
        }
    }

    /// <summary>Adds command.</summary>
    /// <param name="commandService">The command Service.</param>
    /// <param name="commandId">The command Id.</param>
    /// <param name="execute">The execute.</param>
    /// <param name="beforeQueryStatus">The before Query Status.</param>
    private static void AddCommand(OleMenuCommandService commandService, int commandId, EventHandler execute, EventHandler? beforeQueryStatus = null)
    {
        var commandIdentifier = new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), commandId);
        if (commandService.FindCommand(commandIdentifier) is not null)
        {
            return;
        }

        var command = new OleMenuCommand(execute, commandIdentifier);
        if (beforeQueryStatus is not null)
        {
            command.BeforeQueryStatus += beforeQueryStatus;
        }

        commandService.AddCommand(command);
    }

    /// <summary>Executes open Tool Window.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteOpenToolWindow(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        RunVSCodexCommand(() => OpenToolWindowAsync());
    }

    /// <summary>Executes open Settings.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteOpenSettings(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        RunVSCodexCommand(() =>
        {
            _package.ShowOptionPage(typeof(OptionsProvider.GeneralOptions));
            return Task.CompletedTask;
        });
    }

    /// <summary>Executes create Test From Selection.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteCreateTestFromSelection(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildTestPrompt());
    }

    /// <summary>Executes ask Codex.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteAskCodex(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildAskPrompt());
    }

    /// <summary>Executes explain Selection.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteExplainSelection(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildExplainPrompt());
    }

    /// <summary>Executes fix Selection.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteFixSelection(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildFixPrompt());
    }

    /// <summary>Executes review Selection.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteReviewSelection(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildReviewPrompt());
    }

    /// <summary>Executes optimize Selection.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteOptimizeSelection(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildOptimizePrompt());
    }

    /// <summary>Executes generate Docs.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteGenerateDocs(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildDocumentationPrompt());
    }

    /// <summary>Executes debug With Codex.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteDebugWithCodex(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildDebugPrompt());
    }

    /// <summary>Executes create Plan.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteCreatePlan(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildPlanPrompt("Create a plan for the current selected coding task.", string.Empty));
    }

    /// <summary>Executes configure Memory.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteConfigureMemory(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildReactiveMemorySetupPrompt());
    }

    /// <summary>Executes fix Active Exception.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteFixActiveException(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ExecuteDebugWithCodex(sender, e);
    }

    /// <summary>Executes fix Active Error.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteFixActiveError(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ExecuteFixSelection(sender, e);
    }

    /// <summary>Executes fix Test Failure.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void ExecuteFixTestFailure(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShowPromptFromContext(x => x.BuildTestFailurePrompt());
    }

    /// <summary>Performs the query Open Tool Window Command Status operation.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void QueryOpenToolWindowCommandStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is not OleMenuCommand command)
        {
            return;
        }

        command.Visible = true;
        command.Enabled = true;
        command.Text = "VSCodex";
    }

    /// <summary>Performs the query Editor Context Command Status operation.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void QueryEditorContextCommandStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is not OleMenuCommand command)
        {
            return;
        }

        command.Visible = true;
        command.Enabled = HasActiveDocument();
    }

    /// <summary>Performs the query Debug Command Status operation.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void QueryDebugCommandStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is not OleMenuCommand command)
        {
            return;
        }

        var inBreakMode = IsDebuggerInBreakMode();
        command.Visible = true;
        command.Enabled = HasActiveDocument() || inBreakMode;
        command.Text = inBreakMode ? "Debug Exception with VSCodex" : "Debug With VSCodex";
    }

    /// <summary>Performs the query Active Exception Command Status operation.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void QueryActiveExceptionCommandStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is not OleMenuCommand command)
        {
            return;
        }

        command.Visible = true;
        command.Enabled = HasActiveDocument() || IsDebuggerInBreakMode();
        command.Text = "Fix Active Exception with VSCodex";
    }

    /// <summary>Performs the query Active Error Command Status operation.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void QueryActiveErrorCommandStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is not OleMenuCommand command)
        {
            return;
        }

        command.Visible = true;
        command.Enabled = true;
        command.Text = "Fix with VSCodex";
    }

    /// <summary>Performs the query Test Failure Command Status operation.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    private void QueryTestFailureCommandStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (sender is not OleMenuCommand command)
        {
            return;
        }

        command.Visible = true;
        command.Enabled = HasActiveDocument();
        command.Text = "Fix Test Failure with VSCodex";
    }

    /// <summary>Determines whether has Active Document.</summary>
    /// <returns><see langword="true"/> when has Active Document succeeds; otherwise, <see langword="false"/>.</returns>
    private bool HasActiveDocument()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            var dte = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
            return dte?.ActiveDocument is not null;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Determines whether is Debugger In Break Mode.</summary>
    /// <returns><see langword="true"/> when is Debugger In Break Mode succeeds; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>Performs the show Prompt From Context operation.</summary>
    /// <param name="promptFactory">The prompt Factory.</param>
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

    /// <summary>Runs vS Codex Command.</summary>
    /// <param name="action">The action.</param>
    private void RunVSCodexCommand(Func<Task> action)
    {
        TaskObserver.FireAndForget(_package.JoinableTaskFactory.RunAsync(async () =>
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
                _ = ActivityLog.TryLogError(nameof(OpenVSCodexToolWindowCommand), ex.ToString());
                _ = VsShellUtilities.ShowMessageBox(
                    _package,
                    $"VSCodex could not complete the command. Check the Visual Studio ActivityLog for details.\r\n\r\n{ex.Message}",
                    "VSCodex",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }).Task);
    }

    /// <summary>Opens tool Window.</summary>
    /// <param name="configure">The configure.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task OpenToolWindowAsync(Action<VSCodexToolWindowPane>? configure = null)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
        var window = await _package.ShowToolWindowAsync(typeof(VSCodexToolWindowPane), 0, true, _package.DisposalToken).ConfigureAwait(true) as VSCodexToolWindowPane;
        if (window is null || window.Frame is null)
        {
            throw new NotSupportedException("Cannot create VSCodex tool window.");
        }

        configure?.Invoke(window);
    }
}
