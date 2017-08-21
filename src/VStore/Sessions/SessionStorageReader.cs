using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Caching.Memory;

using Newtonsoft.Json;

using NuClear.VStore.DataContract;
using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Json;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionStorageReader
    {
        private readonly string _filesBucketName;
        private readonly IAmazonS3Proxy _amazonS3;
        private readonly IMemoryCache _memoryCache;

        public SessionStorageReader(CephOptions options, IAmazonS3Proxy amazonS3, IMemoryCache memoryCache)
        {
            _filesBucketName = options.FilesBucketName;
            _amazonS3 = amazonS3;
            _memoryCache = memoryCache;
        }

        public async Task<(SessionDescriptor SessionDescriptor, AuthorInfo AuthorInfo, DateTime ExpiresAt)> GetSessionDescriptor(Guid sessionId)
        {
            var result =
                await _memoryCache.GetOrCreateAsync(
                    sessionId,
                    async entry =>
                        {
                            try
                            {
                                using (var objectResponse = await _amazonS3.GetObjectAsync(_filesBucketName, sessionId.AsS3ObjectKey(Tokens.SessionPostfix)))
                                {
                                    var metadataWrapper = MetadataCollectionWrapper.For(objectResponse.Metadata);
                                    var expiresAt = metadataWrapper.Read<DateTime>(MetadataElement.ExpiresAt);
                                    var author = metadataWrapper.Read<string>(MetadataElement.Author);
                                    var authorLogin = metadataWrapper.Read<string>(MetadataElement.AuthorLogin);
                                    var authorName = metadataWrapper.Read<string>(MetadataElement.AuthorName);

                                    string json;
                                    using (var reader = new StreamReader(objectResponse.ResponseStream, Encoding.UTF8))
                                    {
                                        json = reader.ReadToEnd();
                                    }

                                    var sessionDescriptor = JsonConvert.DeserializeObject<SessionDescriptor>(json, SerializerSettings.Default);
                                    var tuple = (SessionDescriptor: sessionDescriptor, AuthorInfo: new AuthorInfo(author, authorLogin, authorName), ExpiresAt: expiresAt);

                                    entry.SetValue(tuple)
                                         .SetAbsoluteExpiration(expiresAt);

                                    return tuple;
                                }
                            }
                            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                            {
                                throw new ObjectNotFoundException($"Session '{sessionId}' does not exist");
                            }
                            catch (SessionExpiredException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                throw new S3Exception(ex);
                            }
                        });

            if (SessionDescriptor.IsSessionExpired(result.ExpiresAt))
            {
                throw new SessionExpiredException(sessionId, result.ExpiresAt);
            }

            return result;
        }

        public async Task VerifySessionExpirationForBinary(string key)
        {
            var sessionId = key.AsSessionId();
            var (_, _, expiresAt) = _memoryCache.Get<(SessionDescriptor, AuthorInfo, DateTime)>(sessionId);

            if (expiresAt == default(DateTime))
            {
                GetObjectMetadataResponse response;
                try
                {
                    response = await _amazonS3.GetObjectMetadataAsync(_filesBucketName, sessionId.AsS3ObjectKey(Tokens.SessionPostfix));
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new ObjectNotFoundException($"Session '{sessionId}' does not exist");
                }
                catch (Exception ex)
                {
                    throw new S3Exception(ex);
                }

                var metadataWrapper = MetadataCollectionWrapper.For(response.Metadata);
                expiresAt = metadataWrapper.Read<DateTime>(MetadataElement.ExpiresAt);
            }

            if (SessionDescriptor.IsSessionExpired(expiresAt))
            {
                throw new SessionExpiredException(sessionId, expiresAt);
            }
        }

        public async Task<BinaryMetadata> GetBinaryMetadata(string key)
        {
            var binaryMetadata = _memoryCache.Get<BinaryMetadata>(key);
            if (binaryMetadata != null)
            {
                return binaryMetadata;
            }

            try
            {
                var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_filesBucketName, key);
                var metadataWrapper = MetadataCollectionWrapper.For(metadataResponse.Metadata);
                var filename = metadataWrapper.Read<string>(MetadataElement.Filename);

                return new BinaryMetadata(filename, metadataResponse.ContentLength);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ObjectNotFoundException($"Binary with the key '{key}' not found.");
            }
            catch (Exception ex)
            {
                throw new S3Exception(ex);
            }
        }
    }
}
