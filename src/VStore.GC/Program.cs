using System;
using System.IO;
using System.Threading.Tasks;

using Amazon;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NuClear.VStore.Options;

using Serilog;

namespace NuClear.VStore.GC
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("VSTORE_GC_ENVIRONMENT") ?? "Production";
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.ToLower()}.json")
                .AddEnvironmentVariables("VSTORE_GC_")
                .Build();

            var services = new ServiceCollection();

            services.AddLogging();

            services.AddOptions();
            services.Configure<CephOptions>(configuration.GetSection("Ceph"));
            services.Configure<LockOptions>(configuration.GetSection("Ceph:Locks"));

            var serviceProvider = services.BuildServiceProvider();

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            ConfigureLogger(configuration, loggerFactory);

            var logger = loggerFactory.CreateLogger<Program>();
            MainAsync(logger).GetAwaiter().GetResult();
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static async Task MainAsync(ILogger<Program> logger)
        {
            logger.LogInformation("GC started.");
            await Task.Delay(TimeSpan.FromSeconds(5));
            logger.LogInformation("GC stopped.");
        }

        private static void ConfigureLogger(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            var loggerConfiguration = new LoggerConfiguration().ReadFrom.Configuration(configuration);
            Log.Logger = loggerConfiguration.CreateLogger();
            loggerFactory.AddSerilog();

            AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Log4Net;
            AWSConfigs.LoggingConfig.LogMetricsFormat = LogMetricsFormatOption.Standard;
        }
    }
}
