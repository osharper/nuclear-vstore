using System;

namespace NuClear.VStore.GC
{
    public sealed class JobNotFoundException : Exception
    {
        public JobNotFoundException(string jobId)
            : base($"Job with id = {jobId} has been not found")
        {
        }
    }
}
