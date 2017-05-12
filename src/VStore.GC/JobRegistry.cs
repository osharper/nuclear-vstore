using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using NuClear.VStore.GC.Jobs;

namespace NuClear.VStore.GC
{
    public sealed class JobRegistry
    {
        private static readonly Dictionary<string, Type> Registry =
            new Dictionary<string, Type>
                {
                    { "locks", typeof(LockCleanupJob) }
                };

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobRegistry> _logger;

        public JobRegistry(IServiceProvider serviceProvider, ILogger<JobRegistry> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public AsyncJob GetJob(string jobId)
        {
            Type jobType;
            if (Registry.TryGetValue(jobId, out jobType))
            {
                return (AsyncJob)_serviceProvider.GetService(jobType);
            }

            _logger.LogCritical("Job with id = '{GCJobId}' has not beed registered.", jobId);
            throw new JobNotFoundException(jobId);
        }
    }
}
