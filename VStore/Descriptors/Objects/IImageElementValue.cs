using System;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface IImageElementValue : IBinaryElementValue
    {
        Uri PreviewUri { get; set; }
    }
}