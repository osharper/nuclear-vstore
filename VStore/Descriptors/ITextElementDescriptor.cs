namespace NuClear.VStore.Descriptors
{
    public interface ITextElementDescriptor : IElementDescriptor
    {
        int? MaxSymbols { get; set; }
    }
}