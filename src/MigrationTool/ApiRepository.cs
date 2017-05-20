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
using NuClear.VStore.Json;

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

        public async Task<string> CreateObjectAsync(string objectId, string firmId, ObjectDescriptor objectDescriptor)
        {
            var methodUri = new Uri(_objectUri, objectId + "?firm=" + firmId);
            var stringResponse = string.Empty;
            try
            {
                using (var content = new StringContent(JsonConvert.SerializeObject(objectDescriptor, ApiSerializerSettings.Default), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _httpClient.PutAsync(methodUri, content))
                    {
                        stringResponse = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();
                        var res = JsonConvert.DeserializeObject<ObjectDescriptor>(stringResponse, SerializerSettings.Default);
                        if (res == null)
                        {
                            throw new SerializationException("Cannot deserialize response: " + stringResponse);
                        }

                        _logger.LogInformation("Imported object {id} got version: {version}", objectId, res.VersionId);
                        return stringResponse;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(), ex, "Request error while object {id} import with response: {response}", objectId, stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Object {id} import error with response: {response}", objectId, stringResponse);
                throw;
            }
        }

        public async Task CreatePositionAsync(string positionId, object positionDescriptor)
        {
            var methodUri = new Uri(_devUri, "nomenclature");
            var stringResponse = string.Empty;
            try
            {
                using (var content = new StringContent(JsonConvert.SerializeObject(positionDescriptor, SerializerSettings.Default), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _httpClient.PostAsync(methodUri, content))
                    {
                        stringResponse = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();
                        _logger.LogInformation("Position {id} has been created", positionId);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(), ex, "Request error while creating position {id} with response: {response}", positionId, stringResponse);
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
                var stringResponse = string.Empty;
                try
                {
                    using (var response = await _httpClient.SendAsync(req))
                    {
                        stringResponse = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();
                        _logger.LogInformation("Link has been created between position {positionId} and template {templateId}", positionId, templateId);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(new EventId(), ex, "Request error while creating link between position {positionId} and template {templateId} with response: {response}", positionId, templateId, stringResponse);
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
            var stringResponse = string.Empty;
            try
            {
                var descriptor = new
                {
                    template.Id,
                    template.VersionId,
                    template.LastModified,
                    template.Author,
                    template.Properties,
                    template.Elements
                };

                using (var content = new StringContent(JsonConvert.SerializeObject(descriptor, SerializerSettings.Default), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _httpClient.PostAsync(methodUri, content))
                    {
                        stringResponse = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();
                        var res = JsonConvert.DeserializeObject<TemplateDescriptor>(stringResponse, SerializerSettings.Default);
                        if (res == null)
                        {
                            throw new SerializationException("Cannot deserialize response: " + stringResponse);
                        }

                        _logger.LogInformation("Created template {id} got version: {version}", templateId, res.VersionId);
                        return stringResponse;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(), ex, "Request error while template {id} creating with response: {response}", templateId, stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Template {id} creating error", templateId);
                throw;
            }
        }

        public async Task<ApiObjectDescriptor> GetNewObjectAsync(string templateId, string langCode)
        {
            var methodUri = new Uri(_templateUri, $"{templateId}/session?languages={langCode}");
            using (var req = new HttpRequestMessage(HttpMethod.Post, methodUri))
            {
                var stringResponse = string.Empty;
                try
                {
                    using (var response = await _httpClient.SendAsync(req))
                    {
                        stringResponse = await response.Content.ReadAsStringAsync();
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
                                     "Request error while getting new object for template {id} and lang {lang} with response: {response}",
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
            var methodUri = new Uri(_searchUri, "nomenclature/");
            var stringResponse = string.Empty;
            try
            {
                using (var response = await _httpClient.GetAsync(methodUri))
                {
                    stringResponse = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();
                    var descriptors = JsonConvert.DeserializeObject<IReadOnlyCollection<PositionDescriptor>>(stringResponse, ApiSerializerSettings.Default);
                    if (descriptors == null)
                    {
                        throw new SerializationException("Cannot deserialize positions: " + stringResponse);
                    }

                    return descriptors;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(), ex, "Request error while getting positions with response: {response}", stringResponse);
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
            var stringResponse = string.Empty;
            var methodUri = new Uri(_templateUri, templateId);
            try
            {
                using (var response = await _httpClient.GetAsync(methodUri))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogDebug("Template {id} not found", templateId);
                        return null;
                    }

                    response.EnsureSuccessStatusCode();
                    stringResponse = await response.Content.ReadAsStringAsync();
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
                _logger.LogError(new EventId(), ex, "Request error while getting template {id} with response: {response}", templateId, stringResponse);
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

                using (var content = new MultipartFormDataContent())
                {
                    content.Add(new StreamContent(new MemoryStream(file.Data)), fileName, fileName);
                    if (!url.IsAbsoluteUri)
                    {
                        url = new Uri(_storageUri, uploadUrl);
                    }

                    using (var response = await _httpClient.PostAsync(url, content))
                    {
                        stringResponse = await response.Content.ReadAsStringAsync();
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
            var stringResponse = string.Empty;
            var templateId = template.Id.ToString();
            var methodUri = new Uri(_templateUri, templateId + "/version/" + template.VersionId);
            try
            {
                using (var content = new StringContent(JsonConvert.SerializeObject(template, SerializerSettings.Default), Encoding.UTF8, "application/json"))
                {
                    using (var response = await _httpClient.PutAsync(methodUri, content))
                    {
                        stringResponse = await response.Content.ReadAsStringAsync();
                        response.EnsureSuccessStatusCode();
                        var res = JsonConvert.DeserializeObject<TemplateDescriptor>(stringResponse, SerializerSettings.Default);
                        if (res == null)
                        {
                            throw new SerializationException("Cannot deserialize response: " + stringResponse);
                        }

                        _logger.LogInformation("Updated template with {id} got new version: {version}", templateId, res.VersionId);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(new EventId(), ex, "Request error in template {id} update with response: {response}", templateId, stringResponse);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Template {id} update error", templateId);
                throw;
            }
        }

        public async Task EnsureApiAvailable(int pingInterval, int pingTries)
        {
            var tryNum = 0;
            var succeeded = false;
            var healthcheckApiUri = new Uri(_apiUri, "healthcheck");
            var healthcheckStorageUri = new Uri(_storageUri, "/swagger/v1/swagger.json");
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
    }
}
