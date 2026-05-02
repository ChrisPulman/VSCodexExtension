using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using VSCodexExtension.Commands;
using VSCodexExtension.Infrastructure;
using VSCodexExtension.ToolWindows;

namespace VSCodexExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("VSCodex", "VSCodex tool window with ReactiveUI, skills, MCP, and memory", "0.1.4")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(VSCodexToolWindowPane), Style = VsDockStyle.Tabbed, Window = EnvDTE.Constants.vsWindowKindOutput)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuidString)]
    public sealed class VSCodexExtensionPackage : AsyncPackage
    {
        public const string PackageGuidString = "cc277233-b28f-43d6-a597-1cc515cb0110";
        private const string SettingsCollection = "VSCodexExtension";
        private const string FirstLaunchToolWindowOpened = "FirstLaunchToolWindowOpenedV5";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await OpenVSCodexToolWindowCommand.InitializeAsync(this).ConfigureAwait(true);
            ScheduleShowToolWindowOnFirstLaunch();
        }

        private void ScheduleShowToolWindowOnFirstLaunch()
        {
            JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await ShowToolWindowOnFirstLaunchAsync(DisposalToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (DisposalToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    ActivityLog.TryLogError(nameof(VSCodexExtensionPackage), ex.ToString());
                }
            }).Task.FireAndForget();
        }

        private async Task ShowToolWindowOnFirstLaunchAsync(CancellationToken cancellationToken)
        {
            await WaitForShellReadyAsync(cancellationToken).ConfigureAwait(false);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var settingsStore = GetWritableUserSettingsStore();
            if (HasOpenedToolWindowOnFirstLaunch(settingsStore))
            {
                return;
            }

            var window = await ShowToolWindowAsync(typeof(VSCodexToolWindowPane), 0, true, DisposalToken).ConfigureAwait(true);
            if (window == null || window.Frame == null)
            {
                throw new NotSupportedException("Cannot create VSCodex tool window on first launch.");
            }

            MarkToolWindowOpenedOnFirstLaunch(settingsStore);
        }

        private async Task WaitForShellReadyAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken).ConfigureAwait(false);

            for (var attempt = 0; attempt < 40; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await IsShellZombieAsync(cancellationToken).ConfigureAwait(true))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<bool> IsShellZombieAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var shell = await GetServiceAsync(typeof(SVsShell)).ConfigureAwait(true) as IVsShell;
            if (shell == null || ErrorHandler.Failed(shell.GetProperty((int)__VSSPROPID.VSSPROPID_Zombie, out var value)))
            {
                return false;
            }

            return value is bool isZombie && isZombie || value is int zombieValue && zombieValue != 0;
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
