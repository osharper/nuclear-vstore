namespace NuClear.VStore.Worker
{
    public struct CommandLine
    {
        public const char ArgumentKeySeparator = '=';
        public const char ArgumentValueSeparator = ',';
        public const string HelpOptionTemplate = "-h|--help";

        public struct Commands
        {
            public const string Collect = "collect";
            public const string Binaries = "binaries";
            public const string Produce = "produce";
            public const string Events = "events";
        }

        public struct Arguments
        {
            public const string Range = "range";
            public const string Mode = "mode";
            public const string Delay = "delay";
        }

        public struct ArgumentValues
        {
            public const string Versions = "versions";
            public const string Binaries = "binaries";
        }
    }
}