namespace NuClear.VStore.Descriptors.Objects.Persistence
{
    public interface IBinaryElementPersistenceValue : IObjectElementRawValue
    {
        string Filename { get; }
        long? Filesize { get; }
    }
}