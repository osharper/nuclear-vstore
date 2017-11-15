using System.IO;
using System.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Sessions.ContentValidation;
using NuClear.VStore.Sessions.ContentValidation.Errors;

using Xunit;

namespace VStore.UnitTests.Validation
{
    public class VectorImageValidationTests
    {
        private const string SvgTemplate =
@"<?xml version=""1.0"" standalone=""no""?>
<!DOCTYPE svg PUBLIC ""-//W3C//DTD SVG 1.1//EN"" ""http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"">
<svg width=""4in"" height=""3in"" version=""1.1""
     xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink"">
  {0}
</svg>";

        [Fact]
        public void TestPdfValidationCheck()
        {
            void TestAction(int templateCode, Stream stream) =>
                VectorImageValidator.ValidateVectorImageHeader(templateCode, FileFormat.Pdf, stream);

            TestHelpers.MakeBinaryValidationCheck("%PDF-12356", TestAction);

            TestHelpers.MakeBinaryValidationCheck<InvalidImageError>(
                "1%PDF-",
                TestAction,
                nameof(IImageElementConstraints.ValidImage));

            TestHelpers.MakeBinaryValidationCheck<InvalidImageError>(
                string.Empty,
                TestAction,
                nameof(IImageElementConstraints.ValidImage));
        }

        [Fact]
        public void TestSvgValidationCheck()
        {
            var constraints = new VectorImageElementConstraints();
            void TestAction(int templateCode, Stream stream) =>
                VectorImageValidator.ValidateVectorImage(templateCode, FileFormat.Svg, constraints, stream);

            TestHelpers.MakeBinaryValidationCheck(
                "<svg><title /><style></style><metadata /><defs /></svg>",
                TestAction);

            TestHelpers.MakeBinaryValidationCheck<InvalidImageError>(
                "svg",
                TestAction,
                nameof(IImageElementConstraints.ValidImage));

            TestHelpers.MakeBinaryValidationCheck<InvalidImageError>(
                string.Empty,
                TestAction,
                nameof(IImageElementConstraints.ValidImage));
        }

        [Theory]
        [InlineData("<image x=\"200\" y=\"200\" width=\"100px\" height=\"100px\" xlink:href=\"myimage.png\" />")]
        [InlineData("<g><Image></Image></g>")]
        public void TestSvgWithBitmaps(string svgContent)
        {
            var constraints = new VectorImageElementConstraints();
            void TestAction(int templateCode, Stream stream) =>
                VectorImageValidator.ValidateVectorImage(templateCode, FileFormat.Svg, constraints, stream);

            TestHelpers.MakeBinaryValidationCheck<ImageWithBitmapsError>(
                string.Format(SvgTemplate, svgContent),
                TestAction,
                nameof(VectorImageElementConstraints.WithoutBitmaps));
        }

        [Theory]
        [InlineData("<lineargradient />")]
        [InlineData("<linearGradient />")]
        [InlineData("<g><g><LinearGradient /></g><LinearGradient /></g>")]
        [InlineData("<RADIALGradient />")]
        [InlineData("<g><meshGRAdient></meshGRAdient></g>")]
        [InlineData("<stop />")]
        [InlineData("<sToP />")]
        [InlineData("<g><g><LinearGradient /></g><radialGradient /><RadialGradient /></g>", 2)]
        public void TestSvgWithGradient(string svgContent, int gradientElementsCount = 1)
        {
            var constraints = new VectorImageElementConstraints();
            const string ExpectedErrorType = nameof(VectorImageElementConstraints.WithoutGradient);
            void TestAction(int templateCode, Stream stream) =>
                VectorImageValidator.ValidateVectorImage(templateCode, FileFormat.Svg, constraints, stream);

            var error = TestHelpers.MakeBinaryValidationCheck<ImageWithGradientError>(
                string.Format(SvgTemplate, svgContent),
                TestAction,
                ExpectedErrorType);

            Assert.Equal(gradientElementsCount, error.GradientElements.Count);
        }

        [Fact]
        public void TestSvgWithClosedPaths()
        {
            var constraints = new VectorImageElementConstraints();
            void TestAction(int templateCode, Stream stream) =>
                VectorImageValidator.ValidateVectorImage(templateCode, FileFormat.Svg, constraints, stream);

            TestHelpers.MakeBinaryValidationCheck(string.Format(SvgTemplate, "<path />"), TestAction);
            TestHelpers.MakeBinaryValidationCheck(
                string.Format(SvgTemplate, "<path d=\"M 100 100 L 300 100 L 200 300 z\" fill=\"red\" stroke=\"blue\" stroke-width=\"3\" />"),
                TestAction);

            TestHelpers.MakeBinaryValidationCheck(
                string.Format(SvgTemplate, "<g><path d=\"M 100 100 L 300 100 L 200 300 Z \" /></g>"),
                TestAction);
        }

