namespace NuClear.VStore.Descriptors.Templates
{
    public interface ITextElementDescriptor : IElementDescriptor
    {
        int? MaxSymbols { get; set; }
    }
}