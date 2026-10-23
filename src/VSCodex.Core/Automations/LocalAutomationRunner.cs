// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VSCodex.Core.Automations;

/// <summary>Executes due jobs without overlapping ticks and coalesces missed intervals.</summary>
/// <param name="clock">The UTC clock.</param>
/// <param name="execute">The isolated execution adapter.</param>
/// <param name="coordinator">The cross-process claim and completion coordinator.</param>
public sealed class LocalAutomationRunner(Func<DateTimeOffset> clock, Func<LocalAutomation, CancellationToken, Task<string>> execute, ILocalAutomationCoordinator coordinator)
{
    /// <summary>Defines the maximum runtime for a local job.</summary>
    private const int TimeoutMinutes = 15;

    /// <summary>Allows a stopped owner time to finish process cleanup.</summary>
    private const int LeaseMarginMinutes = 1;

    /// <summary>Bounds completion merge retries when another process briefly owns state.</summary>
    private const int CompletionAttempts = 10;

    /// <summary>Defines the nonblocking completion retry delay.</summary>
    private const int CompletionRetryMilliseconds = 100;

    /// <summary>Prevents concurrent scheduler passes.</summary>
    private int _running;

    /// <summary>Runs at most one execution per overdue job, checkpointing before process launch.</summary>
    /// <param name="jobs">The immutable job collection snapshot.</param>
    /// <param name="cancellationToken">The host lifetime cancellation.</param>
    /// <returns>The completion task.</returns>
    public async Task TickAsync(IEnumerable<LocalAutomation> jobs, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _running, 1) != 0)
        {
            return;
        }

        try
        {
            foreach (LocalAutomation job in jobs)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                using LocalAutomationClaim? claim = coordinator.TryClaim(job.Id, clock(), TimeSpan.FromMinutes(TimeoutMinutes + LeaseMarginMinutes));
                if (claim is not null)
                {
                    await RunAsync(claim.Job, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _ = Interlocked.Exchange(ref _running, 0);
        }
    }

    /// <summary>Checkpoints and executes one due job with a bounded lifetime.</summary>
    /// <param name="job">The due job.</param>
    /// <param name="cancellationToken">The host cancellation.</param>
    /// <returns>The execution completion.</returns>
    private async Task RunAsync(LocalAutomation job, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(TimeoutMinutes));
        try
        {
            job.LastResult = await execute(job, timeout.Token).ConfigureAwait(false);
            job.LastFailed = false;
        }
        catch (Exception exception)
        {
            job.LastResult = exception is OperationCanceledException ? "Automation cancelled or timed out." : exception.Message;
            job.LastFailed = true;
        }

        job.LastCompleted = clock();
        job.Unread = true;
        await CompleteAsync(job).ConfigureAwait(false);
    }

    /// <summary>Retries a short-lived state lease without blocking the UI thread.</summary>
    /// <param name="job">The completed claim snapshot.</param>
    /// <returns>The completion merge task.</returns>
    private async Task CompleteAsync(LocalAutomation job)
    {
        for (int attempt = 0; attempt < CompletionAttempts; attempt++)
        {
            if (coordinator.TryComplete(job))
            {
                return;
            }

            await Task.Delay(CompletionRetryMilliseconds).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Automation result could not be saved because another Visual Studio instance kept its state lease busy.");
    }
}
