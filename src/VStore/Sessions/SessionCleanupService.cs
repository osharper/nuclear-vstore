using System;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Json;
using NuClear.VStore.Options;
using NuClear.VStore.Prometheus;
using NuClear.VStore.S3;

using Prometheus.Client;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionCleanupService
    {
        private readonly ILogger<SessionManagementService> _logger;
        private readonly IAmazonS3Proxy _amazonS3;
        private readonly string _filesBucketName;
        private readonly Counter _removedBinariesMetric;
        private readonly Counter _removedSessionsMetric;

        public SessionCleanupService(
            ILogger<SessionManagementService> logger,
            IAmazonS3Proxy amazonS3,
            CephOptions cephOptions,
            MetricsProvider metricsProvider)
        {
            _logger = logger;
            _amazonS3 = amazonS3;
            _filesBucketName = cephOptions.FilesBucketName;
            _removedBinariesMetric = metricsProvider.GetRemovedBinariesMetric();
            _removedSessionsMetric = metricsProvider.GetRemovedSessionsMetric();
        }

        public async Task<bool> DeleteSessionAsync(Guid sessionId)
        {
            var listResponse = await _amazonS3.ListObjectsAsync(new ListObjectsRequest { BucketName = _filesBucketName, Prefix = sessionId.ToString() });
            if (listResponse.S3Objects.Count == 0)
            {
                return false;
            }

            foreach (var obj in listResponse.S3Objects.OrderByDescending(x => x.Size))
            {
                await _amazonS3.DeleteObjectAsync(_filesBucketName, obj.Key);
                if (obj.Key.EndsWith(Tokens.SessionPostfix))
                {
                    _removedSessionsMetric.Inc();
                }
                else if (!obj.Key.EndsWith("/"))
                {
                    _removedBinariesMetric.Inc();
                }

                _logger.LogInformation("File with key '{fileKey}' deleted while deleting session '{sessionId}'.", obj.Key, sessionId);
            }

            _logger.LogInformation("Session '{sessionId}' deleted.", sessionId);
            return true;
        }
    }
}