using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Json;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionStorageReader
    {
        private readonly string _filesBucketName;
        private readonly IAmazonS3 _amazonS3;

        public SessionStorageReader(CephOptions options, IAmazonS3 amazonS3)
        {
            _filesBucketName = options.FilesBucketName;
            _amazonS3 = amazonS3;
        }

        public async Task<(SessionDescriptor SessionDescriptor, AuthorInfo AuthorInfo, DateTime ExpiresAt)> GetSessionDescriptor(Guid sessionId)
        {
            GetObjectResponse objectResponse;
            try
            {
                objectResponse = await _amazonS3.GetObjectAsync(_filesBucketName, sessionId.AsS3ObjectKey(Tokens.SessionPostfix));
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ObjectNotFoundException($"Session '{sessionId}' does not exist");
            }
            catch (Exception ex)
            {
                throw new S3Exception(ex);
            }

            var metadataWrapper = MetadataCollectionWrapper.For(objectResponse.Metadata);
            var expiresAt = metadataWrapper.Read<DateTime>(MetadataElement.ExpiresAt);
            if (SessionDescriptor.IsSessionExpired(expiresAt))
            {
                throw new SessionExpiredException(sessionId, expiresAt);
            }

            var author = metadataWrapper.Read<string>(MetadataElement.Author);
            var authorLogin = metadataWrapper.Read<string>(MetadataElement.AuthorLogin);
            var authorName = metadataWrapper.ReadEncoded<string>(MetadataElement.AuthorName);

            string json;
            using (var reader = new StreamReader(objectResponse.ResponseStream, Encoding.UTF8))
            {
                json = reader.ReadToEnd();
            }

            var sessionDescriptor = JsonConvert.DeserializeObject<SessionDescriptor>(json, SerializerSettings.Default);
            return (sessionDescriptor, new AuthorInfo(author, authorLogin, authorName), expiresAt);
        }

        public async Task VerifySessionExpirationForBinary(string key)
        {
            var sessionId = key.AsSessionId();
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
            var expiresAt = metadataWrapper.Read<DateTime>(MetadataElement.ExpiresAt);

            if (SessionDescriptor.IsSessionExpired(expiresAt))
            {
                throw new SessionExpiredException(sessionId, expiresAt);
            }
        }

        public async Task<BinaryMetadata> GetBinaryMetadata(string key)
        {
            try
            {
                var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_filesBucketName, key);
                var metadataWrapper = MetadataCollectionWrapper.For(metadataResponse.Metadata);
                var filename = metadataWrapper.ReadEncoded<string>(MetadataElement.Filename);

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
