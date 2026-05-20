using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

public sealed class SolutionLoadMonitorService : IVsSolutionEvents, IDisposable
{
    private static readonly TimeSpan AutomaticScanDelay = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);
    private readonly AsyncPackage _package;
    private readonly JoinableTaskFactory _joinableTaskFactory;
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
        IMcpConfigService mcpConfig,
        IReactiveMemoryService reactiveMemory)
    {
        _package = package;
        _joinableTaskFactory = joinableTaskFactory;
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
            var solutionPath = TryGetSolutionPath();
            if (!string.IsNullOrWhiteSpace(solutionPath))
            {
                QueueProjectMinerScan("package startup idle check", solutionPath, AutomaticScanDelay);
            }
        }
        else
        {
            ActivityLog.TryLogWarning(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner monitor could not register Visual Studio solution events.");
        }
    }

    public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _scanRetryCount = 0;
        QueueProjectMinerScan("solution opened", TryGetSolutionPath(), AutomaticScanDelay);
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

    private void QueueProjectMinerScan(string reason, string solutionPath, TimeSpan? delay = null)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner scan skipped because no solution path was available (" + reason + ").");
            return;
        }

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
                var identity = BuildWorkspaceIdentityFromSolutionPath(solutionPath);
                if (identity == null || string.IsNullOrWhiteSpace(identity.Id) || string.IsNullOrWhiteSpace(identity.RootPath))
                {
                    if (_scanRetryCount < 1)
                    {
                        _scanRetryCount++;
                        ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner scan is waiting for the Visual Studio workspace identity (" + reason + ").");
                        QueueProjectMinerScan("retry " + _scanRetryCount + " after workspace identity was unavailable for " + reason, solutionPath, RetryDelay);
                    }

                    return;
                }

                if (string.Equals(_lastQueuedWorkspaceId, identity.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _mcpConfig.Refresh();
                var scanIdentity = identity;
                var result = await _reactiveMemory.ScanWorkspaceAsync(scanIdentity, automatic: true).ConfigureAwait(false);
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
                        QueueProjectMinerScan("retry " + _scanRetryCount + " after " + reason, solutionPath, RetryDelay);
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

    private string TryGetSolutionPath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            if (_solution != null && ErrorHandler.Succeeded(_solution.GetSolutionInfo(out var directory, out var file, out _)))
            {
                if (!string.IsNullOrWhiteSpace(file) && Path.IsPathRooted(file))
                {
                    return file;
                }

                if (!string.IsNullOrWhiteSpace(directory) && !string.IsNullOrWhiteSpace(file))
                {
                    return Path.Combine(directory, file);
                }
            }
        }
        catch (Exception ex)
        {
            ActivityLog.TryLogWarning(nameof(SolutionLoadMonitorService), "Could not capture solution path for ReactiveMemory scan: " + ex.Message);
        }

        return string.Empty;
    }

    private static WorkspaceIdentity? BuildWorkspaceIdentityFromSolutionPath(string solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return null;
        }

        var solutionDirectory = Path.GetDirectoryName(solutionPath);
        if (string.IsNullOrWhiteSpace(solutionDirectory) || !Directory.Exists(solutionDirectory))
        {
            return null;
        }

        var root = FindRepositoryRoot(solutionDirectory) ?? solutionDirectory;
        var solutionRelativePath = MakeRelativeIfContained(root, solutionPath);
        var repositoryRemote = ReadRepositoryRemote(root);
        var name = string.IsNullOrWhiteSpace(root)
            ? Path.GetFileNameWithoutExtension(solutionPath)
            : Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var id = Sha256((repositoryRemote ?? string.Empty) + "|" + root + "|" + solutionRelativePath + "|" + solutionPath);

        return new WorkspaceIdentity
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "VSCodex workspace" : name,
            RootPath = root,
            SolutionPath = solutionPath,
            SolutionRelativePath = solutionRelativePath,
            RepositoryRemote = repositoryRemote ?? string.Empty,
            MemoryRoot = string.Empty
        };
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current != null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string MakeRelativeIfContained(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var rootUri = new Uri(AppendSlash(root));
            var pathUri = new Uri(path);
            if (rootUri.IsBaseOf(pathUri))
            {
                return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
        }
        catch
        {
        }

        return Path.GetFileName(path);
    }

    private static string AppendSlash(string path)
        => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? path : path + Path.DirectorySeparatorChar;

    private static string ReadRepositoryRemote(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return string.Empty;
        }

        try
        {
            var configPath = Path.Combine(root, ".git", "config");
            if (!File.Exists(configPath))
            {
                return string.Empty;
            }

            var lines = File.ReadAllLines(configPath);
            var inOrigin = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    inOrigin = line.IndexOf("remote \"origin\"", StringComparison.OrdinalIgnoreCase) >= 0;
                    continue;
                }

                if (inOrigin && line.StartsWith("url", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        return parts[1].Trim();
                    }
                }
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static string Sha256(string value)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)).Select(b => b.ToString("x2")));
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
