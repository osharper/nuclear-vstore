using Prometheus.Client;

namespace NuClear.VStore.Prometheus
{
    public static class Labels
    {
        public static class Backends
        {
            public const string Ceph = "ceph";
        }
    }

    public sealed class MetricsProvider
    {
        private const string JoinSeparator = "_";

        private readonly Histogram _requestDurationMs =
            Metrics.CreateHistogram(
                string.Join(JoinSeparator, Names.RequestDurationMetric, NonBaseUnits.Milliseconds),
                "Request duration in milliseconds",
                new double[] { 5, 10, 50, 100, 150, 200, 250, 300, 400, 500, 800, 1000, 1500, 2000, 5000, 10000, 15000, 20000 },
                Names.BackendLabel,
                Names.TypeLabel,
                Names.MethodLabel);

        private readonly Counter _requestErrors =
            Metrics.CreateCounter(Names.RequestErrorsMetric, "Request errors count", Names.BackendLabel, Names.TypeLabel, Names.MethodLabel);

        public Histogram GetRequestDurationMsMetric() => _requestDurationMs;

        public Counter GetRequestErrorsMetric() => _requestErrors;

        private static class Names
        {
            public const string RequestDurationMetric = "request_duration";
            public const string RequestErrorsMetric = "request_errors";
            public const string BackendLabel = "backend";
            public const string TypeLabel = "type";
            public const string MethodLabel = "method";
        }

        private static class NonBaseUnits
        {
            public const string Milliseconds = "milliseconds";
        }
    }
}
