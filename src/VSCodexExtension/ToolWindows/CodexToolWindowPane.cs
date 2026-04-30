using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using VSCodexExtension.Infrastructure;
using VSCodexExtension.ViewModels;

namespace VSCodexExtension.ToolWindows
{
    [Guid("ee7f4f9f-8f35-46cb-9a77-a09e33f60b60")]
    public sealed class CodexToolWindowPane : ToolWindowPane
    {
        public CodexToolWindowPane() : base(null)
        {
            Caption = "Codex";
            var app = RxAppBuilder.CreateVisualStudioDefault(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider).Build();
            Content = app.CreateToolWindowControl();
        }

        public CodexToolWindowViewModel? ViewModel => (Content as System.Windows.Controls.Control)?.DataContext as CodexToolWindowViewModel;

        public void SetPrompt(string prompt)
        {
            if (ViewModel != null)
            {
                ViewModel.Prompt = prompt;
                ViewModel.Status = "Prepared Codex assistant prompt";
            }
        }

        public static async Task<CodexToolWindowPane?> ShowWithPromptAsync(AsyncPackage package, string prompt)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var window = await package.ShowToolWindowAsync(typeof(CodexToolWindowPane), 0, true, package.DisposalToken).ConfigureAwait(true) as CodexToolWindowPane;
            window?.SetPrompt(prompt);
            return window;
        }
    }
}
