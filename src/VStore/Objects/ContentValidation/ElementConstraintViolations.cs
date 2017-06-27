namespace NuClear.VStore.Objects.ContentValidation
{
    public enum ElementConstraintViolations
    {
        MaxLines,
        MaxSymbols,
        MaxSymbolsPerWord,
        WithoutControlChars,
        WithoutNonBreakingSpace,
        ValidHtml,
        SupportedTags,
        SupportedAttributes,
        SupportedListElements,
        NoEmptyLists,
        NoNestedLists,
        ValidLink,
        ValidDateRange,
        MaxSize,
        MaxFilenameLength,
        SupportedFileFormats,
        BinaryExists,
        ValidArticle,
        ValidImage
    }
}
