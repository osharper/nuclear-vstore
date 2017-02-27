using System;

namespace NuClear.VStore.S3
{
    public sealed class S3Exception : Exception
    {
        public S3Exception(Exception innerException) : base(innerException.Message, innerException)
        {
        }
    }
}
