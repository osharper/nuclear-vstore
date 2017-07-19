using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using MigrationTool.Json;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Http;
using NuClear.VStore.Json;
using NuClear.VStore.Objects;

namespace MigrationTool
{
    public class ApiRepository
    {
        private readonly Uri _apiUri;
        private readonly Uri _storageUri;
        private readonly Uri _templateUri;
        private readonly Uri _devUri;
        private readonly Uri _objectUri;
        private readonly Uri _positionUri;
        private readonly Uri _searchUri;
        private readonly ILogger<ApiRepository> _logger;
        private readonly HttpClient _httpClient;

        public ApiRepository(ILogger<ApiRepository> logger, Uri apiUri, Uri storageUri, string token)
        {
            _apiUri = apiUri;
            _storageUri = storageUri;
            _templateUri = new Uri(apiUri, "template/");
            _positionUri = new Uri(apiUri, "nomenclature/");
            _searchUri = new Uri(apiUri, "search/");
            _devUri = new Uri(apiUri, "dev/");
            _objectUri = new Uri(apiUri, "am/");
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<string> CreateObjectAsync(long id, string firmId, ObjectDescriptor objectDescriptor)
        {
            var objectId = id.ToString();
            var methodUri = new Uri(_objectUri, objectId + "?firm=" + firmId);
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            try
            {
                using (var content = new StringContent(JsonConvert.SerializeObject(objectDescriptor, ApiSerializerSettings.Default), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _httpClient.PutAsync(methodUri, content))
                    {
                        (stringResponse, server, requestId) = await HandleResponse(response);
                        if (response.StatusCode == HttpStatusCode.Conflict)
                        {
                            throw new ObjectAlreadyExistsException(id);
                        }

                        response.EnsureSuccessStatusCode();
                        var res = JsonConvert.DeserializeObject<ApiVersionedDescriptor>(stringResponse, SerializerSettings.Default);
                        if (res == null)
                        {
                            throw new SerializationException("Cannot deserialize response: " + stringResponse);
                        }

                        _logger.LogInformation("Imported object {id} got version: {version}", objectId, res.VersionId);

                        return res.VersionId;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error while object {id} import with response: {response}",
                                 requestId,
                                 server,
                                 objectId,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Object {id} import error with response: {response}", objectId, stringResponse);
                throw;
            }
        }

        public async Task<string> GetObjectVersionAsync(long id)
        {
            var objectId = id.ToString();
            var methodUri = new Uri(_objectUri, objectId);
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            try
            {
                using (var response = await _httpClient.GetAsync(methodUri))
                {
                    (stringResponse, server, requestId) = await HandleResponse(response);
                    response.EnsureSuccessStatusCode();
                    var res = JsonConvert.DeserializeObject<IReadOnlyList<ApiVersionedDescriptor>>(stringResponse, ApiSerializerSettings.Default);
                    if (res == null)
                    {
                        throw new SerializationException("Cannot deserialize response: " + stringResponse);
                    }

                    if (res.Count != 1)
                    {
                        throw new NotSupportedException("Unsupported count of objects in response: " + res.Count.ToString());
                    }

                    return res.First().VersionId;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error while getting object {id} with response: {response}",
                                 requestId,
                                 server,
                                 objectId,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Getting object {id} error", objectId);
                throw;
            }
        }

        public async Task CreatePositionAsync(string positionId, object positionDescriptor)
        {
            var methodUri = new Uri(_devUri, "nomenclature");
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            try
            {
                using (var content = new StringContent(JsonConvert.SerializeObject(positionDescriptor, SerializerSettings.Default), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _httpClient.PostAsync(methodUri, content))
                    {
                        (stringResponse, server, requestId) = await HandleResponse(response);
                        response.EnsureSuccessStatusCode();
                        _logger.LogInformation("Position {id} has been created", positionId);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error while creating position {id} with response: {response}",
                                 requestId,
                                 server,
                                 positionId,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Position {id} creating error", positionId);
                throw;
            }
        }

        public async Task CreatePositionTemplateLinkAsync(string positionId, string templateId)
        {
            var methodUri = new Uri(_positionUri, $"{positionId}/template/{templateId}");
            using (var req = new HttpRequestMessage(HttpMethod.Post, methodUri))
            {
                var server = string.Empty;
                var requestId = string.Empty;
                var stringResponse = string.Empty;
                try
                {
                    using (var response = await _httpClient.SendAsync(req))
                    {
                        (stringResponse, server, requestId) = await HandleResponse(response);
                        response.EnsureSuccessStatusCode();
                        _logger.LogInformation("Link has been created between position {positionId} and template {templateId}", positionId, templateId);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(new EventId(),
                                     ex,
                                     "Request {requestId} to server {server} error while creating link between position {positionId} and template {templateId} with response: {response}",
                                     requestId,
                                     server,
                                     positionId,
                                     templateId,
                                     stringResponse);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(new EventId(), ex, "Error while creating link between position {positionId} and template {templateId}", positionId, templateId);
                    throw;
                }
            }
        }

        public async Task<string> CreateTemplateAsync(string templateId, TemplateDescriptor template)
        {
            var methodUri = new Uri(_templateUri, templateId);
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            try
            {
                var descriptor = new
                {
                    template.Properties,
                    template.Elements
                };

                using (var content = new StringContent(JsonConvert.SerializeObject(descriptor, SerializerSettings.Default), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _httpClient.PostAsync(methodUri, content))
                    {
                        (stringResponse, server, requestId) = await HandleResponse(response);
                        response.EnsureSuccessStatusCode();

                        _logger.LogInformation("Created template {id} got version: {version}", templateId, response.Headers.ETag.Tag);
                        return stringResponse;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error while creating template {id} with response: {response}",
                                 requestId,
                                 server,
                                 templateId,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Template {id} creating error", templateId);
                throw;
            }
        }

        public async Task<ApiObjectDescriptor> GetNewObjectAsync(string templateId, string langCode, string firmId)
        {
            var methodUri = new Uri(_templateUri, $"{templateId}/session?languages={langCode}&firm={firmId}");
            using (var req = new HttpRequestMessage(HttpMethod.Post, methodUri))
            {
                var server = string.Empty;
                var requestId = string.Empty;
                var stringResponse = string.Empty;
                try
                {
                    using (var response = await _httpClient.SendAsync(req))
                    {
                        (stringResponse, server, requestId) = await HandleResponse(response);
                        response.EnsureSuccessStatusCode();
                        var descriptor = JsonConvert.DeserializeObject<IReadOnlyCollection<ApiObjectDescriptor>>(stringResponse, ApiSerializerSettings.Default);
                        if (descriptor == null)
                        {
                            throw new SerializationException("Cannot deserialize new object descriptor for template " + templateId + ": " + stringResponse);
                        }

                        return descriptor.First();
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(new EventId(),
                                     ex,
                                     "Request {requestId} to server {server} error while getting new object for template {id} and lang {lang} with response: {response}",
                                     requestId,
                                     server,
                                     templateId,
                                     langCode,
                                     stringResponse);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(new EventId(), ex, "Get new object for template {id} and lang {lang} error", templateId, langCode);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyCollection<PositionDescriptor>> GetPositionsAsync()
        {
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            var allDescriptors = new List<PositionDescriptor>();
            try
            {
                for (var pageNum = 1; ; ++pageNum)
                {
                    var methodUri = new Uri(_searchUri, $"nomenclature?isDeleted=false&count=500&page={pageNum}");
                    using (var response = await _httpClient.GetAsync(methodUri))
                    {
                        (stringResponse, server, requestId) = await HandleResponse(response);
                        response.EnsureSuccessStatusCode();
                        var descriptors = JsonConvert.DeserializeObject<IReadOnlyCollection<PositionDescriptor>>(stringResponse, ApiSerializerSettings.Default);
                        if (descriptors == null)
                        {
                            throw new SerializationException("Cannot deserialize positions: " + stringResponse);
                        }

                        if (descriptors.Count < 1)
                        {
                            break;
                        }

                        allDescriptors.AddRange(descriptors);

                        if (response.Headers.TryGetValues("X-Pagination-Total-Count", out var values) &&
                            int.TryParse(values.FirstOrDefault(), out var count) &&
                            count == allDescriptors.Count)
                        {
                            break;
                        }
                    }
                }

                return allDescriptors;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error while getting positions with response: {response}",
                                 requestId,
                                 server,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Get positions error");
                throw;
            }
        }

        public async Task<TemplateDescriptor> GetTemplateAsync(string templateId)
        {
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            var methodUri = new Uri(_templateUri, templateId);
            try
            {
                using (var response = await _httpClient.GetAsync(methodUri))
                {
                    (stringResponse, server, requestId) = await HandleResponse(response);
                    if (response.StatusCode == HttpStatusCode.NotFound &&
                        server == "okapi")
                    {
                        _logger.LogDebug("Template {id} not found", templateId);
                        return null;
                    }

                    response.EnsureSuccessStatusCode();
                    var descriptor = JsonConvert.DeserializeObject<TemplateDescriptor>(stringResponse, SerializerSettings.Default);
                    if (descriptor == null)
                    {
                        throw new SerializationException("Cannot deserialize template descriptor " + templateId + ": " + stringResponse);
                    }

                    return descriptor;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error while getting template {id} with response: {response}",
                                 requestId,
                                 server,
                                 templateId,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Get template {id} error", templateId);
                throw;
            }
        }

        public async Task<JToken> UploadFileAsync(Uri uploadUrl, Models.File file, FileFormat format)
        {
            var url = uploadUrl;
            var stringResponse = string.Empty;
            var fileIdStr = file.Id.ToString();
            try
            {
                var fileName = file.FileName;
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = $"file_{fileIdStr}.{format.ToString().ToLowerInvariant()}";
                    _logger.LogWarning("File {id} hasn't name and will have generated name: {name}", fileIdStr, fileName);
                }
                else
                {
                    fileName = Path.ChangeExtension(fileName, format.ToString());
                }

                using (var content = new MultipartFormDataContent())
                {
                    content.Add(new StreamContent(new MemoryStream(file.Data)), fileName, fileName);
                    if (!url.IsAbsoluteUri)
                    {
                        url = new Uri(_storageUri, uploadUrl);
                    }

                    using (var response = await _httpClient.PostAsync(url, content))
                    {
                        stringResponse = (await HandleResponse(response)).ResponseContent;
                        response.EnsureSuccessStatusCode();
                        _logger.LogInformation("File {id} uploaded successfully to {url}", fileIdStr, url);
                        return JObject.Parse(stringResponse);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "File {id} upload error to {url}, response: {response}", fileIdStr, url, stringResponse);
                throw;
            }
        }

        public async Task UpdateTemplateAsync(TemplateDescriptor template)
        {
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            var templateId = template.Id.ToString();
            var methodUri = new Uri(_templateUri, templateId);
            try
            {
                using (var content = new StringContent(JsonConvert.SerializeObject(template, SerializerSettings.Default), Encoding.UTF8, "application/json"))
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Put, methodUri))
                    {
                        request.Content = content;
                        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{template.VersionId}\""));
                        using (var response = await _httpClient.SendAsync(request))
                        {
                            (stringResponse, server, requestId) = await HandleResponse(response);
                            response.EnsureSuccessStatusCode();
                            _logger.LogInformation("Updated template with {id} got new version: {version}", templateId, response.Headers.ETag.Tag);
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error in template {id} update with response: {response}",
                                 requestId,
                                 server,
                                 templateId,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Template {id} update error", templateId);
                throw;
            }
        }

        public async Task SelectObjectToWhitelist(string objectId)
        {
            var methodUri = new Uri(_objectUri, $"{objectId}/whiteList");
            using (var req = new HttpRequestMessage(HttpMethod.Post, methodUri))
            {
                var server = string.Empty;
                var requestId = string.Empty;
                var stringResponse = string.Empty;
                try
                {
                    using (var response = await _httpClient.SendAsync(req))
                    {
                        (stringResponse, server, requestId) = await HandleResponse(response);
                        response.EnsureSuccessStatusCode();
                        _logger.LogInformation("Object {objectId} has been selected to whitelist", objectId);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(new EventId(),
                                     ex,
                                     "Request {requestId} to server {server} error while selecting object {objectId} to whitelist with response: {response}",
                                     requestId,
                                     server,
                                     objectId,
                                     stringResponse);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(new EventId(), ex, "Error while selecting object {objectId} to whitelist", objectId);
                    throw;
                }
            }
        }

        public async Task UpdateObjectModerationStatusAsync(string objectId, string versionId, ModerationResult moderationResult)
        {
            var methodUri = new Uri(_objectUri, $"{objectId}/version/{versionId}/moderation");
            using (var content = new StringContent(JsonConvert.SerializeObject(moderationResult, SerializerSettings.Default), Encoding.UTF8, "application/json"))
            {
                using (var req = new HttpRequestMessage(HttpMethod.Put, methodUri))
                {
                    req.Content = content;
                    var server = string.Empty;
                    var requestId = string.Empty;
                    var stringResponse = string.Empty;
                    try
                    {
                        using (var response = await _httpClient.SendAsync(req))
                        {
                            (stringResponse, server, requestId) = await HandleResponse(response);
                            response.EnsureSuccessStatusCode();
                            _logger.LogInformation("Object {objectId} with version {versionId} has been updated with moderation status {status}", objectId, versionId, moderationResult.Status);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogError(new EventId(),
                                         ex,
                                         "Request {requestId} to server {server} error while updating object {objectId} moderation status {status} with response: {response}",
                                         requestId,
                                         server,
                                         objectId,
                                         moderationResult.Status,
                                         stringResponse);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(new EventId(), ex, "Error while updating object {objectId} moderation status {status}", objectId, moderationResult.Status);
                        throw;
                    }
                }
            }
        }

        public async Task EnsureApiAvailable(int pingInterval, int pingTries)
        {
            var tryNum = 0;
            var succeeded = false;
            var healthcheckApiUri = new Uri(_apiUri, "healthcheck");
            var healthcheckStorageUri = new Uri(_storageUri, "/healthcheck");
            do
            {
                ++tryNum;
                _logger.LogInformation("Waiting for {delay} seconds before try {try}", pingInterval.ToString(), tryNum.ToString());
                await Task.Delay(TimeSpan.FromSeconds(pingInterval));
                try
                {
                    _logger.LogInformation("Connecting to {url}", healthcheckApiUri);
                    using (var response = await _httpClient.GetAsync(healthcheckApiUri))
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    _logger.LogInformation("Connecting to {url}", healthcheckStorageUri);
                    using (var response = await _httpClient.GetAsync(healthcheckStorageUri))
                    {
                        response.EnsureSuccessStatusCode();
                        succeeded = true;
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(new EventId(), ex, "Attempt {try} failed while connecting", tryNum.ToString());
                }
            }
            while (!succeeded && tryNum < pingTries);

            if (!succeeded)
            {
                throw new WebException("Can't establish connection with API");
            }
        }

        private async Task<(string ResponseContent, string Server, string RequestId)> HandleResponse(HttpResponseMessage response)
        {
            var stringResponse = await response.Content.ReadAsStringAsync();
            response.Headers.TryGetValues(HeaderNames.Server, out IEnumerable<string> server);
            response.Headers.TryGetValues(HeaderNames.RequestId, out IEnumerable<string> requestId);
            _logger.LogDebug(
                "Sending '{method}' request on '{url}', request id {requestId}, server {server}, got status {status} with response: {response}",
                response.RequestMessage.Method,
                response.RequestMessage.RequestUri,
                requestId,
                server,
                response.StatusCode,
                stringResponse);
            return (stringResponse, server?.FirstOrDefault(), requestId?.FirstOrDefault());
        }
    }
}
