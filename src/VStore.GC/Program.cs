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

using NuClear.VStore.GC.Jobs;
using NuClear.VStore.Locks;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

using Serilog;

namespace NuClear.VStore.GC
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

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
                                          {
                                              cts.Cancel();
                                              eventArgs.Cancel = false;
                                          };

            var serviceProvider = Bootstrap(configuration);

            var app = new CommandLineApplication { Name = "VStore.GC" };
            app.HelpOption("-h|--help");
            app.OnExecute(
                () =>
                    {
                        Console.WriteLine("VStore.GC job runner.");
                        app.ShowHelp();
                        return 0;
                    });
            app.Command(
                "collect",
                config =>
                    {
                        config.Description = "Run cleanup job. See available arguments for details.";
                        config.HelpOption("-h|--help");

                        var locksArgument = config.Argument("locks", "Collect frozen locks.");
                        var binariesArgument = config.Argument("binaries", "Collect orphan binary files.");
                        var jobRunner = serviceProvider.GetRequiredService<JobRunner>();
                        config.OnExecute(
                            () =>
                                {
                                    string jobId = null;
                                    if (!string.IsNullOrEmpty(locksArgument?.Value))
                                    {
                                        jobId = locksArgument.Value;
                                    }
                                    else if (!string.IsNullOrEmpty(binariesArgument?.Value))
                                    {
                                        jobId = binariesArgument.Value;
                                    }
                                    else
                                    {
                                        config.ShowHelp();
                                    }

                                    ExecuteAsync(jobRunner, jobId, cts.Token).GetAwaiter().GetResult();

                                    return 0;
                                });
                    });

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Program>();
            var exitCode = 0;
            try
            {
                logger.LogInformation("VStore GC started with options: {gcOptions}.", args.Length != 0 ? string.Join(" ", args) : "N/A");
                exitCode = app.Execute(args);
            }
            catch (JobNotFoundException)
            {
                exitCode = 1;
            }
            catch (Exception ex)
            {
                logger.LogCritical(new EventId(), ex, "Unexpected error occured. See logs for details.");
                exitCode = -1;
            }
            finally
            {
                logger.LogInformation("VStore GC is shutting down with code {gcExitCode}.", exitCode);
            }

            Environment.Exit(exitCode);
        }

        private static IServiceProvider Bootstrap(IConfiguration configuration)
        {
            var services = new ServiceCollection()
                .AddOptions()
                .Configure<CephOptions>(configuration.GetSection("Ceph"))
                .Configure<LockOptions>(configuration.GetSection("Ceph:Locks"))
                .AddSingleton(x => x.GetRequiredService<IOptions<CephOptions>>().Value)
                .AddSingleton(x => x.GetRequiredService<IOptions<LockOptions>>().Value)

                .AddLogging()

                .AddSingleton<JobRegistry>()
                .AddScoped<JobRunner>()
                .AddScoped<LockCleanupJob>()

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
                .AddScoped<LockSessionManager>();

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

        private static async Task ExecuteAsync(JobRunner jobRunner, string jobId, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(jobId))
            {
                await jobRunner.RunAsync(jobId, cancellationToken);
            }
        }
    }
}