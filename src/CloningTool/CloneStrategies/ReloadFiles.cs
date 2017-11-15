using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CloningTool.RestClient;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;

namespace CloningTool.CloneStrategies
{
    public class ReloadFiles : ICloneStrategy
    {
        private const string IdentifiersFilename = "ids.txt";
        private readonly ILogger<CloneAdvertisementsBase> _logger;

        protected ReloadFiles(IRestClientFacade destRestClient, ILogger<CloneAdvertisementsBase> logger)
        {
            _logger = logger;
            DestRestClient = destRestClient;
        }

        public IRestClientFacade DestRestClient { get; }

        public async Task<bool> ExecuteAsync()
        {
            var identifiersList = await ReadIdentifiersAsync();
            if (identifiersList.Count < 1)
            {
                return false;
            }

            var uploadPath = $@".{Path.DirectorySeparatorChar.ToString()}upload";
            var downloadPath = $@".{Path.DirectorySeparatorChar.ToString()}download";
            if (!Directory.Exists(downloadPath))
            {
                Directory.CreateDirectory(downloadPath);
            }

            if (!Directory.Exists(uploadPath))
            {
                throw new InvalidOperationException($"Directory {uploadPath} does not exist");
            }

            foreach (var id in identifiersList)
            {
                try
                {
                    var advertisement = await DestRestClient.GetAdvertisementAsync(id);
                    if (advertisement == null)
                    {
                        _logger.LogError("Advertisement {id} not found", id);
                        continue;
                    }

                    var bitmapElements = advertisement.Elements
                                                      .Where(e => e.Type == ElementDescriptorType.BitmapImage)
                                                      .ToList();

                    if (bitmapElements.Count > 0)
                    {
                        var isChanged = false;
                        foreach (var bitmapElement in bitmapElements)
                        {
                            var value = bitmapElement.Value as IBinaryElementValue ?? throw new InvalidCastException(id.ToString());
                            if (string.IsNullOrEmpty(value.Raw))
                            {
                                continue;
                            }

                            var file = await DestRestClient.DownloadFileAsync(id, value.DownloadUri);
                            var name = id.ToString() + "_" + bitmapElement.TemplateCode.ToString() + "_" + Path.GetFileNameWithoutExtension(value.Filename);
                            var fileName = Path.Combine(downloadPath, Path.ChangeExtension(name, Path.GetExtension(value.Raw)));
                            using (var stream = File.Create(fileName))
                            {
                                await stream.WriteAsync(file, 0, file.Length);
                            }

                            var uploadedFileName = id.ToString() + "_" + Path.GetFileName(value.Filename);
                            var uploadedFilePath = Path.Combine(uploadPath, uploadedFileName);
                            if (!File.Exists(uploadedFilePath))
                            {
                                _logger.LogError("File {filename} not found", uploadedFilePath);
                            }

                            byte[] data;
                            using (var replacementFile = File.OpenRead(uploadedFilePath))
                            {
                                data = new byte[(int)replacementFile.Length];
                                await replacementFile.ReadAsync(data, 0, (int)replacementFile.Length);
                            }

                            var newValue = await DestRestClient.UploadFileAsync(id, new Uri(bitmapElement.UploadUrl), value.Filename, data);
                            bitmapElement.Value = newValue;
                            isChanged = true;
                            _logger.LogInformation("Bitmap within advertisement {id} has been replaced with file {name} (was {oldRaw})", id, uploadedFileName, value.Raw);
                        }

                        if (isChanged)
                        {
                            await DestRestClient.UpdateAdvertisementAsync(advertisement);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Advertisement {id} has not any bitmap image elements", id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(new EventId(), ex, "Error with advertisement {id}", id);
                }
            }

            return true;
        }

        private async Task<IReadOnlyCollection<long>> ReadIdentifiersAsync()
        {
            if (!File.Exists(IdentifiersFilename))
            {
                _logger.LogError("File {name} with identifiers does not exist", IdentifiersFilename);
                return Array.Empty<long>();
            }

            var identifiersList = new HashSet<long>();
            using (var file = File.OpenText(IdentifiersFilename))
            {
                var lineCount = 0;
                while (!file.EndOfStream)
                {
                    ++lineCount;
                    var line = await file.ReadLineAsync();
                    if (!long.TryParse(line, out var value))
                    {
                        _logger.LogWarning("Cannot convert line {num} to number: {line}", lineCount, line);
                        continue;
                    }

                    identifiersList.Add(value);
                }
            }

            return identifiersList;
        }
    }
}
