using System.Reflection;

using Amazon;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

using Autofac.Extensions.DependencyInjection;

using NuClear.VStore.Host.Logging;

using Serilog;
using Serilog.Events;

namespace NuClear.VStore.Host
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var webHost = BuildWebHost(args);
            ConfigureAwsLogging();
            webHost.Run();
        }

        private static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                   .ConfigureServices(services => services.AddAutofac())
                   .UseStartup<Startup>()
                   .UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration))
                   .Build();

        private static void ConfigureAwsLogging()
        {
            var log4NetLevel = Log.IsEnabled(LogEventLevel.Verbose) ? "ALL"
                               : Log.IsEnabled(LogEventLevel.Debug) ? "DEBUG"
                               : Log.IsEnabled(LogEventLevel.Information) ? "INFO"
                               : Log.IsEnabled(LogEventLevel.Warning) ? "WARN"
                               : Log.IsEnabled(LogEventLevel.Error) ? "ERROR"
                               : Log.IsEnabled(LogEventLevel.Fatal) ? "FATAL" : "OFF";

            var serilogAppender = new SerilogAppender(Log.Logger);
            serilogAppender.ActivateOptions();
            var log = log4net.LogManager.GetLogger(Assembly.GetEntryAssembly(), "Amazon");
            var wrapper = (log4net.Repository.Hierarchy.Logger)log.Logger;
            wrapper.Level = wrapper.Hierarchy.LevelMap[log4NetLevel];
            wrapper.AddAppender(serilogAppender);
            wrapper.Repository.Configured = true;

            AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Log4Net;
            AWSConfigs.LoggingConfig.LogMetricsFormat = LogMetricsFormatOption.Standard;
        }
    }
}
