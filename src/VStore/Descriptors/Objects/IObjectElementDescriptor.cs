using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface IObjectElementDescriptor : IElementDescriptor, IIdentifyable<long>
    {
        IObjectElementValue Value { get; }
    }
}