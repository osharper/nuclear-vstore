using Amazon.Runtime;
using Amazon.S3;

namespace NuClear.VStore.S3
{
    public static class AmazonS3ConfigExtensions
    {
        public static AmazonS3Config ToS3Config(this ClientConfig clientConfig)
        {
            var config = new AmazonS3Config
                             {
                                 AllowAutoRedirect = clientConfig.AllowAutoRedirect,
                                 BufferSize = clientConfig.BufferSize,
                                 CacheHttpClient = clientConfig.CacheHttpClient,
                                 DisableLogging = clientConfig.DisableLogging,
                                 LogMetrics = clientConfig.LogMetrics,
                                 LogResponse = clientConfig.LogResponse,
                                 MaxErrorRetry = clientConfig.MaxErrorRetry,
                                 ProgressUpdateInterval = clientConfig.ProgressUpdateInterval,
                                 ResignRetries = clientConfig.ResignRetries,
                                 ServiceURL = clientConfig.ServiceURL,
                                 SignatureMethod = clientConfig.SignatureMethod,
                                 SignatureVersion = clientConfig.SignatureVersion,
                                 ThrottleRetries = clientConfig.ThrottleRetries,
                                 UseDualstackEndpoint = clientConfig.UseDualstackEndpoint,
                                 UseHttp = clientConfig.UseHttp,
                             };

            if (clientConfig.Timeout != null)
            {
                config.Timeout = clientConfig.Timeout;
            }

            return config;
        }
    }
}