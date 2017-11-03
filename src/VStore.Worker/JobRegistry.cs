using System;
using System.Collections.Generic;

using Autofac;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Worker.Jobs;

namespace NuClear.VStore.Worker
{
    public sealed class JobRegistry
    {
        private static readonly Dictionary<string, Type> Registry =
            new Dictionary<string, Type>
                {
                    { "collect-binaries", typeof(BinariesCleanupJob) },
                    { "produce-events", typeof(ObjectEventsProcessingJob) }
                };

        private readonly ILifetimeScope _lifetimeScope;
        private readonly ILogger<JobRegistry> _logger;

        public JobRegistry(ILifetimeScope lifetimeScope, ILogger<JobRegistry> logger)
        {
            _lifetimeScope = lifetimeScope;
            _logger = logger;
        }

        public AsyncJob GetJob(string workerId, string jobId)
        {
            if (Registry.TryGetValue($"{workerId}-{jobId}", out var jobType))
            {
                return (AsyncJob)_lifetimeScope.Resolve(jobType);
            }

            _logger.LogCritical("Job with id = '{workerJobId}' for worker '{workerId}' has not beed registered.", jobId, workerId);
            throw new JobNotFoundException(jobId);
        }
    }
}
