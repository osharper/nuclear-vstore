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
        private readonly ILogger<SessionCleanupService> _logger;
        private readonly IS3Client _s3Client;
        private readonly string _filesBucketName;
        private readonly Counter _removedBinariesMetric;
        private readonly Counter _removedSessionsMetric;

        public SessionCleanupService(
            ILogger<SessionCleanupService> logger,
            IS3Client s3Client,
            CephOptions cephOptions,
            MetricsProvider metricsProvider)
        {
            _logger = logger;
            _s3Client = s3Client;
            _filesBucketName = cephOptions.FilesBucketName;
            _removedBinariesMetric = metricsProvider.GetRemovedBinariesMetric();
            _removedSessionsMetric = metricsProvider.GetRemovedSessionsMetric();
        }

        public async Task<bool> DeleteSessionAsync(Guid sessionId)
        {
            var listResponse = await _s3Client.ListObjectsAsync(new ListObjectsRequest { BucketName = _filesBucketName, Prefix = sessionId.ToString() });
            if (listResponse.S3Objects.Count == 0)
            {
                return false;
            }

            foreach (var obj in listResponse.S3Objects.OrderByDescending(x => x.Size))
            {
                await _s3Client.DeleteObjectAsync(_filesBucketName, obj.Key);
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