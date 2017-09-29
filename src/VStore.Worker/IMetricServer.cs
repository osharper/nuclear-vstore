namespace NuClear.VStore.Worker
{
    public interface IMetricServer
    {
        void Start();

        void Stop();

        bool IsRunning { get; }
    }
}
