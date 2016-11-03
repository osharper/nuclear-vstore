namespace NuClear.VStore.Descriptors.Templates
{
    public interface ITextConstraintSet : IConstraintSet
    {
        int? MaxSymbols { get; set; }
    }
}