using System;

using NuClear.VStore.Descriptors.Objects.Persistence;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface IBinaryElementValue : IBinaryElementPersistenceValue
    {
        Uri DownloadUri { get; set; }
    }
}
