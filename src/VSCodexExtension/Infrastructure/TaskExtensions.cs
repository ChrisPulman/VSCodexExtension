using System.Threading.Tasks;
namespace VSCodexExtension.Infrastructure
{
    internal static class TaskExtensions
    {
        public static async void FireAndForget(this Task task)
        {
            try { await task.ConfigureAwait(false); } catch { }
        }
    }
}
