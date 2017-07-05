using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace NuClear.VStore.Worker
{
    public sealed class JobRunner
    {
        private readonly ILogger<JobRunner> _logger;
        private readonly JobRegistry _jobRegistry;

        public JobRunner(ILogger<JobRunner> logger, JobRegistry jobRegistry)
        {
            _logger = logger;
            _jobRegistry = jobRegistry;
        }

        public async Task RunAsync(string workerId, string jobId, IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken)
        {
            var job = _jobRegistry.GetJob(workerId, jobId);
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Job '{workerJobType}' with id = '{workerJobId}' for worker '{workerId}' is starting.", job.GetType().Name, jobId, workerId);
                await job.ExecuteAsync(args, cancellationToken);
                _logger.LogInformation("Job '{workerJobType}' with id = '{workerJobId}' for worker '{workerId}' finished.", job.GetType().Name, jobId, workerId);
            }
        }
    }
}