using Prometheus.Client;

namespace NuClear.VStore.Prometheus
{
    public static class Labels
    {
        public static class Backends
        {
            public const string Aws = "aws";
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

        private readonly Counter _uploadedBinaries =
            Metrics.CreateCounter(Names.UploadedBinariesMetric, "Uploaded binaries count");

        private readonly Counter _referencedBinaries =
            Metrics.CreateCounter(Names.ReferencedBinariesMetric, "Referenced binaries count");

        private readonly Counter _removedBinaries =
            Metrics.CreateCounter(Names.RemovedBinariesMetric, "Removed binaries count");

        private readonly Counter _createdSessions =
            Metrics.CreateCounter(Names.CreatedSessionsMetric, "Created sessions count");

        private readonly Counter _removedSessions =
            Metrics.CreateCounter(Names.RemovedSessionsMetric, "Removed sessions count");

        public Histogram GetRequestDurationMsMetric() => _requestDurationMs;

        public Counter GetRequestErrorsMetric() => _requestErrors;

        public Counter GetUploadedBinariesMetric() => _uploadedBinaries;

        public Counter GetReferencedBinariesMetric() => _referencedBinaries;

        public Counter GetRemovedBinariesMetric() => _removedBinaries;

        public Counter GetCreatedSessionsMetric() => _createdSessions;

        public Counter GetRemovedSessionsMetric() => _removedSessions;

        private static class Names
        {
            public const string RequestDurationMetric = "vstore_request_duration";
            public const string RequestErrorsMetric = "vstore_request_errors";
            public const string UploadedBinariesMetric = "vstore_binaries_uploaded";
            public const string ReferencedBinariesMetric = "vstore_binaries_referenced";
            public const string RemovedBinariesMetric = "vstore_binaries_removed";
            public const string CreatedSessionsMetric = "vstore_sessions_created";
            public const string RemovedSessionsMetric = "vstore_sessions_removed";
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
