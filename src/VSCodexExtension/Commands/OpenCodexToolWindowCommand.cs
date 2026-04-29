using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using VSCodexExtension.Infrastructure;
using VSCodexExtension.ToolWindows;

namespace VSCodexExtension.Commands
{
    internal sealed class OpenCodexToolWindowCommand
    {
        private readonly AsyncPackage _package;
        private OpenCodexToolWindowCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            var commandId = new CommandID(new Guid(CodexCommandIds.CommandSetGuidString), CodexCommandIds.OpenToolWindowCommandId);
            commandService.AddCommand(new MenuCommand(Execute, commandId));
        }
        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)).ConfigureAwait(true) as OleMenuCommandService;
            if (commandService != null) _ = new OpenCodexToolWindowCommand(package, commandService);
        }
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                var window = await _package.ShowToolWindowAsync(typeof(CodexToolWindowPane), 0, true, _package.DisposalToken).ConfigureAwait(true);
                if (window == null || window.Frame == null) throw new NotSupportedException("Cannot create Codex tool window.");
            }).FireAndForget();
        }
    }
}
