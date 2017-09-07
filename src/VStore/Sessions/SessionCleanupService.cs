using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
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
        private readonly ICephS3Client _cephS3Client;
        private readonly string _filesBucketName;
        private readonly Counter _removedBinariesMetric;
        private readonly Counter _removedSessionsMetric;

        public SessionCleanupService(
            ILogger<SessionCleanupService> logger,
            ICephS3Client cephS3Client,
            CephOptions cephOptions,
            MetricsProvider metricsProvider)
        {
            _logger = logger;
            _cephS3Client = cephS3Client;
            _filesBucketName = cephOptions.FilesBucketName;
            _removedBinariesMetric = metricsProvider.GetRemovedBinariesMetric();
            _removedSessionsMetric = metricsProvider.GetRemovedSessionsMetric();
        }

        public async Task<int> ArchiveSessionAsync(Guid sessionId, DateTime archiveDate)
        {
            var listResponse = await _cephS3Client.ListObjectsAsync(
                                   new ListObjectsRequest
                                       {
                                           BucketName = _filesBucketName,
                                           Prefix = sessionId.ToString()
                                       });
            if (listResponse.S3Objects.Count == 0)
            {
                return 0;
            }

            var s3Objects = listResponse.S3Objects.OrderByDescending(x => x.Size).ToList();
            foreach (var obj in s3Objects)
            {
                var getMetadataRespose = await _cephS3Client.GetObjectMetadataAsync(_filesBucketName, obj.Key);
                var archivedFileKey = obj.Key.AsArchivedFileKey(archiveDate);
                var copyRequest = new CopyObjectRequest
                    {
                        SourceBucket = _filesBucketName,
                        SourceKey = obj.Key,
                        DestinationBucket = _filesBucketName,
                        DestinationKey = archivedFileKey,
                        MetadataDirective = S3MetadataDirective.REPLACE,
                        CannedACL = S3CannedACL.PublicRead
                    };
                foreach (var metadataKey in getMetadataRespose.Metadata.Keys)
                {
                    copyRequest.Metadata.Add(metadataKey, getMetadataRespose.Metadata[metadataKey]);
                }

                await _cephS3Client.CopyObjectAsync(copyRequest);
                _logger.LogInformation(
                    "File {fileKey} copied to {archivedFileKey} while archiving session '{sessionId}'.",
                    obj.Key,
                    archivedFileKey,
                    sessionId);
            }

            var count = await DeleteSessionFilesAsync(sessionId, s3Objects);
            _logger.LogInformation("Session '{sessionId}' archived.", sessionId);

            return count;
        }

        public async Task<int> DeleteArchievedSessionAsync(Guid sessionId, DateTime archiveDate)
        {
            var listResponse = await _cephS3Client.ListObjectsAsync(
                                   new ListObjectsRequest
                                       {
                                           BucketName = _filesBucketName,
                                           Prefix = sessionId.ToString().AsArchivedFileKey(archiveDate)
                                       });
            if (listResponse.S3Objects.Count == 0)
            {
                return 0;
            }

            var count = await DeleteSessionFilesAsync(sessionId, listResponse.S3Objects.OrderByDescending(x => x.Size));
            _logger.LogInformation("Archieved session '{sessionId}' deleted.", sessionId);

            return count;
        }

        private async Task<int> DeleteSessionFilesAsync(Guid sessionId, IEnumerable<S3Object> s3Objects)
        {
            var count = 0;
            foreach (var obj in s3Objects)
            {
                await _cephS3Client.DeleteObjectAsync(_filesBucketName, obj.Key);
                if (obj.Key.EndsWith(Tokens.SessionPostfix))
                {
                    _removedSessionsMetric.Inc();
                }
                else if (!obj.Key.EndsWith("/"))
                {
                    _removedBinariesMetric.Inc();
                }

                _logger.LogInformation("File {fileKey} deleted while archiving or deleting session '{sessionId}'.", obj.Key, sessionId);
                ++count;
            }

            return count;
        }
    }
}