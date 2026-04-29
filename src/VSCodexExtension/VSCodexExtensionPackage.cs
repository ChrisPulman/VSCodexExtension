using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using VSCodexExtension.Commands;
using VSCodexExtension.ToolWindows;

namespace VSCodexExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Codex for Visual Studio - Reactive", "OpenAI Codex tool window with ReactiveUI, skills, MCP, and memory", "0.1.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(CodexToolWindowPane), Style = VsDockStyle.Tabbed, Window = EnvDTE.Constants.vsWindowKindOutput)]
    [Guid(PackageGuidString)]
    public sealed class VSCodexExtensionPackage : AsyncPackage
    {
        public const string PackageGuidString = "cc277233-b28f-43d6-a597-1cc515cb0110";
        protected override async System.Threading.Tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await OpenCodexToolWindowCommand.InitializeAsync(this).ConfigureAwait(true);
        }
    }
}
