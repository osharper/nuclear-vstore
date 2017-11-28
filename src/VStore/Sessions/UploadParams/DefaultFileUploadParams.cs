namespace NuClear.VStore.Sessions.UploadParams
{
    public sealed class DefaultFileUploadParams : IFileUploadParams
    {
        private DefaultFileUploadParams()
        {
        }

        public static readonly DefaultFileUploadParams Instance = new DefaultFileUploadParams();
    }
}