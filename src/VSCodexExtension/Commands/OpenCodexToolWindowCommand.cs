using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using VSCodexExtension.Infrastructure;
using VSCodexExtension.Services;
using VSCodexExtension.ToolWindows;

namespace VSCodexExtension.Commands
{
    internal sealed class OpenCodexToolWindowCommand
    {
        private readonly AsyncPackage _package;

        private OpenCodexToolWindowCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            commandService.AddCommand(new MenuCommand(ExecuteOpenToolWindow, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.OpenToolWindowCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteOpenOptions, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.OpenOptionsCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteAskCodex, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.AskCodexCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteExplainSelection, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.ExplainSelectionCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteFixSelection, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.FixSelectionCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteReviewSelection, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.ReviewSelectionCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteOptimizeSelection, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.OptimizeSelectionCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteGenerateDocs, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.GenerateDocsCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteCreateTestFromSelection, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.CreateTestFromSelectionCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteDebugWithCodex, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.DebugWithCodexCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteCreatePlan, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.CreatePlanCommandId)));
            commandService.AddCommand(new MenuCommand(ExecuteConfigureMemory, new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.ConfigureMemoryCommandId)));
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)).ConfigureAwait(true) as OleMenuCommandService;
            if (commandService != null)
            {
                _ = new OpenCodexToolWindowCommand(package, commandService);
            }
        }

        private void ExecuteOpenToolWindow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                var window = await _package.ShowToolWindowAsync(typeof(CodexToolWindowPane), 0, true, _package.DisposalToken).ConfigureAwait(true);
                if (window == null || window.Frame == null)
                {
                    throw new NotSupportedException("Cannot create Codex tool window.");
                }
            }).Task.FireAndForget();
        }

        private void ExecuteOpenOptions(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_package is global::VSCodexExtension.VSCodexExtensionPackage codexPackage)
            {
                codexPackage.ShowCodexOptionsPage();
            }
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

        private void ShowPromptFromContext(Func<ICodingAssistantContextService, string> promptFactory)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var workspace = new WorkspaceContextService(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider);
            workspace.Refresh();
            var context = new CodingAssistantContextService(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider, workspace);
            var prompt = promptFactory(context);
            _package.JoinableTaskFactory.RunAsync(async () => await CodexToolWindowPane.ShowWithPromptAsync(_package, prompt).ConfigureAwait(true)).Task.FireAndForget();
        }
    }
}
