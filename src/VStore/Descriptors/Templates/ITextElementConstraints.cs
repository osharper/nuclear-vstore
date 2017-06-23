namespace NuClear.VStore.Descriptors.Templates
{
    public interface ITextElementConstraints : IElementConstraints
    {
        int? MaxSymbols { get; set; }

        bool WithoutControlChars { get; }

        bool WithoutNonBreakingSpace { get; }
    }
}
