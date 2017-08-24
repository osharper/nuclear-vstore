using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ImageSharp;

using Microsoft.Extensions.Logging;

using MigrationTool.Json;
using MigrationTool.Models;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;

namespace MigrationTool
{
    public class ConverterService
    {
        private const int BytesInKilobyte = 1024;
        private const long VideoElementTemplateIdentifier = 1005145157231706304L;
        private readonly ILogger<ConverterService> _logger;

        private static readonly IReadOnlyDictionary<string, FileFormat> MimeMapping = new Dictionary<string, FileFormat>
            {
                { "application/pdf", FileFormat.Pdf },
                { "image/bmp", FileFormat.Bmp },
                { "image/gif", FileFormat.Gif },
                { "image/jpeg", FileFormat.Jpeg },
                { "image/png", FileFormat.Png },
                { "image/svg+xml", FileFormat.Svg },
                { "image/x-png", FileFormat.Png }
            };

        private static readonly IReadOnlyCollection<string> VectorFileFormats = new[] { FileFormat.Pdf.ToString(), FileFormat.Svg.ToString() };

        public ConverterService(ILogger<ConverterService> logger)
        {
            _logger = logger;
        }

        public FileFormat DetectFileFormat(Models.File file, int templateCode)
        {
            if (!MimeMapping.ContainsKey(file.ContentType))
            {
                throw new InvalidOperationException($"Cannot determine file format by content type '{file.ContentType}', template code = {templateCode}");
            }

            return MimeMapping[file.ContentType];
        }

        public FileFormat PreprocessImageFile(Models.File file, long templateId, int templateCode, BitmapImageElementConstraints constraints)
        {
            var fileId = file.Id.ToString();
            var templateIdStr = templateId.ToString();
            var templateCodeStr = templateCode.ToString();
            if (constraints == null)
            {
                throw new InvalidOperationException("Incorrect image constraints; template code = " + templateCodeStr + ", template id = " + templateIdStr);
            }

            var fileFormat = DetectFileFormat(file, templateCode);
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
                case ElementDescriptorType.FasComment:
                    if (elementTemplate.MaxSymbolsInWord > elementTemplate.TextLengthRestriction)
                    {
                        _logger.LogWarning(
                            "Element template with id {id} has MaxSymbolsInWord larger than TextLengthRestriction ({maxSymbols} and {lengthRestriction}), taking the least one",
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
                            "Element template with id {id} has MaxSymbolsInWord larger than TextLengthRestriction ({maxSymbols} and {lengthRestriction}), taking the least one",
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
                case ElementDescriptorType.Link:
                case ElementDescriptorType.VideoLink:
                    return new LinkElementConstraints
                    {
                        MaxSymbols = elementTemplate.TextLengthRestriction
                    };
                case ElementDescriptorType.BitmapImage:
                    return new BitmapImageElementConstraints
                    {
                        SupportedImageSizes = ConvertImageDimensionToImageSizes(elementTemplate),
                        IsAlphaChannelRequired = elementTemplate.IsAlphaChannelRequired,
                        MaxFilenameLength = elementTemplate.FileNameLengthRestriction,
                        MaxSize = elementTemplate.FileSizeRestriction * BytesInKilobyte,
                        SupportedFileFormats = ConvertFileExtenstionRestrictionToFileFormats(elementTemplate)
                    };
                case ElementDescriptorType.VectorImage:
                    return new VectorImageElementConstraints
                    {
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
            if (elementTemplate.Id == VideoElementTemplateIdentifier)
            {
                return ElementDescriptorType.VideoLink;
            }

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
                    return VectorFileFormats.Contains(elementTemplate.FileExtensionRestriction, StringComparer.OrdinalIgnoreCase)
                        ? ElementDescriptorType.VectorImage
                        : ElementDescriptorType.BitmapImage;
                case ElementRestrictionType.FasComment:
                    return ElementDescriptorType.FasComment;
                case ElementRestrictionType.Date:
                    throw new NotSupportedException("Date restriction type is not supported");
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementTemplate), elementTemplate.RestrictionType, "Unknown ElementRestrictionType");
            }
        }

