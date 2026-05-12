using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using VSCodex.Infrastructure;

namespace VSCodex.Services;

public sealed class SolutionLoadMonitorService : IVsSolutionEvents, IDisposable
{
    private static readonly TimeSpan AutomaticScanDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);
    private readonly AsyncPackage _package;
    private readonly JoinableTaskFactory _joinableTaskFactory;
    private readonly IWorkspaceContextService _workspace;
    private readonly IMcpConfigService _mcpConfig;
    private readonly IReactiveMemoryService _reactiveMemory;
    private IVsSolution? _solution;
    private uint _solutionEventsCookie;
    private string _lastQueuedWorkspaceId = string.Empty;
    private int _scanRetryCount;
    private int _scanInProgress;

    public SolutionLoadMonitorService(
        AsyncPackage package,
        JoinableTaskFactory joinableTaskFactory,
        IWorkspaceContextService workspace,
        IMcpConfigService mcpConfig,
        IReactiveMemoryService reactiveMemory)
    {
        _package = package;
        _joinableTaskFactory = joinableTaskFactory;
        _workspace = workspace;
        _mcpConfig = mcpConfig;
        _reactiveMemory = reactiveMemory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        _solution = await _package.GetServiceAsync(typeof(SVsSolution)).ConfigureAwait(true) as IVsSolution;
        if (_solution == null)
        {
            ActivityLog.TryLogWarning(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner monitor could not get SVsSolution.");
            return;
        }

        if (ErrorHandler.Succeeded(_solution.AdviseSolutionEvents(this, out _solutionEventsCookie)))
        {
            ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner monitor registered for Visual Studio solution events.");
            QueueProjectMinerScan("package startup idle check", AutomaticScanDelay);
        }
        else
        {
            ActivityLog.TryLogWarning(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner monitor could not register Visual Studio solution events.");
        }
    }

    public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
    {
        _scanRetryCount = 0;
        QueueProjectMinerScan("solution opened", AutomaticScanDelay);
        return VSConstants.S_OK;
    }

    public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
    {
        return VSConstants.S_OK;
    }

    public int OnBeforeCloseSolution(object pUnkReserved)
    {
        _lastQueuedWorkspaceId = string.Empty;
        _scanRetryCount = 0;
        return VSConstants.S_OK;
    }

    public int OnAfterCloseSolution(object pUnkReserved)
    {
        _lastQueuedWorkspaceId = string.Empty;
        _scanRetryCount = 0;
        return VSConstants.S_OK;
    }

    public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.S_OK;
    public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;
    public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;
    public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;
    public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;
    public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;

    private void QueueProjectMinerScan(string reason, TimeSpan? delay = null)
    {
        ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner scan queued (" + reason + ").");
        Task.Run(async () =>
        {
            var acquiredScanSlot = false;
            try
            {
                await Task.Delay(delay ?? AutomaticScanDelay, _package.DisposalToken).ConfigureAwait(false);
                if (Interlocked.Exchange(ref _scanInProgress, 1) == 1)
                {
                    ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner scan skipped because another scan is already running (" + reason + ").");
                    return;
                }

                acquiredScanSlot = true;
                await _joinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
                _workspace.RefreshWorkspaceIdentity();
                var identity = _workspace.CurrentWorkspaceIdentity;
                if (identity == null || string.IsNullOrWhiteSpace(identity.Id) || string.IsNullOrWhiteSpace(identity.RootPath))
                {
                    if (_scanRetryCount < 1)
                    {
                        _scanRetryCount++;
                        ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner scan is waiting for the Visual Studio workspace identity (" + reason + ").");
                        QueueProjectMinerScan("retry " + _scanRetryCount + " after workspace identity was unavailable for " + reason, RetryDelay);
                    }

                    return;
                }

                if (string.Equals(_lastQueuedWorkspaceId, identity.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _mcpConfig.Refresh();
                var scanIdentity = identity;
                var result = await Task.Run(async () => await _reactiveMemory.ScanWorkspaceAsync(scanIdentity, automatic: true).ConfigureAwait(false), _package.DisposalToken).ConfigureAwait(false);
                if (result.Success)
                {
                    _lastQueuedWorkspaceId = identity.Id;
                    _scanRetryCount = 0;
                    ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), result.Message + " (" + reason + ")");
                }
                else
                {
                    ActivityLog.TryLogWarning(nameof(SolutionLoadMonitorService), result.Message + " (" + reason + ")");
                    if (_scanRetryCount < 1)
                    {
                        _scanRetryCount++;
                        QueueProjectMinerScan("retry " + _scanRetryCount + " after " + reason, RetryDelay);
                    }
                }
            }
            catch (OperationCanceledException) when (_package.DisposalToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                ActivityLog.TryLogWarning(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner scan failed: " + ex);
            }
            finally
            {
                if (acquiredScanSlot)
                {
                    Interlocked.Exchange(ref _scanInProgress, 0);
                }
            }
        }).FireAndForget();
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_solution != null && _solutionEventsCookie != 0)
        {
            _solution.UnadviseSolutionEvents(_solutionEventsCookie);
            _solutionEventsCookie = 0;
        }
    }
}
