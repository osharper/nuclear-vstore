using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Json;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

namespace NuClear.VStore.Locks
{
    public sealed class LockSessionManager
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly ILogger<LockSessionManager> _logger;
        private readonly string _bucketName;

        public LockSessionManager(IAmazonS3 amazonS3, LockOptions lockOptions, ILogger<LockSessionManager> logger)
        {
            _amazonS3 = amazonS3;
            _logger = logger;
            _bucketName = lockOptions.BucketName;
        }

        public async Task<IReadOnlyCollection<long>> GetAllCurrentLockSessionsAsync()
        {
            var response = await _amazonS3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucketName });
            return response.S3Objects.Select(x => x.Key.AsLockObjectId()).ToArray();
        }

        public async Task<bool> IsLockSessionExpired(long rootObjectKey)
        {
            var lockSessionDescriptor = await GetLockSessionDescriptor(rootObjectKey.AsS3LockKey());
            var isExpired = lockSessionDescriptor?.ExpirationDate <= DateTime.UtcNow;
            if (isExpired)
            {
                _logger.LogWarning("Expired lock session found for object with id = {id}.", rootObjectKey);
            }

            return isExpired;
        }

        public async Task DeleteLockSessionAsync(long rootObjectKey)
        {
            var lockId = rootObjectKey.AsS3LockKey();
            try
            {
                var responseTask = await _amazonS3.ListVersionsAsync(
                    new ListVersionsRequest
                    {
                        BucketName = _bucketName,
                        Prefix = lockId
                    });

                foreach (var version in responseTask.Versions)
                {
                    await _amazonS3.DeleteObjectAsync(_bucketName, lockId, version.VersionId);
                }
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode != HttpStatusCode.NotFound)
            {
                throw new S3Exception(ex);
            }
        }

        private async Task<LockSessionDescriptor> GetLockSessionDescriptor(string key)
        {
            GetObjectResponse getObjectResponse;
            try
            {
                getObjectResponse = await _amazonS3.GetObjectAsync(_bucketName, key);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            string content;
            using (var reader = new StreamReader(getObjectResponse.ResponseStream, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<LockSessionDescriptor>(content, SerializerSettings.Default);
        }
    }
}
