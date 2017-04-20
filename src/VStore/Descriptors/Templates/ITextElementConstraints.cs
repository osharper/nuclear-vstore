namespace NuClear.VStore.Descriptors.Templates
{
    public interface ITextElementConstraints : IElementConstraints
    {
        int? MaxSymbols { get; set; }

        bool WithoutControlСhars { get; }

        bool WithoutNonBreakingSpace { get; }
    }
}
