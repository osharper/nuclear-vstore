using System.Collections.Generic;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface ILogoElementValue : IImageElementValue
    {
        CropShape CropShape { get; set; }
        CropArea CropArea { get; set; }
        IEnumerable<CustomImage> CustomImages { get; set; }
    }
}