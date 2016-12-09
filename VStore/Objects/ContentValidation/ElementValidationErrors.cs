namespace NuClear.VStore.Objects.ContentValidation
{
    public enum ElementValidationErrors
    {
        ControlСharacters = 1,
        UnsupportedTags,
        NestedList,
        NonBreakingSpaceSymbol,
        TooManyLines,
        UnsupportedAttributes,
        UnsupportedListElements,
        InvalidHtml,
        IncorrectLink,
        EmptyList,
        WordsTooLong,
        TextTooLong
    }
}
