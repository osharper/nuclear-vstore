namespace NuClear.VStore.Host.Descriptors
{
    public interface ITextElementDescriptor : IElementDescriptor
    {
        int? MinSymbolsPerWord { get; set; }
    }
}