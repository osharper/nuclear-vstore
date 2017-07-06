using System;

namespace NuClear.VStore.Worker
{
    public sealed class JobNotFoundException : Exception
    {
        public JobNotFoundException(string jobId)
            : base($"Job with id = {jobId} has not been found")
        {
        }
    }
}
