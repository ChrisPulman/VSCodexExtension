using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using VSCodex.Commands;
using VSCodex.Infrastructure;
using VSCodex.Options;
using VSCodex.ToolWindows;

namespace VSCodex;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("VSCodex", "VSCodex tool window with ReactiveUI, skills, MCP, and memory", "0.4.1")]
[ProvideMenuResource("Menus.ctmenu", 5)]
[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "VSCodex", "General", 0, 0, true)]
[ProvideProfile(typeof(OptionsProvider.GeneralOptions), "VSCodex", "General", 0, 0, true)]
[ProvideToolWindow(typeof(VSCodexToolWindowPane), Style = VsDockStyle.Tabbed, Window = EnvDTE.Constants.vsWindowKindOutput)]
[ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
[Guid(PackageGuidString)]
public sealed class VSCodexPackage : AsyncPackage
{
    public const string PackageGuidString = "cc277233-b28f-43d6-a597-1cc515cb0110";
    private const string SettingsCollection = "VSCodex";
    private const string FirstLaunchToolWindowOpened = "FirstLaunchToolWindowOpenedV8";
    private static readonly string[] FirstLaunchToolWindowOpenedKeys =
    {
        FirstLaunchToolWindowOpened,
        "FirstLaunchToolWindowOpenedV7",
        "FirstLaunchToolWindowOpenedV6",
        "FirstLaunchToolWindowOpenedV5",
        "FirstLaunchToolWindowOpenedV4",
        "FirstLaunchToolWindowOpenedV3",
        "FirstLaunchToolWindowOpenedV2",
        "FirstLaunchToolWindowOpened"
    };
    private Services.SolutionLoadMonitorService? _solutionLoadMonitor;

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await OpenVSCodexToolWindowCommand.InitializeAsync(this).ConfigureAwait(true);
        ScheduleReactiveMemoryProjectMinerInitialization();
        ScheduleMenuInitialization();
        ScheduleShowToolWindowOnFirstLaunch();
    }

    private void ScheduleReactiveMemoryProjectMinerInitialization()
    {
        JoinableTaskFactory.RunAsync(async () =>
        {
            try
            {
                await WaitForShellInitializedAsync(DisposalToken).ConfigureAwait(false);
                await InitializeReactiveMemoryProjectMinerAsync(DisposalToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (DisposalToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                ActivityLog.TryLogError(nameof(VSCodexPackage), ex.ToString());
            }
        }).Task.FireAndForget();
    }

    private async Task InitializeReactiveMemoryProjectMinerAsync(CancellationToken cancellationToken)
    {
        var app = RxAppBuilder.CreateVisualStudioDefault(this, JoinableTaskFactory).Build();
        _solutionLoadMonitor = new Services.SolutionLoadMonitorService(
            this,
            JoinableTaskFactory,
            app.Get<Services.IMcpConfigService>(),
            app.Get<Services.IReactiveMemoryService>());
        await _solutionLoadMonitor.InitializeAsync(cancellationToken).ConfigureAwait(true);
    }

    private void ScheduleMenuInitialization()
    {
        JoinableTaskFactory.RunAsync(async () =>
        {
            try
            {
                await WaitForShellInitializedAsync(DisposalToken).ConfigureAwait(false);
                await Services.VisualStudioMenuIntegrationService.InitializeAsync(this).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (DisposalToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                ActivityLog.TryLogError(nameof(VSCodexPackage), ex.ToString());
            }
        }).Task.FireAndForget();
    }

    /// <summary>
    /// Waits until <see cref="KnownUIContexts.ShellInitializedContext"/> is active, which is
    /// the VS-guaranteed signal that the main window is fully rendered and all command bars
    /// (including "MenuBar") are populated.  Falls back to the zombie-poll once the context
    /// is active so both conditions are satisfied before we touch the DTE command bars.
    /// </summary>
    private async Task WaitForShellInitializedAsync(CancellationToken cancellationToken)
    {
        // Switch to the UI thread so we can read UIContext state.
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        // ShellInitializedContext becomes active after the VS main window is shown and all
        // built-in command bars have been created – exactly the point at which DTE.CommandBars
        // contains "MenuBar".
        if (!KnownUIContexts.ShellInitializedContext.IsActive)
        {
            var tcs = new TaskCompletionSource<bool>();
            KnownUIContexts.ShellInitializedContext.WhenActivated(() => tcs.TrySetResult(true));

            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                // Release the UI thread while we wait so VS can finish initialising.
                await tcs.Task.ConfigureAwait(false);
            }

            // Return to the UI thread for the zombie check.
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        }

        // Belt-and-suspenders: also wait out the zombie flag in case the two signals
        // don't coincide on all VS versions / configurations.
        await WaitForShellReadyAsync(cancellationToken).ConfigureAwait(false);
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
                ActivityLog.TryLogError(nameof(VSCodexPackage), ex.ToString());
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
        if (!settingsStore.CollectionExists(SettingsCollection))
        {
            return false;
        }

        return FirstLaunchToolWindowOpenedKeys.Any(key =>
            settingsStore.PropertyExists(SettingsCollection, key)
            && settingsStore.GetBoolean(SettingsCollection, key));
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
