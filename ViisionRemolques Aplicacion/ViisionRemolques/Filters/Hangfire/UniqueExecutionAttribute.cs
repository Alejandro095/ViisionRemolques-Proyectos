using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;

namespace ViisionRemolques.Filters.Hangfire
{
    public class UniqueExecutionAttribute : JobFilterAttribute, IServerFilter
    {
        public void OnPerforming(PerformingContext filterContext)
        {
            var resourceLock = $"{filterContext.BackgroundJob.Job.Type.Name}.{filterContext.BackgroundJob.Job.Method.Name}";

            try
            {
                var distributedLock = filterContext.Connection.AcquireDistributedLock(resourceLock, TimeSpan.Zero);
                filterContext.Items["DistributedLock"] = distributedLock;
            }
            catch (DistributedLockTimeoutException)
            {
                filterContext.Canceled = true;

                BackgroundJob.Delete(filterContext.BackgroundJob.Id);
            }
        }

        public void OnPerformed(PerformedContext filterContext)
        {
            if (filterContext.Items.TryGetValue("DistributedLock", out var distributedLock))
            {
                ((IDisposable) distributedLock).Dispose();
            }
        }
    }
}
