using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ImageSharp;

using Microsoft.Extensions.Logging;

using MigrationTool.Models;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;

namespace MigrationTool
{
    public class ConverterService
    {
        private const int BytesInKilobyte = 1024;
        private readonly ILogger<ConverterService> _logger;

        public ConverterService(ILogger<ConverterService> logger)
        {
            _logger = logger;
        }

        public FileFormat PreprocessImageFile(Models.File file, long templateId, int templateCode, ImageElementConstraints constraints)
        {
            var fileId = file.Id.ToString();
            var templateIdStr = templateId.ToString();
            var templateCodeStr = templateCode.ToString();
            var format = file.ContentType.Replace("image/x-", string.Empty).Replace("image/", string.Empty);

            if (!Enum.TryParse(format, true, out FileFormat fileFormat))
            {
                throw new InvalidOperationException($"Unknown image format '{file.ContentType}'; template code = {templateCodeStr}, template id = {templateIdStr}");
            }

            if (constraints == null)
            {
                throw new InvalidOperationException("Incorrect image constraints; template code = " + templateCodeStr + ", template id = " + templateIdStr);
            }

            if (constraints.SupportedFileFormats.Contains(fileFormat))
            {
                return fileFormat;
            }

            _logger.LogWarning("Image {fileId} (template {templateId}, template code {templateCode}) has format {format}, but supported are: {formats}",
                               fileId,
                               templateIdStr,
                               templateCodeStr,
                               fileFormat,
                               constraints.SupportedFileFormats);

            if (!constraints.SupportedFileFormats.Contains(FileFormat.Png))
            {
                throw new InvalidOperationException("Can't determine image format to convert");
            }

            _logger.LogWarning("Image {fileId} (template {templateId}, template code {templateCode}) will be converted to {format} before uploading",
                               fileId,
                               templateIdStr,
                               templateCodeStr,
                               FileFormat.Png);

            try
            {
                using (var image = Image.Load(file.Data))
                {
                    using (var stream = new MemoryStream())
                    {
                        image.SaveAsPng(stream);
                        file.Data = stream.ToArray();
                    }
                }

                return FileFormat.Png;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Error while converting image; template code = " + templateCodeStr + ", template id = " + templateIdStr, ex);
            }
        }

