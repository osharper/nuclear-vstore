namespace NuClear.VStore.Descriptors
{
    public interface ITextElementDescriptor : IElementDescriptor
    {
        int? MinSymbolsPerWord { get; set; }
    }
}