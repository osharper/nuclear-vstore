namespace NuClear.VStore.Events
{
    public interface IEvent
    {
        string Key { get; }
        string Serialize();
    }
}