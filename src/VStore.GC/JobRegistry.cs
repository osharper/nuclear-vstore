using System;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;
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
            if (Registry.TryGetValue(jobId, out Type jobType))
            {
                return (AsyncJob)_serviceProvider.GetRequiredService(jobType);
            }

            _logger.LogCritical("Job with id = '{gcJobId}' has not beed registered.", jobId);
            throw new JobNotFoundException(jobId);
        }
    }
}
