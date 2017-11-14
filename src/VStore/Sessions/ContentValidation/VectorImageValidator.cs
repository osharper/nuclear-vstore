using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Sessions.ContentValidation.Errors;

namespace NuClear.VStore.Sessions.ContentValidation
{
    public static class VectorImageValidator
    {
        private const string PdfHeader = "%PDF-";
        private const string SvgRootName = "svg";

        private delegate void SvgValidationRule(int templateCode, XElement rootNode, VectorImageElementConstraints constraints);

        private static readonly IEnumerable<SvgValidationRule> SvgValidationRules =
            new SvgValidationRule[]
                {
                    SvgValidationUtils.EnsureNoBitmaps,
                    SvgValidationUtils.EnsureNoGradient,
                    SvgValidationUtils.EnsureWithoutNonRenderedElements,
                    SvgValidationUtils.EnsureNoUrlInStyles,
                    SvgValidationUtils.EnsureNoUnclosedPaths
                };

        public static void ValidateVectorImageHeader(int templateCode, FileFormat fileFormat, Stream inputStream)
        {
            switch (fileFormat)
            {
                case FileFormat.Svg:
                    break;
                case FileFormat.Pdf:
                    ValidatePdfHeader(templateCode, inputStream);
                    break;
                case FileFormat.Png:
                case FileFormat.Gif:
                case FileFormat.Bmp:
                case FileFormat.Chm:
                case FileFormat.Jpg:
                case FileFormat.Jpeg:
                    throw new NotSupportedException($"Not vector image file format {fileFormat}");
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileFormat), fileFormat, "Unsupported file format");
            }
        }

        public static void ValidateVectorImage(int templateCode, FileFormat fileFormat, VectorImageElementConstraints elementConstraints, Stream inputStream)
        {
            switch (fileFormat)
            {
                case FileFormat.Svg:
                    ValidateSvg(templateCode, elementConstraints, inputStream);
                    break;
                case FileFormat.Pdf:
                    break;
                case FileFormat.Png:
                case FileFormat.Gif:
                case FileFormat.Bmp:
                case FileFormat.Chm:
                case FileFormat.Jpg:
                case FileFormat.Jpeg:
                    throw new NotSupportedException($"Not vector image file format {fileFormat}");
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileFormat), fileFormat, "Unsupported file format");
            }
        }

        private static void ValidatePdfHeader(int templateCode, Stream inputStream)
        {
            string header;
            var position = inputStream.Position;
            inputStream.Seek(0, SeekOrigin.Begin);
            using (var br = new BinaryReader(inputStream, Encoding.ASCII, true))
            {
                header = new string(br.ReadChars(PdfHeader.Length));
            }

            inputStream.Position = position;
            if (!header.StartsWith(PdfHeader))
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }
        }

        private static void ValidateSvg(int templateCode, VectorImageElementConstraints elementConstraints, Stream inputStream)
        {
            XDocument xdoc;
            try
            {
                xdoc = XDocument.Load(inputStream);
            }
            catch (XmlException)
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }

            if (xdoc.Root == null || !SvgRootName.Equals(xdoc.Root.Name.LocalName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }

            foreach (var validationRule in SvgValidationRules)
            {
                validationRule(templateCode, xdoc.Root, elementConstraints);
            }
        }
    }
}
