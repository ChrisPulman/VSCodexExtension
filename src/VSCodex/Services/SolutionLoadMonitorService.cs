// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using VSCodex.Infrastructure;
using VSCodex.Models;

namespace VSCodex.Services;

/// <summary>Provides the solution Load Monitor Service implementation.</summary>
/// <param name="package">The package.</param>
/// <param name="joinableTaskFactory">The joinable task factory.</param>
/// <param name="mcpConfig">The MCP configuration service.</param>
/// <param name="reactiveMemory">The Reactive Memory service.</param>
public sealed class SolutionLoadMonitorService(
    AsyncPackage package,
    JoinableTaskFactory joinableTaskFactory,
    IMcpConfigService mcpConfig,
    IReactiveMemoryService reactiveMemory) : IDisposable
{
    /// <summary>Named number used by this type.</summary>
    private const int Numeric12 = 12;

    /// <summary>Named number used by this type.</summary>
    private const int Numeric2 = 2;

    /// <summary>Stores the automatic Scan Delay.</summary>
    private static readonly TimeSpan AutomaticScanDelay = TimeSpan.FromMinutes(10);

    /// <summary>Stores the retry Delay.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    /// <summary>Stores the package.</summary>
    private readonly AsyncPackage _package = package;

    /// <summary>Stores the joinable Task Factory.</summary>
    private readonly JoinableTaskFactory _joinableTaskFactory = joinableTaskFactory;

    /// <summary>Stores the mcp Config.</summary>
    private readonly IMcpConfigService _mcpConfig = mcpConfig;

    /// <summary>Stores the reactive Memory.</summary>
    private readonly IReactiveMemoryService _reactiveMemory = reactiveMemory;

    /// <summary>Stores the solution.</summary>
    private IVsSolution? _solution;

    /// <summary>Indicates whether managed solution events are subscribed.</summary>
    private bool _isSubscribed;

    /// <summary>Stores the last Queued Workspace Id.</summary>
    private string _lastQueuedWorkspaceId = string.Empty;

    /// <summary>Stores the scan Retry Count.</summary>
    private int _scanRetryCount;

    /// <summary>Stores the scan In Progress.</summary>
    private int _scanInProgress;

    /// <summary>Initializes the operation.</summary>
    /// <param name="cancellationToken">The cancellation Token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        _solution = await _package.GetServiceAsync(typeof(SVsSolution)).ConfigureAwait(true) as IVsSolution;
        if (_solution is null)
        {
            _ = ActivityLog.TryLogWarning(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner monitor could not get SVsSolution.");
            return;
        }

        SubscribeToSolutionEvents();
        _ = ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner monitor registered for Visual Studio solution events.");
        var solutionPath = TryGetSolutionPath();
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return;
        }

        QueueProjectMinerScan("package startup idle check", solutionPath, AutomaticScanDelay);
    }

    /// <summary>Performs the dispose operation.</summary>
    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (!_isSubscribed)
        {
            return;
        }

        SolutionEvents.OnAfterOpenSolution -= OnAfterOpenSolution;
        SolutionEvents.OnBeforeCloseSolution -= OnSolutionClosed;
        SolutionEvents.OnAfterCloseSolution -= OnSolutionClosed;
        _isSubscribed = false;
    }

    /// <summary>Handles the after Open Solution event.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The event arguments.</param>
    private void OnAfterOpenSolution(object? sender, EventArgs eventArgs)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _scanRetryCount = 0;
        QueueProjectMinerScan("solution opened", TryGetSolutionPath(), AutomaticScanDelay);
    }

    /// <summary>Handles a solution closing or closed event.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The event arguments.</param>
    private void OnSolutionClosed(object? sender, EventArgs eventArgs)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _lastQueuedWorkspaceId = string.Empty;
        _scanRetryCount = 0;
    }

    /// <summary>Subscribes to the managed Visual Studio solution event source.</summary>
    private void SubscribeToSolutionEvents()
    {
        if (_isSubscribed)
        {
            return;
        }

        SolutionEvents.OnAfterOpenSolution += OnAfterOpenSolution;
        SolutionEvents.OnBeforeCloseSolution += OnSolutionClosed;
        SolutionEvents.OnAfterCloseSolution += OnSolutionClosed;
        _isSubscribed = true;
    }

    /// <summary>Performs the queue Project Miner Scan operation.</summary>
    /// <param name="reason">The reason.</param>
    /// <param name="solutionPath">The solution Path.</param>
    /// <param name="delay">The delay.</param>
    private void QueueProjectMinerScan(string reason, string solutionPath, TimeSpan? delay = null)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            _ = ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), $"ReactiveMemory ProjectMiner scan skipped because no solution path was available ({reason}).");
            return;
        }

        _ = ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), $"ReactiveMemory ProjectMiner scan queued ({reason}).");
        TaskObserver.FireAndForget(Task.Run(() => RunProjectMinerScanAsync(reason, solutionPath, delay ?? AutomaticScanDelay)));
    }

    /// <summary>Runs the queued Project Miner scan.</summary>
    /// <param name="reason">The reason.</param>
    /// <param name="solutionPath">The solution path.</param>
    /// <param name="delay">The delay before scanning.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task RunProjectMinerScanAsync(string reason, string solutionPath, TimeSpan delay)
    {
        var acquiredScanSlot = false;
        try
        {
            await Task.Delay(delay, _package.DisposalToken).ConfigureAwait(false);
            if (Interlocked.Exchange(ref _scanInProgress, 1) == 1)
            {
                _ = ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), $"ReactiveMemory ProjectMiner scan skipped because another scan is already running ({reason}).");
                return;
            }

            acquiredScanSlot = true;
            await ScanWorkspaceAsync(reason, solutionPath).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_package.DisposalToken.IsCancellationRequested)
        {
            _ = ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), "ReactiveMemory ProjectMiner scan cancelled during package disposal.");
        }
        catch (Exception ex)
        {
            _ = ActivityLog.TryLogWarning(nameof(SolutionLoadMonitorService), $"ReactiveMemory ProjectMiner scan failed: {ex}");
        }
        finally
        {
            if (acquiredScanSlot)
            {
                _ = Interlocked.Exchange(ref _scanInProgress, 0);
            }
        }
    }

    /// <summary>Scans the workspace identified by the solution path.</summary>
    /// <param name="reason">The reason.</param>
    /// <param name="solutionPath">The solution path.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ScanWorkspaceAsync(string reason, string solutionPath)
    {
        var identity = BuildWorkspaceIdentityFromSolutionPath(solutionPath);
        if (identity is null || string.IsNullOrWhiteSpace(identity.Id) || string.IsNullOrWhiteSpace(identity.RootPath))
        {
            QueueRetryForUnavailableWorkspaceIdentity(reason, solutionPath);
            return;
        }

        if (string.Equals(_lastQueuedWorkspaceId, identity.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _mcpConfig.Refresh();
        var result = await _reactiveMemory.ScanWorkspaceAsync(identity, automatic: true).ConfigureAwait(false);
        if (result.Success)
        {
            _lastQueuedWorkspaceId = identity.Id;
            _scanRetryCount = 0;
            _ = ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), $"{result.Message} ({reason})");
            return;
        }

        _ = ActivityLog.TryLogWarning(nameof(SolutionLoadMonitorService), $"{result.Message} ({reason})");
        QueueRetryAfterFailedScan(reason, solutionPath);
    }

    /// <summary>Queues a retry after a workspace identity could not be established.</summary>
    /// <param name="reason">The reason.</param>
    /// <param name="solutionPath">The solution path.</param>
    private void QueueRetryForUnavailableWorkspaceIdentity(string reason, string solutionPath)
    {
        if (_scanRetryCount >= 1)
        {
            return;
        }

        _scanRetryCount++;
        _ = ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), $"ReactiveMemory ProjectMiner scan is waiting for the Visual Studio workspace identity ({reason}).");
        QueueProjectMinerScan($"retry {_scanRetryCount} after workspace identity was unavailable for {reason}", solutionPath, RetryDelay);
    }

    /// <summary>Queues a retry after an unsuccessful scan.</summary>
    /// <param name="reason">The reason.</param>
    /// <param name="solutionPath">The solution path.</param>
    private void QueueRetryAfterFailedScan(string reason, string solutionPath)
    {
        if (_scanRetryCount >= 1)
        {
            return;
        }

        _scanRetryCount++;
        QueueProjectMinerScan($"retry {_scanRetryCount} after {reason}", solutionPath, RetryDelay);
    }

    /// <summary>Attempts to get Solution Path.</summary>
    /// <returns>The try Get Solution Path result.</returns>
    private string TryGetSolutionPath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            if (_solution is not null && ErrorHandler.Succeeded(_solution.GetSolutionInfo(out var directory, out var file, out _)))
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
            _ = ActivityLog.TryLogWarning(nameof(SolutionLoadMonitorService), $"Could not capture solution path for ReactiveMemory scan: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>Builds workspace Identity From Solution Path.</summary>
    /// <param name="solutionPath">The solution Path.</param>
    /// <returns>The build Workspace Identity From Solution Path result.</returns>
    private WorkspaceIdentity? BuildWorkspaceIdentityFromSolutionPath(string solutionPath)
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
        var id = ComputeWorkspaceIdentityId(repositoryRemote, root);

        return new WorkspaceIdentity
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "VSCodex workspace" : name,
            RootPath = root,
            SolutionPath = solutionPath,
            SolutionRelativePath = solutionRelativePath,
            RepositoryRemote = repositoryRemote ?? string.Empty,
            MemoryRoot = BuildWorkspaceMemoryRoot(id)
        };
    }

    /// <summary>Finds repository Root.</summary>
    /// <param name="startDirectory">The start Directory.</param>
    /// <returns>The find Repository Root result.</returns>
    private string? FindRepositoryRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
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

    /// <summary>Performs the make Relative If Contained operation.</summary>
    /// <param name="root">The root.</param>
    /// <param name="path">The path.</param>
    /// <returns>The make Relative If Contained result.</returns>
    private string MakeRelativeIfContained(string root, string path)
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
        catch (Exception ex)
        {
            _ = ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), $"Could not make the solution path relative to the repository root: {ex.Message}");
        }

        return Path.GetFileName(path);
    }

    /// <summary>Performs the append Slash operation.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The append Slash result.</returns>
    private string AppendSlash(string path)
        => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? path : path + Path.DirectorySeparatorChar;

    /// <summary>Reads repository Remote.</summary>
    /// <param name="root">The root.</param>
    /// <returns>The read Repository Remote result.</returns>
    private string ReadRepositoryRemote(string root)
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
                    var parts = line.Split(['='], Numeric2);
                    if (parts.Length == Numeric2)
                    {
                        return parts[1].Trim();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _ = ActivityLog.TryLogInformation(nameof(SolutionLoadMonitorService), $"Could not read the Git remote configuration: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>Computes workspace Identity Id.</summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The compute Workspace Identity Id result.</returns>
    private string ComputeWorkspaceIdentityId(params string[] parts)
    {
        var key = string.Join("|", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(NormalizeIdentityPart));
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(key)), Numeric12);
    }

    /// <summary>Builds workspace Memory Root.</summary>
    /// <param name="workspaceIdentityId">The workspace Identity Id.</param>
    /// <returns>The build Workspace Memory Root result.</returns>
    private string BuildWorkspaceMemoryRoot(string workspaceIdentityId)
        => string.IsNullOrWhiteSpace(workspaceIdentityId) ? string.Empty : $"reactivememory://workspace/{workspaceIdentityId}";

    /// <summary>Performs the normalize Identity Part operation.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The normalize Identity Part result.</returns>
    private string NormalizeIdentityPart(string value)
        => value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim().ToLowerInvariant();

    /// <summary>Performs the to Hex operation.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="byteCount">The byte Count.</param>
    /// <returns>The to Hex result.</returns>
    private string ToHex(byte[] bytes, int byteCount)
    {
        var builder = new StringBuilder(byteCount * Numeric2);
        for (var i = 0; i < Math.Min(bytes.Length, byteCount); i++)
        {
            _ = builder.Append(bytes[i].ToString("x2"));
        }

        return builder.ToString();
    }
}
