using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task RunAsync(string workerId, string jobId, IReadOnlyCollection<string> args, CancellationToken cancellationToken)
        {
            var parsedArgs = args
                .Select(x => x.Split(CommandLine.ArgumentKeySeparator))
                .ToDictionary(
                    x => x[0],
                    x => x.Length < 2
                             ? null
                             : x[1]?.Split(new[] { CommandLine.ArgumentValueSeparator }, StringSplitOptions.RemoveEmptyEntries).ToArray());

            var job = _jobRegistry.GetJob(workerId, jobId);
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Job '{workerJobType}' with id = '{workerJobId}' for worker '{workerId}' is starting.", job.GetType().Name, jobId, workerId);
                await job.ExecuteAsync(parsedArgs, cancellationToken);
                _logger.LogInformation("Job '{workerJobType}' with id = '{workerJobId}' for worker '{workerId}' finished.", job.GetType().Name, jobId, workerId);
            }
        }
    }
}