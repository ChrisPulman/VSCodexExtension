using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using VSCodex.Infrastructure;
using VSCodex.ViewModels;
using VSCodex.Controls;

namespace VSCodex.ToolWindows;

[Guid("ee7f4f9f-8f35-46cb-9a77-a09e33f60b60")]
public sealed class VSCodexToolWindowPane : ToolWindowPane
{
    public VSCodexToolWindowPane() : base(null)
    {
        Caption = "VSCodex";
        try
        {
            var app = RxAppBuilder.CreateVisualStudioDefault(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider, ThreadHelper.JoinableTaskFactory).Build();
            Content = app.CreateToolWindowControl();
        }
        catch (System.Exception ex)
        {
            ActivityLog.TryLogError(nameof(VSCodexToolWindowPane), ex.ToString());
            Content = new VSCodexToolWindowFallbackControl(ex);
        }
    }

    public VSCodexToolWindowViewModel? ViewModel => (Content as System.Windows.Controls.Control)?.DataContext as VSCodexToolWindowViewModel;

    public void SetPrompt(string prompt)
    {
        if (ViewModel != null)
        {
            ViewModel.Prompt = prompt;
            ViewModel.Status = "Prepared VSCodex assistant prompt";
        }
    }

    public void ShowSettings() => ViewModel?.ShowSettings();

    public static async Task<VSCodexToolWindowPane?> ShowWithPromptAsync(AsyncPackage package, string prompt)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        var window = await package.ShowToolWindowAsync(typeof(VSCodexToolWindowPane), 0, true, package.DisposalToken).ConfigureAwait(true) as VSCodexToolWindowPane;
        window?.SetPrompt(prompt);
        return window;
    }

    public static async Task<VSCodexToolWindowPane?> ShowSettingsAsync(AsyncPackage package)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        var window = await package.ShowToolWindowAsync(typeof(VSCodexToolWindowPane), 0, true, package.DisposalToken).ConfigureAwait(true) as VSCodexToolWindowPane;
        window?.ShowSettings();
        return window;
    }
}
