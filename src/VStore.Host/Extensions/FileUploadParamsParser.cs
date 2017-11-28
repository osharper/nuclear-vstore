using System;
using System.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Sessions.UploadParams;

namespace NuClear.VStore.Host.Extensions
{
    public sealed class FileUploadParamsParser
    {
        public bool TryParse(string rawFileUploadParams, out IFileUploadParams fileUploadParams)
        {
            fileUploadParams = null;
            if (string.IsNullOrEmpty(rawFileUploadParams))
            {
                fileUploadParams = DefaultFileUploadParams.Instance;
                return true;
            }

            var tokens = rawFileUploadParams.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (!tokens.Any())
            {
                return false;
            }

            var type = tokens[0];
            switch (type)
            {
                case "custom-image":
                    if (tokens.Length != 2)
                    {
                        return false;
                    }

                    var sizeTokens = tokens[1].Split('x');
                    if (sizeTokens.Length != 2 || !int.TryParse(sizeTokens[0], out var width) || !int.TryParse(sizeTokens[1], out var height))
                    {
                        return false;
                    }

                    fileUploadParams = new CustomImageFileUploadParams(new ImageSize { Width = width, Height = height });
                    return true;
                default:
                    return false;
            }
        }
    }
}