        [Theory]
        [InlineData("<path d=\"\" />")]
        [InlineData("<g><PATH D=\"M 100 100 L 300 100 L 200 300 z\" /><path d=\"L 200 300\" /></g>")]
        public void TestSvgWithUnclosedPaths(string svgContent)
        {
            var constraints = new VectorImageElementConstraints();
            const string ExpectedErrorType = nameof(VectorImageElementConstraints.PathsAreClosed);
            void TestAction(int templateCode, Stream stream) =>
                VectorImageValidator.ValidateVectorImage(templateCode, FileFormat.Svg, constraints, stream);

            TestHelpers.MakeBinaryValidationCheck<ImageWithUnclosedPathsError>(
                string.Format(SvgTemplate, svgContent),
                TestAction,
                ExpectedErrorType);
        }

        [Theory]
        [InlineData("clipPath")]
        [InlineData("ClipPath")]
        [InlineData("Hatch")]
        [InlineData("hatch")]
        [InlineData("Marker")]
        [InlineData("marker")]
        [InlineData("MASK")]
        [InlineData("mask")]
        [InlineData("paTTerN")]
        [InlineData("pattern")]
        [InlineData("scRipT")]
        [InlineData("script")]
        [InlineData("SYMBOL")]
        [InlineData("symbol")]
        public void TestSvgWithNonRenderedElements(string svgElementName)
        {
            var constraints = new VectorImageElementConstraints();
            const string ExpectedErrorType = nameof(VectorImageElementConstraints.WithoutNonRenderedElements);
            void TestAction(int templateCode, Stream stream) =>
                VectorImageValidator.ValidateVectorImage(templateCode, FileFormat.Svg, constraints, stream);

            TestHelpers.MakeBinaryValidationCheck<ImageWithNonRenderedElementsError>(
                string.Format(SvgTemplate, $"<{svgElementName} />"),
                TestAction,
                ExpectedErrorType);

            var error = TestHelpers.MakeBinaryValidationCheck<ImageWithNonRenderedElementsError>(
                string.Format(SvgTemplate, $"<g><path /><{svgElementName} /><{svgElementName} /></g>"),
                TestAction,
                ExpectedErrorType);

            Assert.Single(error.NonRenderedElements);
            Assert.Equal(svgElementName.ToLowerInvariant(), error.NonRenderedElements.First());
        }

        [Theory]
        [InlineData("url()")]
        [InlineData("uRl()")]
        [InlineData("URL()")]
        [InlineData("background: url('topbanner.png') #00D no-repeat fixed;")]
        [InlineData("background: url( topbanner.png ) #00D no-repeat fixed;")]
        [InlineData("ul { list-style: square url(http://www.example.com/redball.png); }")]
        public void TestSvgWithUrlInStyles(string styleContent)
        {
            var constraints = new VectorImageElementConstraints();
            const string ExpectedErrorType = nameof(VectorImageElementConstraints.WithoutUrlInStyles);
            void TestAction(int templateCode, Stream stream) =>
                VectorImageValidator.ValidateVectorImage(templateCode, FileFormat.Svg, constraints, stream);

            TestHelpers.MakeBinaryValidationCheck<ImageWithUrlInStylesError>(
                string.Format(SvgTemplate, $"<circle style=\"{styleContent}\" />"),
                TestAction,
                ExpectedErrorType);

            TestHelpers.MakeBinaryValidationCheck<ImageWithUrlInStylesError>(
                string.Format(SvgTemplate, $"<g><rect style=\"{styleContent}\" /></g>"),
                TestAction,
                ExpectedErrorType);

            TestHelpers.MakeBinaryValidationCheck<ImageWithUrlInStylesError>(
                $"<svg style=\"{styleContent}\" />",
                TestAction,
                ExpectedErrorType);

            TestHelpers.MakeBinaryValidationCheck<ImageWithUrlInStylesError>(
                string.Format(SvgTemplate, $"<style>/* <![CDATA[ */{styleContent}/* ]]> */</style>"),
                TestAction,
                ExpectedErrorType);
        }
    }
}