        public ModerationResult GetAdvertisementModerationStatus(Advertisement advertisement)
        {
            if (advertisement.AdvertisementElements.All(
                ae => (AdvertisementElementStatusValue)ae.AdvertisementElementStatus.Status == AdvertisementElementStatusValue.Valid))
            {
                return new ModerationResult { Status = ModerationStatus.Approved, Comment = string.Empty };
            }

            if (advertisement.AdvertisementElements.Any(
                ae => (AdvertisementElementStatusValue)ae.AdvertisementElementStatus.Status == AdvertisementElementStatusValue.ReadyForValidation
                      || (AdvertisementElementStatusValue)ae.AdvertisementElementStatus.Status == AdvertisementElementStatusValue.Draft))
            {
                return new ModerationResult { Status = ModerationStatus.OnApproval, Comment = string.Empty };
            }

            if (advertisement.AdvertisementElements.Any(
                ae => (AdvertisementElementStatusValue)ae.AdvertisementElementStatus.Status == AdvertisementElementStatusValue.Invalid))
            {
                var denialReasons = advertisement.AdvertisementElements
                                                 .SelectMany(ae => ae.AdvertisementElementDenialReasons)
                                                 .Select(aedr => string.IsNullOrEmpty(aedr.Comment) ? aedr.DenialReason.Name : $"{aedr.DenialReason.Name} - {aedr.Comment}");

                return new ModerationResult
                    {
                        Status = ModerationStatus.Approved,
                        Comment = string.Join("; ", denialReasons)
                    };
            }

            throw new NotImplementedException($"Cannot determine moderation status for advertisement {advertisement.Id.ToString()}");
        }

        public string ConvertFasCommentType(AdvertisementElement element, IElementDescriptor newElement)
        {
            if (!element.FasCommentType.HasValue)
            {
                return null;
            }

            var raw = GetFasCommentType(element.FasCommentType.Value);
            var allowedText = newElement.Properties
                .Property("fasComments")
                .FirstOrDefault(fc => fc.Value<string>("raw") == raw)
                ?.Value<string>("text");

            return allowedText != element.Text ? "custom" : raw;
        }

        private static string GetFasCommentType(FasComment fasComment)
        {
            switch (fasComment)
            {
                case FasComment.NewFasComment:
                    return "custom";
                case FasComment.RussiaAlcohol:
                case FasComment.CyprusAlcohol:
                case FasComment.ChileAlcohol:
                case FasComment.CzechAlcoholAdvertising:
                case FasComment.UkraineAlcohol:
                case FasComment.KyrgyzstanAlcohol:
                    return "alcohol";
                case FasComment.RussiaSupplements:
                case FasComment.CyprusSupplements:
                    return "supplements";
                case FasComment.RussiaDrugs:
                case FasComment.CyprusDrugs:
                case FasComment.UkraineDrugs:
                    return "drugs";
                case FasComment.CyprusDrugsAndService:
                case FasComment.ChileDrugsAndService:
                    return "drugsAndService";
                case FasComment.CzechMedsMultiple:
                    return "medsMultiple";
                case FasComment.CzechMedsSingle:
                    return "medsSingle";
                case FasComment.CzechDietarySupplement:
                    return "dietarySupplement";
                case FasComment.CzechSpecialNutrition:
                    return "specialNutrition";
                case FasComment.CzechChildNutrition:
                    return "childNutrition";
                case FasComment.CzechFinancilaServices:
                    return "financialServices";
                case FasComment.CzechMedsTraditional:
                    return "medsTraditional";
                case FasComment.CzechBiocides:
                    return "biocides";
                case FasComment.ChileMedicalReceiptDrugs:
                    return "medicalReceiptDrugs";
                case FasComment.UkraineAutotherapy:
                    return "autotherapy";
                case FasComment.UkraineMedicalDevice:
                    return "medicalDevice";
                case FasComment.UkraineSoundPhonogram:
                    return "soundPhonogram";
                case FasComment.UkraineSoundLive:
                    return "soundLive";
                case FasComment.UkraineEmploymentAssistance:
                    return "employmentAssistance";
                case FasComment.KyrgyzstanCertificateRequired:
                    return "certificateRequired";
                default:
                    throw new ArgumentOutOfRangeException(nameof(fasComment), fasComment, "Unsupported FAS comment type");
            }
        }
    }
}
