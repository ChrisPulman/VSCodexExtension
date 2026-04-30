using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using VSCodexExtension.Commands;
using VSCodexExtension.Options;
using VSCodexExtension.ToolWindows;

namespace VSCodexExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Codex for Visual Studio - Reactive", "OpenAI Codex tool window with ReactiveUI, skills, MCP, and memory", "0.1.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(CodexToolWindowPane), Style = VsDockStyle.Tabbed, Window = EnvDTE.Constants.vsWindowKindOutput)]
    [ProvideOptionPage(typeof(CodexOptionsPage), "Codex", "General", 0, 0, true)]
    [ProvideProfile(typeof(CodexOptionsPage), "Codex", "General", 0, 0, true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class VSCodexExtensionPackage : AsyncPackage
    {
        public const string PackageGuidString = "cc277233-b28f-43d6-a597-1cc515cb0110";
        private const string SettingsCollection = "VSCodexExtension";
        private const string FirstLaunchToolWindowOpened = "FirstLaunchToolWindowOpened";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await OpenCodexToolWindowCommand.InitializeAsync(this).ConfigureAwait(true);
            await ShowToolWindowOnFirstLaunchAsync(cancellationToken).ConfigureAwait(true);
        }

        public void ShowCodexOptionsPage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ShowOptionPage(typeof(CodexOptionsPage));
        }

        private async Task ShowToolWindowOnFirstLaunchAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                var settingsStore = GetWritableUserSettingsStore();
                if (HasOpenedToolWindowOnFirstLaunch(settingsStore))
                {
                    return;
                }

                var window = await ShowToolWindowAsync(typeof(CodexToolWindowPane), 0, true, DisposalToken).ConfigureAwait(true);
                if (window == null || window.Frame == null)
                {
                    throw new NotSupportedException("Cannot create Codex tool window on first launch.");
                }

                MarkToolWindowOpenedOnFirstLaunch(settingsStore);
            }
            catch (Exception ex)
            {
                ActivityLog.TryLogError(nameof(VSCodexExtensionPackage), ex.ToString());
            }
        }

        private WritableSettingsStore GetWritableUserSettingsStore()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var settingsManager = new ShellSettingsManager(this);
            return settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
        }

        private static bool HasOpenedToolWindowOnFirstLaunch(WritableSettingsStore settingsStore)
        {
            return settingsStore.CollectionExists(SettingsCollection)
                && settingsStore.PropertyExists(SettingsCollection, FirstLaunchToolWindowOpened)
                && settingsStore.GetBoolean(SettingsCollection, FirstLaunchToolWindowOpened);
        }

        private static void MarkToolWindowOpenedOnFirstLaunch(WritableSettingsStore settingsStore)
        {
            if (!settingsStore.CollectionExists(SettingsCollection))
            {
                settingsStore.CreateCollection(SettingsCollection);
            }

            settingsStore.SetBoolean(SettingsCollection, FirstLaunchToolWindowOpened, true);
        }
    }
}
