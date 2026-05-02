using System.Threading;
using System.Threading.Tasks;

namespace VSCodex.Infrastructure;

internal static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        _ = task.ContinueWith(
            t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
