namespace NuClear.VStore.Descriptors.Templates
{
    public class TextElementConstraints : ITextConstraintSet
    {
        public int? MaxSymbols { get; set; }
        public int? MaxSymbolsPerWord { get; set; }
        public int? MaxLines { get; set; }
        public bool IsFormatted { get; set; }
    }
}