using NuClear.VStore.Descriptors;

namespace NuClear.VStore.Sessions.UploadParams
{
    public sealed class CustomImageFileUploadParams : IFileUploadParams
    {
        public CustomImageFileUploadParams(ImageSize size)
        {
            Size = size;
        }

        public ImageSize Size { get; }
    }
}