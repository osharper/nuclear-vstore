using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Sessions.ContentValidation.Errors;

namespace NuClear.VStore.Sessions.ContentValidation
{
    public static class SvgValidationUtils
    {
        private const string ImageElementName = "image";
        private const string PathElementName = "path";
        private const string StyleElementName = "style";
        private const string StyleAttributeName = "style";
        private const string PathDefinitionAttributeName = "d";
        private const string ClosePathCommand = "z";
        private const string CssUrlToken = "url(";
        private static readonly IReadOnlyCollection<string> GradientElements =
            new HashSet<string> { "meshgradient", "lineargradient", "radialgradient", "stop" };

        private static readonly IReadOnlyCollection<string> NonRenderedElements =
            new HashSet<string> { "mask", "clippath", "hatch", "marker", "pattern", "script", "symbol" };

        public static IEnumerable<BinaryValidationError> EnsureNoGradient(XElement rootNode, VectorImageElementConstraints elementConstraints)
        {
            if (!elementConstraints.WithoutGradient)
            {
                return Array.Empty<BinaryValidationError>();
            }

            var gradientElements = new HashSet<string>(rootNode.Descendants()
                                                               .Select(e => e.Name.LocalName.ToLowerInvariant())
                                                               .Where(name => GradientElements.Contains(name)));

            return gradientElements.Count > 0
                       ? new[] { new ImageWithGradientError(gradientElements) }
                       : Array.Empty<BinaryValidationError>();
        }

        public static IEnumerable<BinaryValidationError> EnsureNoBitmaps(XElement rootNode, VectorImageElementConstraints elementConstraints)
        {
            if (!elementConstraints.WithoutBitmaps)
            {
                return Array.Empty<BinaryValidationError>();
            }

            return rootNode.Descendants().Any(e => ImageElementName.Equals(e.Name.LocalName, StringComparison.OrdinalIgnoreCase))
                       ? new[] { new ImageWithBitmapsError() }
                       : Array.Empty<BinaryValidationError>();
        }

        public static IEnumerable<BinaryValidationError> EnsureWithoutNonRenderedElements(XElement rootNode, VectorImageElementConstraints elementConstraints)
        {
            if (!elementConstraints.WithoutNonRenderedElements)
            {
                return Array.Empty<BinaryValidationError>();
            }

            var nonRenderedElements = new HashSet<string>(rootNode.Descendants()
                                                                  .Select(e => e.Name.LocalName.ToLowerInvariant())
                                                                  .Where(name => NonRenderedElements.Contains(name)));

            return nonRenderedElements.Count > 0
                       ? new[] { new ImageWithNonRenderedElementsError(nonRenderedElements) }
                       : Array.Empty<BinaryValidationError>();
        }

        public static IEnumerable<BinaryValidationError> EnsureNoUrlInStyles(XElement rootNode, VectorImageElementConstraints elementConstraints)
        {
            if (!elementConstraints.WithoutUrlInStyles)
            {
                return Array.Empty<BinaryValidationError>();
            }

            if (rootNode.Attributes().Any(a => IsStyleAttribute(a) && StyleContainsUrl(a.Value)))
            {
                return new[] { new ImageWithUrlInStylesError() };
            }

            if (rootNode.Descendants().Any(e => IsStyleElement(e) && StyleContainsUrl(e.Value)))
            {
                return new[] { new ImageWithUrlInStylesError() };
            }

            return rootNode.Descendants().Any(e => e.Attributes().Any(a => IsStyleAttribute(a) && StyleContainsUrl(a.Value)))
                       ? new[] { new ImageWithUrlInStylesError() }
                       : Array.Empty<BinaryValidationError>();
        }

        public static IEnumerable<BinaryValidationError> EnsureNoUnclosedPaths(XElement rootNode, VectorImageElementConstraints elementConstraints)
        {
            if (!elementConstraints.PathsAreClosed)
            {
                return Array.Empty<BinaryValidationError>();
            }

            return rootNode.Descendants().Any(e => IsPathElement(e) && IsUnclosedPath(e))
                       ? new[] { new ImageWithUnclosedPathsError() }
                       : Array.Empty<BinaryValidationError>();
        }

        // https://www.w3.org/TR/SVG/paths.html#PathDataClosePathCommand
        private static bool IsUnclosedPath(XElement e) =>
            e.Attributes().Any(a => PathDefinitionAttributeName.Equals(a.Name.LocalName, StringComparison.OrdinalIgnoreCase) &&
                                    !a.Value.TrimEnd().EndsWith(ClosePathCommand, StringComparison.OrdinalIgnoreCase));

        private static bool IsPathElement(XElement e) =>
            PathElementName.Equals(e.Name.LocalName, StringComparison.OrdinalIgnoreCase);

        private static bool IsStyleAttribute(XAttribute a) =>
            StyleAttributeName.Equals(a.Name.LocalName, StringComparison.OrdinalIgnoreCase);

        private static bool IsStyleElement(XElement e) =>
            StyleElementName.Equals(e.Name.LocalName, StringComparison.OrdinalIgnoreCase);

        // https://www.w3.org/TR/css-syntax-3/#url-token-diagram
        private static bool StyleContainsUrl(string styleContent) =>
            styleContent.IndexOf(CssUrlToken, StringComparison.OrdinalIgnoreCase) != -1;
    }
}
