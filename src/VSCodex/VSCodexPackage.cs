// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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
using VSCodex.Services;
using VSCodex.ToolWindows;

namespace VSCodex;

/// <summary>Provides the vS Codex Package implementation.</summary>
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
    /// <summary>Defines the package Guid String.</summary>
    internal const string PackageGuidString = "cc277233-b28f-43d6-a597-1cc515cb0110";

    /// <summary>Named number used by this type.</summary>
    private const int Numeric1500 = 1500;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric250 = 250;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric40 = 40;

    /// <summary>Defines the settings Collection.</summary>
    private const string SettingsCollection = "VSCodex";

    /// <summary>Defines the first Launch Tool Window Opened.</summary>
    private const string FirstLaunchToolWindowOpened = "FirstLaunchToolWindowOpenedV8";

    /// <summary>Stores the first Launch Tool Window Opened Keys.</summary>
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

    /// <summary>Stores the solution Load Monitor.</summary>
    private SolutionLoadMonitorService? _solutionLoadMonitor;

    /// <summary>Initializes the operation.</summary>
    /// <param name="cancellationToken">The cancellation Token.</param>
    /// <param name="progress">The progress.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await OpenVSCodexToolWindowCommand.InitializeAsync(this).ConfigureAwait(true);
        ScheduleReactiveMemoryProjectMinerInitialization();
        ScheduleMenuInitialization();
        ScheduleShowToolWindowOnFirstLaunch();
    }

    /// <summary>Determines whether has Opened Tool Window On First Launch.</summary>
    /// <param name="settingsStore">The settings Store.</param>
    /// <returns><see langword="true"/> when has Opened Tool Window On First Launch succeeds; otherwise, <see langword="false"/>.</returns>
    private bool HasOpenedToolWindowOnFirstLaunch(WritableSettingsStore settingsStore)
    {
        return settingsStore.CollectionExists(SettingsCollection)
            && FirstLaunchToolWindowOpenedKeys.Any(key =>
                settingsStore.PropertyExists(SettingsCollection, key)
                && settingsStore.GetBoolean(SettingsCollection, key));
    }

    /// <summary>Performs the mark Tool Window Opened On First Launch operation.</summary>
    /// <param name="settingsStore">The settings Store.</param>
    private void MarkToolWindowOpenedOnFirstLaunch(WritableSettingsStore settingsStore)
    {
        if (!settingsStore.CollectionExists(SettingsCollection))
        {
            settingsStore.CreateCollection(SettingsCollection);
        }

        settingsStore.SetBoolean(SettingsCollection, FirstLaunchToolWindowOpened, true);
    }

    /// <summary>Performs the schedule Reactive Memory Project Miner Initialization operation.</summary>
    private void ScheduleReactiveMemoryProjectMinerInitialization()
    {
        TaskObserver.FireAndForget(JoinableTaskFactory.RunAsync(async () =>
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
                _ = ActivityLog.TryLogError(nameof(VSCodexPackage), ex.ToString());
            }
        }).Task);
    }

    /// <summary>Initializes reactive Memory Project Miner.</summary>
    /// <param name="cancellationToken">The cancellation Token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task InitializeReactiveMemoryProjectMinerAsync(CancellationToken cancellationToken)
    {
        var app = RxAppBuilder.CreateVisualStudioDefault(this, JoinableTaskFactory).Build();
        _solutionLoadMonitor = new(
            this,
            JoinableTaskFactory,
            app.Get<IMcpConfigService>(),
            app.Get<IReactiveMemoryService>());
        await _solutionLoadMonitor.InitializeAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Performs the schedule Menu Initialization operation.</summary>
    private void ScheduleMenuInitialization()
    {
        TaskObserver.FireAndForget(JoinableTaskFactory.RunAsync(async () =>
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
                _ = ActivityLog.TryLogError(nameof(VSCodexPackage), ex.ToString());
            }
        }).Task);
    }

    /// <summary>
    /// Waits until <see cref="KnownUIContexts.ShellInitializedContext"/> is active, which is
    /// the VS-guaranteed signal that the main window is fully rendered and all command bars
    /// (including "MenuBar") are populated.  Falls back to the zombie-poll once the context
    /// is active so both conditions are satisfied before we touch the DTE command bars.
    /// </summary>
    /// <param name="cancellationToken">The cancellation Token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>Performs the schedule Show Tool Window On First Launch operation.</summary>
    private void ScheduleShowToolWindowOnFirstLaunch()
    {
        TaskObserver.FireAndForget(JoinableTaskFactory.RunAsync(async () =>
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
                _ = ActivityLog.TryLogError(nameof(VSCodexPackage), ex.ToString());
            }
        }).Task);
    }

    /// <summary>Performs the show Tool Window On First Launch operation.</summary>
    /// <param name="cancellationToken">The cancellation Token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
        if (window is null || window.Frame is null)
        {
            throw new NotSupportedException("Cannot create VSCodex tool window on first launch.");
        }

        MarkToolWindowOpenedOnFirstLaunch(settingsStore);
    }

    /// <summary>Performs the wait For Shell Ready operation.</summary>
    /// <param name="cancellationToken">The cancellation Token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task WaitForShellReadyAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(Numeric1500), cancellationToken).ConfigureAwait(false);

        for (var attempt = 0; attempt < Numeric40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await IsShellZombieAsync(cancellationToken).ConfigureAwait(true))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(Numeric250), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Determines whether is Shell Zombie.</summary>
    /// <param name="cancellationToken">The cancellation Token.</param>
    /// <returns>A task whose result contains the operation result.</returns>
    private async Task<bool> IsShellZombieAsync(CancellationToken cancellationToken)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        object? service = await GetServiceAsync(typeof(SVsShell)).ConfigureAwait(true);
        return service is IVsShell shell
            && !ErrorHandler.Failed(shell.GetProperty((int)__VSSPROPID.VSSPROPID_Zombie, out var value))
            && ((value is bool isZombie && isZombie) || (value is int zombieValue && zombieValue != 0));
    }

    /// <summary>Gets writable User Settings Store.</summary>
    /// <returns>The get Writable User Settings Store result.</returns>
    private WritableSettingsStore GetWritableUserSettingsStore()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var settingsManager = new ShellSettingsManager(this);
        return settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
    }
}
