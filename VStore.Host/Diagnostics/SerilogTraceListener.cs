// Copyright 2015 Serilog Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics;
using System.Globalization;

using Serilog;
using Serilog.Events;

namespace NuClear.VStore.Host.Diagnostics
{
    /// <summary>
    ///     TraceListener implementation that directs all output to Serilog.
    /// </summary>
    public class SerilogTraceListener : TraceListener
    {
        private const LogEventLevel FailLevel = LogEventLevel.Fatal;
        private const LogEventLevel DefaultLogLevel = LogEventLevel.Debug;
        private const string MessagelessTraceEventMessageTemplate = "{TraceSource:l} {TraceEventType}: {TraceEventId}";
        private const string MesageMessageTemplate = "{TraceMessage:l}";
        private const string MessageWithCategoryMessageTemplate = "{Category:l}: {TraceMessage:l}";
        private const string FailMessageTemplate = "Fail: {TraceMessage:l}";
        private const string DetailedFailMessageTemplate = "Fail: {TraceMessage:l} {FailDetails:l}";
        private static readonly string TraceDataMessageTemplate = "{TraceSource:l} {TraceEventType}: {TraceEventId} :" + Environment.NewLine + "{TraceData:l}";
        private static readonly string TraceEventMessageTemplate = "{TraceSource:l} {TraceEventType}: {TraceEventId} :" + Environment.NewLine + "{TraceMessage:l}";
        private readonly ILogger _logger;

        /// <summary>
        ///     Creates a SerilogTraceListener that uses the logger from `Serilog.Log`
        /// </summary>
        /// <remarks>
        ///     This is needed because TraceListeners are often configured through XML
        ///     where there would be no opportunity for constructor injection
        /// </remarks>
        public SerilogTraceListener() : this(Log.Logger)
        {
        }

        /// <summary>
        ///     Creates a SerilogTraceListener that uses the specified logger
        /// </summary>
        public SerilogTraceListener(ILogger logger)
        {
            this._logger = logger.ForContext<SerilogTraceListener>();
        }

        /// <summary>
        ///     Creates a SerilogTraceListener for the context specified.
        ///     <listeners>
        ///         <add name="Serilog" type="SerilogTraceListener.SerilogTraceListener, SerilogTraceListener" initializeData="MyContext" />
        ///     </listeners>
        /// </summary>
        public SerilogTraceListener(string context)
        {
            this._logger = Log.Logger.ForContext("SourceContext", context);
        }

        public override bool IsThreadSafe => true;

        public override void Write(string message)
        {
            _logger.Write(DefaultLogLevel, MesageMessageTemplate, message);
        }

        public override void Write(string message, string category)
        {
            _logger.Write(DefaultLogLevel, MessageWithCategoryMessageTemplate, category, message);
        }

        public override void WriteLine(string message)
        {
            Write(message);
        }

        public override void WriteLine(string message, string category)
        {
            Write(message, category);
        }

        public override void Fail(string message)
        {
            _logger.Write(FailLevel, FailMessageTemplate, message);
        }

        public override void Fail(string message, string detailMessage)
        {
            _logger.Write(FailLevel, DetailedFailMessageTemplate, message, detailMessage);
        }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data)
        {
            WriteEvent(eventCache, eventType, TraceDataMessageTemplate, source, eventType, id, data);
        }

        public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, params object[] data)
        {
            WriteEvent(eventCache, eventType, TraceDataMessageTemplate, source, eventType, id, data);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id)
        {
            WriteEvent(eventCache, eventType, MessagelessTraceEventMessageTemplate, source, eventType, id);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            WriteEvent(eventCache, eventType, TraceEventMessageTemplate, source, eventType, id, message);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            TraceEvent(eventCache, source, eventType, id, string.Format(CultureInfo.InvariantCulture, format, args ?? new object[0]));
        }

        internal static LogEventLevel ToLogEventLevel(TraceEventType eventType)
        {
            switch (eventType)
            {
                case TraceEventType.Critical:
                    return LogEventLevel.Fatal;
                case TraceEventType.Error:
                    return LogEventLevel.Error;
                case TraceEventType.Information:
                    return LogEventLevel.Information;
                case TraceEventType.Warning:
                    return LogEventLevel.Warning;
                case TraceEventType.Verbose:
                    return LogEventLevel.Verbose;
                default:
                    return LogEventLevel.Debug;
            }
        }

        private void WriteEvent(TraceEventCache eventCache, TraceEventType eventType, string messageTemplate, params object[] propertyValues)
        {
            var level = ToLogEventLevel(eventType);
            _logger.Write(level, messageTemplate, propertyValues);
        }
    }
}