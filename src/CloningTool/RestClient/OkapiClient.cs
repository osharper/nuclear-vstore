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

using CloningTool.Json;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Http;
using NuClear.VStore.Json;
using NuClear.VStore.Objects;

namespace CloningTool.RestClient
{
    public class OkapiClient : IRestClientFacade, IDisposable
    {
        private const int ApiFetchMaxSize = 500;
        private const string DefaultServer = "okapi";
        private const string PaginationTotalCountHeaderName = "X-Pagination-Total-Count";

        private readonly Uri _apiUri;
        private readonly Uri _templateUri;
        private readonly Uri _amUri;
        private readonly Uri _positionUri;
        private readonly Uri _searchUri;
        private readonly ILogger<OkapiClient> _logger;
        private readonly HttpClient _authorizedHttpClient = new HttpClient();
        private readonly HttpClient _unauthorizedHttpClient = new HttpClient();

        public OkapiClient(ILogger<OkapiClient> logger, Uri apiUri, string apiVersion, string apiToken)
        {
            _apiUri = apiUri;
            var apiBase = new Uri(apiUri, $"api/{apiVersion}/");
            _templateUri = new Uri(apiBase, "template/");
            _positionUri = new Uri(apiBase, "nomenclature/");
            _searchUri = new Uri(apiBase, "search/");
            _amUri = new Uri(apiBase, "am/");
            _logger = logger;
            _authorizedHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        }

        public void Dispose()
        {
            _authorizedHttpClient?.Dispose();
            _unauthorizedHttpClient?.Dispose();
        }

