using System;
using System.Collections.Generic;

using log4net.Appender;
using log4net.Core;

using Serilog.Events;

using ILogger = Serilog.ILogger;

namespace NuClear.VStore.Host.Logging
{
    public class SerilogAppender : AppenderSkeleton
    {
        private static readonly IReadOnlyDictionary<Level, LogEventLevel> LevelMap =
            new Dictionary<Level, LogEventLevel>
                {
                    { Level.Verbose, LogEventLevel.Verbose },
                    { Level.Debug, LogEventLevel.Debug },
                    { Level.Info, LogEventLevel.Information },
                    { Level.Warn, LogEventLevel.Warning },
                    { Level.Error, LogEventLevel.Error },
                    { Level.Fatal, LogEventLevel.Fatal }
                };

        private readonly ILogger _logger;

        public SerilogAppender(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            var serilogLevel = ConvertLevel(loggingEvent.Level);
            var logger = _logger.ForContext(Serilog.Core.Constants.SourceContextPropertyName, loggingEvent.LoggerName);
            logger.Write(serilogLevel, loggingEvent.ExceptionObject, loggingEvent.MessageObject?.ToString());
        }

        private static LogEventLevel ConvertLevel(Level log4NetLevel)
        {
            if (LevelMap.TryGetValue(log4NetLevel, out LogEventLevel serilogLevel))
            {
                return serilogLevel;
            }

            Serilog.Debugging.SelfLog.WriteLine("Unexpected log4net logging level ({0}) logging as Information", log4NetLevel.DisplayName);
            return LogEventLevel.Information;
        }
    }
}
