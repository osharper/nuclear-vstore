using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CloningTool.Json;

using NuClear.VStore.Descriptors.Templates;

namespace CloningTool.RestClient
{
    public interface IReadOnlyRestClientFacade
    {
        Task<string> GetAdvertisementVersionAsync(long id);
        Task<IReadOnlyCollection<PositionDescriptor>> GetContentPositionsAsync();
        Task<TemplateDescriptor> GetTemplateAsync(string templateId);
        Task<IReadOnlyCollection<ApiListTemplate>> GetTemplatesAsync();
        Task<IReadOnlyCollection<ApiListAdvertisement>> GetAdvertisementsByTemplateAsync(long templateId, int? fetchSize);
        Task<ApiObjectDescriptor> GetAdvertisementAsync(long advertisementId);
        Task<byte[]> DownloadFileAsync(long advertisementId, Uri downloadUrl);
        Task EnsureApiAvailableAsync(int initialPingInterval, int initialPingTries);
    }
}
