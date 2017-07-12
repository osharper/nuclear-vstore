using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuClear.VStore.Worker
{
    public abstract class AsyncJob
    {
        public async Task ExecuteAsync(IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Task.Factory.StartNew(
                          async () => await ExecuteInternalAsync(args, cancellationToken),
                          cancellationToken,
                          TaskCreationOptions.LongRunning,
                          TaskScheduler.Default)
                      .Unwrap();
        }

        protected abstract Task ExecuteInternalAsync(IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken);
    }
}
