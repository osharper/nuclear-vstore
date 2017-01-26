using System;

using Amazon.S3;

namespace NuClear.VStore.S3
{
    public sealed class S3Exception : Exception
    {
        public S3Exception(Exception innerException) : base(innerException.Message, innerException)
        {
        }
    }
}