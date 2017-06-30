using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;

using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NuClear.VStore.Kafka;
using NuClear.VStore.Locks;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions;
using NuClear.VStore.Worker.Jobs;

using Serilog;

namespace NuClear.VStore.Worker
{
    public sealed class Program
    {
        public static void Main(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("VSTORE_ENVIRONMENT") ?? "Production";
            var basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.ToLower()}.json")
                .AddEnvironmentVariables("VSTORE_")
                .Build();

            var serviceProvider = Bootstrap(configuration);

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Program>();

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
                                          {
                                              logger.LogInformation("Application is shutting down...");
                                              cts.Cancel();
                                              eventArgs.Cancel = true;
                                          };
            var app = new CommandLineApplication { Name = "VStore.Worker" };
            app.HelpOption("-h|--help");
            app.OnExecute(
                () =>
                    {
                        Console.WriteLine("VStore job runner.");
                        app.ShowHelp();
                        return 0;
                    });

            var jobRunner = serviceProvider.GetRequiredService<JobRunner>();
            app.Command(
                "collect",
                config =>
                    {
                        config.Description = "Run cleanup job. See available arguments for details.";
                        config.HelpOption("-h|--help");
                        config.Command(
                            "locks",
                            nestedConfig =>
                                {
                                    nestedConfig.Description = "Collect expired locks.";
                                    nestedConfig.HelpOption("-h|--help");
                                    nestedConfig.OnExecute(() => Run(nestedConfig, jobRunner, cts));
                                });
                        config.Command(
                            "binaries",
                            nestedConfig =>
                                {
                                    nestedConfig.Description = "Collect orphan binary files.";
                                    nestedConfig.HelpOption("-h|--help");
                                    nestedConfig.OnExecute(() => Run(nestedConfig, jobRunner, cts));
                                });
                        config.OnExecute(() =>
                                             {
                                                 config.ShowHelp();
                                                 return 0;
                                             });
                    });
            app.Command(
                "produce",
                config =>
                    {
                        config.Description = "Run produce job. See available arguments for details.";
                        config.HelpOption("-h|--help");
                        config.Command(
                            "events",
                            nestedConfig =>
                                {
                                    nestedConfig.Description = "Produce events of object versions creating and binary files usings.";
                                    nestedConfig.HelpOption("-h|--help");
                                    nestedConfig.OnExecute(() => Run(nestedConfig, jobRunner, cts));
                                });
                    });

            var exitCode = 0;
            try
            {
                logger.LogInformation("VStore Worker started with options: {workerOptions}.", args.Length != 0 ? string.Join(" ", args) : "N/A");
                exitCode = app.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                ex.Command.ShowHelp();
                exitCode = 1;
            }
            catch (JobNotFoundException)
            {
                exitCode = 2;
            }
            catch (Exception ex)
            {
                logger.LogCritical(new EventId(), ex, "Unexpected error occured. See logs for details.");
                exitCode = -1;
            }
            finally
            {
                logger.LogInformation("VStore Worker is shutting down with code {workerExitCode}.", exitCode);
            }

            Environment.Exit(exitCode);
        }

        private static IServiceProvider Bootstrap(IConfiguration configuration)
        {
            var services = new ServiceCollection()
                .AddOptions()
                .Configure<CephOptions>(configuration.GetSection("Ceph"))
                .Configure<LockOptions>(configuration.GetSection("Ceph:Locks"))
                .Configure<VStoreOptions>(configuration.GetSection("VStore"))
                .Configure<KafkaOptions>(configuration.GetSection("Kafka"))
                .AddSingleton(x => x.GetRequiredService<IOptions<CephOptions>>().Value)
                .AddSingleton(x => x.GetRequiredService<IOptions<LockOptions>>().Value)
                .AddSingleton(x => x.GetRequiredService<IOptions<VStoreOptions>>().Value)
                .AddSingleton(x => x.GetRequiredService<IOptions<KafkaOptions>>().Value)

                .AddLogging()

                .AddSingleton<JobRegistry>()
                .AddScoped<JobRunner>()
                .AddScoped<LockCleanupJob>()
                .AddScoped<BinariesCleanupJob>()
                .AddScoped<ObjectEventsProcessingJob>()

                .AddSingleton<IAmazonS3>(
                    x =>
                        {
                            var options = configuration.GetAWSOptions();

                            AWSCredentials credentials;
                            if (options.Credentials != null)
                            {
                                credentials = options.Credentials;
                            }
                            else
                            {
                                var storeChain = new CredentialProfileStoreChain(options.ProfilesLocation);
                                if (string.IsNullOrEmpty(options.Profile) || !storeChain.TryGetAWSCredentials(options.Profile, out credentials))
                                {
                                    credentials = FallbackCredentialsFactory.GetCredentials();
                                }
                            }

                            var config = options.DefaultClientConfig.ToS3Config();
                            config.ForcePathStyle = true;

                            return new AmazonS3Client(credentials, config);
                        })
                .AddSingleton<LockSessionManager>()
                .AddSingleton<SessionCleanupService>();

            var serviceProvider = services.BuildServiceProvider();

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            ConfigureLogger(configuration, loggerFactory);

            return serviceProvider;
        }

        private static void ConfigureLogger(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            var loggerConfiguration = new LoggerConfiguration().ReadFrom.Configuration(configuration);
            Log.Logger = loggerConfiguration.CreateLogger();
            loggerFactory.AddSerilog();

            AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Log4Net;
            AWSConfigs.LoggingConfig.LogMetricsFormat = LogMetricsFormatOption.Standard;
        }

        private static int Run(CommandLineApplication app, JobRunner jobRunner, CancellationTokenSource cts)
        {
            async Task ExecuteAsync(JobRunner runner, string jobId, CancellationToken cancellationToken) => await runner.RunAsync(jobId, cancellationToken);

            ExecuteAsync(jobRunner, app.Name, cts.Token).GetAwaiter().GetResult();
            return 0;
        }
    }
}