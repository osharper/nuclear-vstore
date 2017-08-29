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
    public sealed class S3ClientPrometheusDecorator : IS3Client
    {
        private readonly IS3Client _s3Client;
        private readonly string _backendLabel;
        private readonly Histogram _requestDurationMsMetric;
        private readonly Counter _requestErrorsMetric;

        public S3ClientPrometheusDecorator(IS3Client s3Client, MetricsProvider metricsProvider, string backendLabel)
        {
            _s3Client = s3Client;
            _backendLabel = backendLabel;
            _requestDurationMsMetric = metricsProvider.GetRequestDurationMsMetric();
            _requestErrorsMetric = metricsProvider.GetRequestErrorsMetric();
        }

        public async Task<ListObjectsResponse> ListObjectsAsync(ListObjectsRequest request)
            => await ExecuteS3Request(() => _s3Client.ListObjectsAsync(request), request.BucketName);

        public async Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request)
            => await ExecuteS3Request(() => _s3Client.ListObjectsV2Async(request), request.BucketName);

        public async Task<ListVersionsResponse> ListVersionsAsync(string bucketName, string key)
            => await ExecuteS3Request(() => _s3Client.ListVersionsAsync(bucketName, key), bucketName);

        public async Task<ListVersionsResponse> ListVersionsAsync(ListVersionsRequest request)
            => await ExecuteS3Request(() => _s3Client.ListVersionsAsync(request), request.BucketName);

        public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key)
            => await ExecuteS3Request(() => _s3Client.GetObjectMetadataAsync(bucketName, key), bucketName);

        public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, string versionId)
            => await ExecuteS3Request(() => _s3Client.GetObjectMetadataAsync(bucketName, key, versionId), bucketName);

        public async Task<GetObjectResponse> GetObjectAsync(string bucketName, string key)
            => await ExecuteS3Request(() => _s3Client.GetObjectAsync(bucketName, key), bucketName);

        public async Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, string versionId)
            => await ExecuteS3Request(() => _s3Client.GetObjectAsync(bucketName, key, versionId), bucketName);

        public async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request)
            => await ExecuteS3Request(() => _s3Client.PutObjectAsync(request), request.BucketName);

        public async Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key)
            => await ExecuteS3Request(() => _s3Client.DeleteObjectAsync(bucketName, key), bucketName);

        public async Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, string versionId)
            => await ExecuteS3Request(() => _s3Client.DeleteObjectAsync(bucketName, key, versionId), bucketName);

        public async Task<CopyObjectResponse> CopyObjectAsync(CopyObjectRequest request)
            => await ExecuteS3Request(() => _s3Client.CopyObjectAsync(request), request.DestinationBucket);

        private async Task<TResponse> ExecuteS3Request<TResponse>(Func<Task<TResponse>> amazonRequest, string bucketName, [CallerMemberName] string method = null)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await amazonRequest();
                stopwatch.Stop();
                _requestDurationMsMetric.Labels(_backendLabel, bucketName, method)
                                        .Observe(stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode != HttpStatusCode.NotFound)
            {
                _requestErrorsMetric.Labels(_backendLabel, bucketName, method).Inc();
                throw;
            }
        }
    }
}