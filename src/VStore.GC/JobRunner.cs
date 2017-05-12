using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace NuClear.VStore.GC
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

        public async Task RunAsync(string jobId, CancellationToken cancellationToken)
        {
            var job = _jobRegistry.GetJob(jobId);
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Job '{GCJobType}' with id = '{GCJobId}' is starting.", job.GetType().Name, jobId);
                await job.ExecuteAsync(cancellationToken);
                _logger.LogInformation("Job '{GCJobType}' with id = '{GCJobId}' finished." , job.GetType().Name, jobId);
            }
        }
    }
}