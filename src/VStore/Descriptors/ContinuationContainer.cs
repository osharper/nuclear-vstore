using System.Collections.Generic;

namespace NuClear.VStore.Descriptors
{
    public sealed class ContinuationContainer<T>
    {
        public ContinuationContainer(IReadOnlyCollection<T> collection, string continuationToken)
        {
            Collection = collection;
            ContinuationToken = continuationToken;
        }

        public IReadOnlyCollection<T> Collection { get; }
        public string ContinuationToken { get; }
    }
}