using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using NuClear.VStore.Prometheus;

using Prometheus.Client;

namespace NuClear.VStore.S3
{
    public class AmazonS3Proxy : IAmazonS3Proxy
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly Histogram _requestDurationMsMetric;
        private readonly Counter _requestErrorsMetric;

        public AmazonS3Proxy(IAmazonS3 amazonS3, MetricsProvider metricsProvider)
        {
            _amazonS3 = amazonS3;
            _requestDurationMsMetric = metricsProvider.GetRequestDurationMsMetric();
            _requestErrorsMetric = metricsProvider.GetRequestErrorsMetric();
        }

        public async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request) =>
            await ExecuteS3Request(() => _amazonS3.PutObjectAsync(request), request.BucketName);

        public async Task<ListObjectsResponse> ListObjectsAsync(ListObjectsRequest request) =>
            await ExecuteS3Request(() => _amazonS3.ListObjectsAsync(request), request.BucketName);

        public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key) =>
            await ExecuteS3Request(() => _amazonS3.GetObjectMetadataAsync(bucketName, key), bucketName);

        public async Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, string versionId) =>
            await ExecuteS3Request(() => _amazonS3.GetObjectAsync(bucketName, key, versionId), bucketName);

        public async Task<ListVersionsResponse> ListVersionsAsync(string bucketName, string key) =>
            await ExecuteS3Request(() => _amazonS3.ListVersionsAsync(bucketName, key), bucketName);

        public async Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request) =>
            await ExecuteS3Request(() => _amazonS3.ListObjectsV2Async(request), request.BucketName);

        public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, string versionId) =>
            await ExecuteS3Request(() => _amazonS3.GetObjectMetadataAsync(bucketName, key, versionId), bucketName);

        public async Task<ListVersionsResponse> ListVersionsAsync(ListVersionsRequest request) =>
            await ExecuteS3Request(() => _amazonS3.ListVersionsAsync(request), request.BucketName);

        public async Task<GetObjectResponse> GetObjectAsync(string bucketName, string key) =>
            await ExecuteS3Request(() => _amazonS3.GetObjectAsync(bucketName, key), bucketName);

        public async Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, string versionId) =>
            await ExecuteS3Request(() => _amazonS3.DeleteObjectAsync(bucketName, key, versionId), bucketName);

        public async Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key) =>
            await ExecuteS3Request(() => _amazonS3.DeleteObjectAsync(bucketName, key), bucketName);

        private async Task<TResponse> ExecuteS3Request<TResponse>(Func<Task<TResponse>> amazonRequest, string bucketName, [CallerMemberName] string method = null)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await amazonRequest();
                stopwatch.Stop();
                _requestDurationMsMetric.Labels(Labels.Backends.Ceph, bucketName, method)
                                        .Observe(stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode != HttpStatusCode.NotFound)
            {
                _requestErrorsMetric.Labels(Labels.Backends.Ceph, bucketName, method).Inc();
                throw;
            }
        }
    }
}