        public async Task<string> CreateAdvertisementAsync(long id, long firmId, ApiObjectDescriptor advertisement)
        {
            var amId = id.ToString();
            var methodUri = new Uri(_amUri, amId + "?firm=" + firmId.ToString());
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            try
            {
                using (var content = new StringContent(JsonConvert.SerializeObject(advertisement, ApiSerializerSettings.Default), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _authorizedHttpClient.PutAsync(methodUri, content))
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

                        _logger.LogInformation("Created advertisement {id} got version: {version}", amId, res.VersionId);

                        return res.VersionId;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error while advertisement {id} creating with response: {response}",
                                 requestId,
                                 server,
                                 amId,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Advertisement {id} creating error with response: {response}", amId, stringResponse);
                throw;
            }
        }

        public async Task<ApiObjectDescriptor> GetAdvertisementAsync(long advertisementId)
        {
            var amId = advertisementId.ToString();
            var methodUri = new Uri(_amUri, amId);
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            try
            {
                using (var response = await _authorizedHttpClient.GetAsync(methodUri))
                {
                    (stringResponse, server, requestId) = await HandleResponse(response);
                    if (response.StatusCode == HttpStatusCode.NotFound && server == DefaultServer)
                    {
                        _logger.LogDebug("Advertisement {id} not found", amId);
                        return null;
                    }

                    response.EnsureSuccessStatusCode();
                    var res = JsonConvert.DeserializeObject<IReadOnlyList<ApiObjectDescriptor>>(stringResponse, ApiSerializerSettings.Default);
                    if (res == null)
                    {
                        throw new SerializationException("Cannot deserialize response for advertisement " + amId + ": " + stringResponse);
                    }

                    if (res.Count != 1)
                    {
                        throw new NotSupportedException("Unsupported count of advertisements in response for object " + amId + ": " + res.Count.ToString());
                    }

                    return res.First();
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error while getting advertisement {id} with response: {response}",
                                 requestId,
                                 server,
                                 amId,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Getting advertisement {id} error", amId);
                throw;
            }
        }

        public async Task<string> GetAdvertisementVersionAsync(long id)
        {
            var amId = id.ToString();
            var methodUri = new Uri(_searchUri, "am?id=" + amId);
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            try
            {
                using (var response = await _authorizedHttpClient.GetAsync(methodUri))
                {
                    (stringResponse, server, requestId) = await HandleResponse(response);
                    response.EnsureSuccessStatusCode();
                    var res = JsonConvert.DeserializeObject<IReadOnlyList<ApiListAdvertisement>>(stringResponse, ApiSerializerSettings.Default);
                    if (res == null)
                    {
                        throw new SerializationException("Cannot deserialize response for advertisement " + amId + ": " + stringResponse);
                    }

                    if (res.Count < 1)
                    {
                        _logger.LogDebug("Advertisement {id} not found", amId);
                        return null;
                    }

                    if (res.Count > 1)
                    {
                        throw new NotSupportedException("Unsupported count of objects in response for advertisement " + amId + ": " + res.Count.ToString());
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
                                 amId,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Getting object {id} error", amId);
                throw;
            }
        }

        public async Task<ApiObjectDescriptor> UpdateAdvertisementAsync(ApiObjectDescriptor advertisement)
        {
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            var advertisementId = advertisement.Id.ToString();
            var methodUri = new Uri(_amUri, advertisementId);
            try
            {
                using (var content = new StringContent(JsonConvert.SerializeObject(advertisement, ApiSerializerSettings.Default), Encoding.UTF8, "application/json"))
                {
                    using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), methodUri))
                    {
                        request.Content = content;
                        using (var response = await _authorizedHttpClient.SendAsync(request))
                        {
                            (stringResponse, server, requestId) = await HandleResponse(response);
                            response.EnsureSuccessStatusCode();
                            var descriptor = JsonConvert.DeserializeObject<ApiObjectDescriptor>(stringResponse, ApiSerializerSettings.Default);
                            if (descriptor == null)
                            {
                                throw new SerializationException("Cannot deserialize object descriptor " + advertisementId + ": " + stringResponse);
                            }

                            _logger.LogInformation(
                                "Updated advertisement {id} got new version: {version} (old version {oldVersion})",
                                advertisementId,
                                descriptor.VersionId,
                                advertisement.VersionId);
                            return descriptor;
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error while updating advertisement {id} with response: {response}",
                                 requestId,
                                 server,
                                 advertisementId,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Advertisement {id} update error", advertisementId);
                throw;
            }
        }

        public async Task<ApiObjectDescriptor> CreateAdvertisementPrototypeAsync(long templateId, string langCode, long firmId)
        {
            var methodUri = new Uri(_templateUri, $"{templateId}/session?languages={langCode}&firm={firmId}");
            using (var req = new HttpRequestMessage(HttpMethod.Post, methodUri))
            {
                var server = string.Empty;
                var requestId = string.Empty;
                var stringResponse = string.Empty;
                try
                {
                    using (var response = await _authorizedHttpClient.SendAsync(req))
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

        public async Task<IReadOnlyCollection<ApiListAdvertisement>> GetAdvertisementsByTemplateAsync(long templateId, int? fetchSize)
        {
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            var allDescriptors = new List<ApiListAdvertisement>();
            try
            {
                for (var pageNum = 1; ; ++pageNum)
                {
                    var methodUri = new Uri(_searchUri, $"am?template={templateId}&count={fetchSize ?? ApiFetchMaxSize}&page={pageNum}&sort=createdAt:desc");
                    using (var response = await _authorizedHttpClient.GetAsync(methodUri))
                    {
                        (stringResponse, server, requestId) = await HandleResponse(response);
                        response.EnsureSuccessStatusCode();
                        var descriptors = JsonConvert.DeserializeObject<IReadOnlyCollection<ApiListAdvertisement>>(stringResponse, ApiSerializerSettings.Default);
                        if (descriptors == null)
                        {
                            throw new SerializationException("Cannot deserialize advertisements: " + stringResponse);
                        }

                        if (descriptors.Count < 1 && pageNum == 1)
                        {
                            _logger.LogWarning("There are no advertisements with template {id}", templateId);
                            return Array.Empty<ApiListAdvertisement>();
                        }

                        if (fetchSize.HasValue)
                        {
                            return descriptors;
                        }

                        allDescriptors.AddRange(descriptors);

                        if (response.Headers.TryGetValues(PaginationTotalCountHeaderName, out var values) &&
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
                                 "Request {requestId} to server {server} error while getting advertisements with response: {response}",
                                 requestId,
                                 server,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Get advertisements error");
                throw;
            }
        }

        public async Task SelectAdvertisementToWhitelistAsync(string advertisementId)
        {
            var methodUri = new Uri(_amUri, $"{advertisementId}/whiteList");
            using (var req = new HttpRequestMessage(HttpMethod.Post, methodUri))
            {
                var server = string.Empty;
                var requestId = string.Empty;
                var stringResponse = string.Empty;
                try
                {
                    using (var response = await _authorizedHttpClient.SendAsync(req))
                    {
                        (stringResponse, server, requestId) = await HandleResponse(response);
                        response.EnsureSuccessStatusCode();
                        _logger.LogInformation("Object {objectId} has been selected to whitelist", advertisementId);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(new EventId(),
                                     ex,
                                     "Request {requestId} to server {server} error while selecting object {objectId} to whitelist with response: {response}",
                                     requestId,
                                     server,
                                     advertisementId,
                                     stringResponse);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(new EventId(), ex, "Error while selecting object {objectId} to whitelist", advertisementId);
                    throw;
                }
            }
        }

        public async Task UpdateAdvertisementModerationStatusAsync(string objectId, string versionId, ModerationResult moderationResult)
        {
            var methodUri = new Uri(_amUri, $"{objectId}/version/{versionId}/moderation");
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
                        using (var response = await _authorizedHttpClient.SendAsync(req))
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
                    using (var response = await _authorizedHttpClient.PostAsync(methodUri, content))
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

        public async Task<IReadOnlyCollection<ApiListTemplate>> GetTemplatesAsync()
        {
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            var allDescriptors = new List<ApiListTemplate>();
            try
            {
                for (var pageNum = 1; ; ++pageNum)
                {
                    var methodUri = new Uri(_searchUri, $"template?count={ApiFetchMaxSize}&page={pageNum}");
                    using (var response = await _authorizedHttpClient.GetAsync(methodUri))
                    {
                        (stringResponse, server, requestId) = await HandleResponse(response);
                        response.EnsureSuccessStatusCode();
                        var descriptors = JsonConvert.DeserializeObject<IReadOnlyCollection<ApiListTemplate>>(stringResponse, ApiSerializerSettings.Default);
                        if (descriptors == null)
                        {
                            throw new SerializationException("Cannot deserialize templates: " + stringResponse);
                        }

                        if (descriptors.Count < 1)
                        {
                            break;
                        }

                        allDescriptors.AddRange(descriptors);

                        if (response.Headers.TryGetValues(PaginationTotalCountHeaderName, out var values) &&
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
                                 "Request {requestId} to server {server} error while getting templates with response: {response}",
                                 requestId,
                                 server,
                                 stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Get templates error");
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
                using (var response = await _authorizedHttpClient.GetAsync(methodUri))
                {
                    (stringResponse, server, requestId) = await HandleResponse(response);
                    if (response.StatusCode == HttpStatusCode.NotFound &&
                        server == DefaultServer)
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

        public async Task<string> UpdateTemplateAsync(TemplateDescriptor template, string versionId)
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
                        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{versionId}\""));
                        using (var response = await _authorizedHttpClient.SendAsync(request))
                        {
                            (stringResponse, server, requestId) = await HandleResponse(response);
                            response.EnsureSuccessStatusCode();
                            _logger.LogInformation(
                                "Updated template {id} got new version: {version} (old version {oldVersion})",
                                templateId,
                                response.Headers.ETag.Tag,
                                versionId);
                            return response.Headers.ETag.Tag;
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(),
                                 ex,
                                 "Request {requestId} to server {server} error while updating template {id} with response: {response}",
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

        public async Task<IReadOnlyCollection<PositionDescriptor>> GetContentPositionsAsync()
        {
            var server = string.Empty;
            var requestId = string.Empty;
            var stringResponse = string.Empty;
            var allDescriptors = new List<PositionDescriptor>();
            try
            {
                for (var pageNum = 1; ; ++pageNum)
                {
                    var methodUri = new Uri(_searchUri, $"nomenclature?isDeleted=false&isContentSales=true&count={ApiFetchMaxSize}&page={pageNum}");
                    using (var response = await _authorizedHttpClient.GetAsync(methodUri))
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

                        if (response.Headers.TryGetValues(PaginationTotalCountHeaderName, out var values) &&
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
                    using (var response = await _authorizedHttpClient.SendAsync(req))
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

        public async Task<byte[]> DownloadFileAsync(long advertisementId, Uri downloadUrl)
        {
            var stringResponse = string.Empty;
            try
            {
                using (var response = await _unauthorizedHttpClient.GetAsync(downloadUrl))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("File within advertisement {amId} downloaded from {url}", advertisementId, downloadUrl);
                        return await response.Content.ReadAsByteArrayAsync();
                    }

                    (stringResponse, _, _) = await HandleResponse(response);
                    throw new HttpRequestException($"Request error with status {response.StatusCode} and reason {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    new EventId(),
                    ex,
                    "File within advertisement {amId} download error from {url}, response: {response}",
                    advertisementId,
                    downloadUrl,
                    stringResponse);
                throw;
            }
        }

        public async Task<IObjectElementValue> UploadFileAsync(long advertisementId, Uri uploadUrl, string fileName, byte[] fileData)
        {
            var url = uploadUrl;
            var stringResponse = string.Empty;
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException(nameof(fileName), "File name cannot be empty");
            }

            try
            {
                using (var content = new MultipartFormDataContent())
                {
                    using (var memoryStream = new MemoryStream(fileData, false))
                    {
                        using (var streamContent = new StreamContent(memoryStream))
                        {
                            content.Add(streamContent, fileName, fileName);
                            using (var response = await _authorizedHttpClient.PostAsync(url, content))
                            {
                                stringResponse = (await HandleResponse(response)).ResponseContent;
                                response.EnsureSuccessStatusCode();
                                _logger.LogInformation("File {fileName} within advertisement {objectId} uploaded to {url}", fileName, advertisementId, url);
                                return JsonConvert.DeserializeObject<ApiObjectElementRawValue>(stringResponse, ApiSerializerSettings.Default);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    new EventId(),
                    ex,
                    "File {fileName} within advertisement {objectId} upload error to {url}, response: {response}",
                    fileName,
                    advertisementId,
                    url,
                    stringResponse);
                throw;
            }
        }

        public async Task EnsureApiAvailableAsync(int pingInterval, int pingTries)
        {
            var tryNum = 0;
            var succeeded = false;
            var healthcheckUri = new Uri(_apiUri, "healthcheck");
            do
            {
                ++tryNum;
                _logger.LogInformation("Waiting for {delay} seconds before try {try}", pingInterval, tryNum);
                await Task.Delay(TimeSpan.FromSeconds(pingInterval));
                try
                {
                    _logger.LogInformation("Connecting to {url}", healthcheckUri);
                    using (var response = await _authorizedHttpClient.GetAsync(healthcheckUri))
                    {
                        response.EnsureSuccessStatusCode();
                        succeeded = true;
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(new EventId(), ex, "Attempt {try} failed while connecting", tryNum);
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
            response.Headers.TryGetValues(HeaderNames.Server, out var server);
            response.Headers.TryGetValues(HeaderNames.RequestId, out var requestId);
            _logger.LogDebug(
                "Sent '{method}' request on '{url}', request id {requestId}, server {server}, got status {status} with response: {response}",
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
