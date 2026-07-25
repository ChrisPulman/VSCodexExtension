// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace VSCodex.Infrastructure;

/// <summary>Observes fire-and-forget tasks so their failures are consumed safely.</summary>
internal static class TaskObserver
{
    /// <summary>Performs the fire And Forget operation.</summary>
    /// <param name="task">The task.</param>
    internal static void FireAndForget(Task task)
    {
        _ = task.ContinueWith(
            t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
