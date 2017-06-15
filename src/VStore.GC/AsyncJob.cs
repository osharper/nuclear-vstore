using System.Threading;
using System.Threading.Tasks;

namespace NuClear.VStore.GC
{
    public abstract class AsyncJob
    {
        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Factory.StartNew(
                          async () => await ExecuteInternalAsync(cancellationToken),
                          cancellationToken,
                          TaskCreationOptions.LongRunning,
                          TaskScheduler.Default)
                      .Unwrap();
        }

        protected abstract Task ExecuteInternalAsync(CancellationToken cancellationToken);
    }
}
