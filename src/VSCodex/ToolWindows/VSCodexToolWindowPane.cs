// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using VSCodex.Controls;
using VSCodex.Infrastructure;
using VSCodex.ViewModels;

namespace VSCodex.ToolWindows;

/// <summary>Provides the vS Codex Tool Window Pane implementation.</summary>
[Guid("ee7f4f9f-8f35-46cb-9a77-a09e33f60b60")]
public sealed class VSCodexToolWindowPane : ToolWindowPane
{
    /// <summary>Initializes a new instance of the <see cref="VSCodexToolWindowPane"/> class.</summary>
    public VSCodexToolWindowPane()
        : base(null)
    {
        Caption = "VSCodex";
        try
        {
            var app = RxAppBuilder.CreateVisualStudioDefault(Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider, ThreadHelper.JoinableTaskFactory).Build();
            Content = app.CreateToolWindowControl();
        }
        catch (Exception ex)
        {
            _ = ActivityLog.TryLogError(nameof(VSCodexToolWindowPane), ex.ToString());
            Content = new VSCodexToolWindowFallbackControl(ex);
        }
    }

    /// <summary>Gets the view Model.</summary>
    public VSCodexToolWindowViewModel? ViewModel => (Content as System.Windows.Controls.Control)?.DataContext as VSCodexToolWindowViewModel;

    /// <summary>Performs the show With Prompt operation.</summary>
    /// <param name="package">The package.</param>
    /// <param name="prompt">The prompt.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    public static async Task<VSCodexToolWindowPane?> ShowWithPromptAsync(AsyncPackage package, string prompt)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        var window = await package.ShowToolWindowAsync(typeof(VSCodexToolWindowPane), 0, true, package.DisposalToken).ConfigureAwait(true) as VSCodexToolWindowPane;
        window?.SetPrompt(prompt);
        return window;
    }

    /// <summary>Performs the show History operation.</summary>
    /// <param name="package">The package.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    public static async Task<VSCodexToolWindowPane?> ShowHistoryAsync(AsyncPackage package)
    {
        await package.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        var window = await package.ShowToolWindowAsync(typeof(VSCodexToolWindowPane), 0, true, package.DisposalToken).ConfigureAwait(true) as VSCodexToolWindowPane;
        window?.ShowHistory();
        return window;
    }

    /// <summary>Sets prompt.</summary>
    /// <param name="prompt">The prompt.</param>
    public void SetPrompt(string prompt)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.Prompt = prompt;
        ViewModel.Status = "Prepared VSCodex assistant prompt";
    }

    /// <summary>Performs the show History operation.</summary>
    public void ShowHistory() => ViewModel?.ShowHistory();
}
