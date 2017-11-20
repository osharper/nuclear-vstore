namespace NuClear.VStore.Templates
{
    public enum TemplateElementValidationErrors
    {
        NegativeMaxSymbols,
        NegativeMaxSymbolsPerWord,
        NegativeMaxFilenameLength,
        NegativeMaxSize,
        NegativeMaxLines,
        NegativeImageSizeDimension,
        MissingSupportedFileFormats,
        MissingSupportedImageSizes,
        EmptySupportedFileFormats,
        EmptySupportedImageSizes,
        NonUniqueTemplateCode,
        InvalidMaxSymbolsPerWord,
        InvalidImageSize,
        UnsupportedArticleFileFormat,
        UnsupportedImageFileFormat,
        MaxSizeLimitExceeded,
        InvalidImageSizeRange,
        CustomImageSizeRangeOverlapsWithOriginal
    }
}