        public IElementConstraints GetElementTemplateConstraints(ElementDescriptorType elementType, AdvertisementElementTemplate elementTemplate)
        {
            var elementTemplateId = elementTemplate.Id.ToString();
            switch (elementType)
            {
                case ElementDescriptorType.PlainText:
                    if (elementTemplate.MaxSymbolsInWord > elementTemplate.TextLengthRestriction)
                    {
                        _logger.LogWarning(
                            "Element template with {id} has MaxSymbolsInWord larger than TextLengthRestriction ({maxSymbols} and {lengthRestriction}), taking the least one",
                            elementTemplateId,
                            elementTemplate.MaxSymbolsInWord,
                            elementTemplate.TextLengthRestriction);
                    }

                    return new PlainTextElementConstraints
                        {
                            MaxSymbols = elementTemplate.TextLengthRestriction,
                            MaxLines = elementTemplate.TextLineBreaksCountRestriction,
                            MaxSymbolsPerWord = elementTemplate.MaxSymbolsInWord > elementTemplate.TextLengthRestriction
                                                    ? elementTemplate.TextLengthRestriction
                                                    : elementTemplate.MaxSymbolsInWord
                        };

                case ElementDescriptorType.FormattedText:
                    if (elementTemplate.MaxSymbolsInWord > elementTemplate.TextLengthRestriction)
                    {
                        _logger.LogWarning(
                            "Element template with {id} has MaxSymbolsInWord larger than TextLengthRestriction ({maxSymbols} and {lengthRestriction}), taking the least one",
                            elementTemplateId,
                            elementTemplate.MaxSymbolsInWord,
                            elementTemplate.TextLengthRestriction);
                    }

                    return new FormattedTextElementConstraints
                        {
                            MaxSymbols = elementTemplate.TextLengthRestriction,
                            MaxLines = elementTemplate.TextLineBreaksCountRestriction,
                            MaxSymbolsPerWord = elementTemplate.MaxSymbolsInWord > elementTemplate.TextLengthRestriction
                                                    ? elementTemplate.TextLengthRestriction
                                                    : elementTemplate.MaxSymbolsInWord
                        };

                case ElementDescriptorType.FasComment:
                    if (elementTemplate.MaxSymbolsInWord > elementTemplate.TextLengthRestriction)
                    {
                        _logger.LogWarning(
                            "Element template with {id} has MaxSymbolsInWord larger than TextLengthRestriction ({maxSymbols} and {lengthRestriction}), taking the least one",
                            elementTemplateId,
                            elementTemplate.MaxSymbolsInWord,
                            elementTemplate.TextLengthRestriction);
                    }

                    return new PlainTextElementConstraints
                    {
                        MaxSymbols = elementTemplate.TextLengthRestriction,
                        MaxLines = elementTemplate.TextLineBreaksCountRestriction,
                        MaxSymbolsPerWord = elementTemplate.MaxSymbolsInWord > elementTemplate.TextLengthRestriction
                                                    ? elementTemplate.TextLengthRestriction
                                                    : elementTemplate.MaxSymbolsInWord
                    };
                case ElementDescriptorType.Link:
                    if (elementTemplate.MaxSymbolsInWord > elementTemplate.TextLengthRestriction)
                    {
                        _logger.LogWarning(
                            "Element template with {id} has MaxSymbolsInWord larger than TextLengthRestriction ({maxSymbols} and {lengthRestriction}), taking the least one",
                            elementTemplateId,
                            elementTemplate.MaxSymbolsInWord,
                            elementTemplate.TextLengthRestriction);
                    }

                    return new LinkElementConstraints
                    {
                        MaxSymbols = elementTemplate.TextLengthRestriction,
                        MaxLines = elementTemplate.TextLineBreaksCountRestriction,
                        MaxSymbolsPerWord = elementTemplate.MaxSymbolsInWord > elementTemplate.TextLengthRestriction
                                                    ? elementTemplate.TextLengthRestriction
                                                    : elementTemplate.MaxSymbolsInWord
                    };
                case ElementDescriptorType.Image:
                    return new ImageElementConstraints
                    {
                        SupportedImageSizes = ConvertImageDimensionToImageSizes(elementTemplate),
                        IsAlphaChannelRequired = elementTemplate.IsAlphaChannelRequired,
                        MaxFilenameLength = elementTemplate.FileNameLengthRestriction,
                        MaxSize = elementTemplate.FileSizeRestriction * BytesInKilobyte,
                        SupportedFileFormats = ConvertFileExtenstionRestrictionToFileFormats(elementTemplate)
                    };
                case ElementDescriptorType.Article:
                    return new ArticleElementConstraints
                    {
                        MaxFilenameLength = elementTemplate.FileNameLengthRestriction,
                        MaxSize = elementTemplate.FileSizeRestriction * BytesInKilobyte,
                        SupportedFileFormats = ConvertFileExtenstionRestrictionToFileFormats(elementTemplate)
                    };
                case ElementDescriptorType.Date:
                    return new DateElementConstraints();
                case ElementDescriptorType.Phone:
                    return new PhoneElementConstraints();
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementType), elementType, "Unknown ElementDescriptorType");
            }
        }

        private IEnumerable<ImageSize> ConvertImageDimensionToImageSizes(AdvertisementElementTemplate elementTemplate)
        {
            if (string.IsNullOrEmpty(elementTemplate.ImageDimensionRestriction))
            {
                throw new ArgumentException("Image dimension(s) must be set", nameof(elementTemplate.ImageDimensionRestriction));
            }

            var res = new List<ImageSize>();
            var sizes = elementTemplate.ImageDimensionRestriction.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var size in sizes)
            {
                var dims = size.Split('x');
                if (dims.Length != 2 ||
                    !int.TryParse(dims[0], out int width) ||
                    !int.TryParse(dims[1], out int height))
                {
                    throw new ArgumentException("Incorrect image dimension: " + size, nameof(size));
                }

                res.Add(new ImageSize { Height = height, Width = width });
            }

            if (res.Count <= 0)
            {
                throw new ArgumentException("Incorrect image dimension string, no dimensions found: " + elementTemplate.ImageDimensionRestriction);
            }

            return res;
        }

        private IEnumerable<FileFormat> ConvertFileExtenstionRestrictionToFileFormats(AdvertisementElementTemplate elementTemplate)
        {
            if (string.IsNullOrEmpty(elementTemplate.FileExtensionRestriction))
            {
                return Array.Empty<FileFormat>();
            }

            var res = new List<FileFormat>();
            var formats = elementTemplate.FileExtensionRestriction.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var format in formats)
            {
                if (!Enum.TryParse(format, true, out FileFormat fileFormat))
                {
                    throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported file format '" + format + "' in: " + elementTemplate.FileExtensionRestriction);
                }

                res.Add(fileFormat);
            }

            if (res.Count <= 0)
            {
                throw new ArgumentException("Incorrect file formats string, no formats found: " + elementTemplate.FileExtensionRestriction);
            }

            return res;
        }

        public ElementDescriptorType ConvertRestrictionTypeToDescriptorType(AdvertisementElementTemplate elementTemplate)
        {
            switch (elementTemplate.RestrictionType)
            {
                case ElementRestrictionType.Text:
                    return elementTemplate.IsAdvertisementLink
                               ? ElementDescriptorType.Link
                               : elementTemplate.IsPhoneNumber
                                   ? ElementDescriptorType.Phone
                                   : (elementTemplate.FormattedText ? ElementDescriptorType.FormattedText : ElementDescriptorType.PlainText);
                case ElementRestrictionType.Article:
                    return ElementDescriptorType.Article;
                case ElementRestrictionType.Image:
                    return ElementDescriptorType.Image;
                case ElementRestrictionType.FasComment:
                    return ElementDescriptorType.FasComment;
                case ElementRestrictionType.Date:
                    return ElementDescriptorType.Date;
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementTemplate), elementTemplate.RestrictionType, "Unknown ElementRestrictionType");
            }
        }
    }
}
