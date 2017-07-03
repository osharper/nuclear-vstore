using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NuClear.VStore.Worker.Jobs;

namespace NuClear.VStore.Worker
{
    public sealed class JobRegistry
    {
        private static readonly Dictionary<string, Type> Registry =
            new Dictionary<string, Type>
                {
                    { "collect-locks", typeof(LockCleanupJob) },
                    { "collect-binaries", typeof(BinariesCleanupJob) },
                    { "produce-events", typeof(ObjectEventsProcessingJob) }
                };

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobRegistry> _logger;

        public JobRegistry(IServiceProvider serviceProvider, ILogger<JobRegistry> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public AsyncJob GetJob(string workerId, string jobId)
        {
            if (Registry.TryGetValue($"{workerId}-{jobId}", out Type jobType))
            {
                return (AsyncJob)_serviceProvider.GetRequiredService(jobType);
            }

            _logger.LogCritical("Job with id = '{workerJobId}' has not beed registered.", jobId);
            throw new JobNotFoundException(jobId);
        }
    }
}
