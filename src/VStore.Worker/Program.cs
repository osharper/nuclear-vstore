using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Amazon;
using Amazon.Runtime;
using Amazon.S3;

using Autofac;
using Autofac.Extensions.DependencyInjection;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NuClear.VStore.Locks;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions;
using NuClear.VStore.Worker.Jobs;

using Serilog;
using NuClear.VStore.Objects;
using NuClear.VStore.Templates;
using NuClear.VStore.Kafka;
using NuClear.VStore.Prometheus;

using Prometheus.Client.MetricServer;

using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;

namespace NuClear.VStore.Worker
{
    public sealed class Program
    {
        private const string Aws = "AWS";
        private const string Ceph = "Ceph";

        private static readonly IMetricServer MetricServer = new MetricServer(5000);

        public static void Main(string[] args)
        {
#if DEBUG
            if (Debugger.IsAttached)
            {
                args = args.Skip(1).ToArray();
            }
#endif
            var env = (Environment.GetEnvironmentVariable("VSTORE_ENVIRONMENT") ?? "Production").ToLower();

            var basePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env}.json")
                .AddEnvironmentVariables("VSTORE_")
                .Build();

            var container = Bootstrap(configuration);

            var loggerFactory = container.Resolve<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Program>();

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
                                          {
                                              logger.LogInformation("Application is shutting down...");
                                              cts.Cancel();
                                              container.Dispose();

                                              eventArgs.Cancel = true;
                                          };
            var app = new CommandLineApplication { Name = "VStore.Worker" };
            app.HelpOption(CommandLine.HelpOptionTemplate);
            app.OnExecute(
                () =>
                    {
                        Console.WriteLine("VStore job runner.");
                        app.ShowHelp();
                        return 0;
                    });

            var jobRunner = container.Resolve<JobRunner>();
            app.Command(
                CommandLine.Commands.Collect,
                config =>
                    {
                        config.Description = "Run cleanup job. See available arguments for details.";
                        config.HelpOption(CommandLine.HelpOptionTemplate);
                        config.Command(
                            CommandLine.Commands.Binaries,
                            commandConfig =>
                                {
                                    commandConfig.Description = "Collect unrefenced and expired binary files.";
                                    commandConfig.HelpOption(CommandLine.HelpOptionTemplate);
                                    commandConfig.Argument(CommandLine.Arguments.BatchSize, "Maximun amount of events to process for one iteration.");
                                    commandConfig.Argument(CommandLine.Arguments.Delay, "Delay between collections.");
                                    commandConfig.OnExecute(() => Run(commandConfig, jobRunner, cts));
                                });
                        config.OnExecute(() =>
                                             {
                                                 config.ShowHelp();
                                                 return 0;
                                             });
                    });
            app.Command(
                CommandLine.Commands.Produce,
                config =>
                    {
                        config.Description = "Run produce events job. See available arguments for details.";
                        config.HelpOption(CommandLine.HelpOptionTemplate);
                        config.Command(
                            CommandLine.Commands.Events,
                            commandConfig =>
                                {
                                    commandConfig.Description = "Produce events of created versions of objects and/or binary files references.";
                                    commandConfig.HelpOption(CommandLine.HelpOptionTemplate);
                                    commandConfig.Argument(CommandLine.Arguments.Mode,
                                                           $"Set '{CommandLine.ArgumentValues.Versions}' to produce events of created versions of objects, " +
                                                           $"and '{CommandLine.ArgumentValues.Binaries}' to produce events of binary files references.");
                                    commandConfig.OnExecute(() => Run(commandConfig, jobRunner, cts));
                                });
                    });

            var exitCode = 0;
            try
            {
                logger.LogInformation("VStore Worker started with options: {workerOptions}.", args.Length != 0 ? string.Join(" ", args) : "N/A");
                MetricServer.Start();
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
                MetricServer.Stop();
                logger.LogInformation("VStore Worker is shutting down with code {workerExitCode}.", exitCode);
            }

            Environment.Exit(exitCode);
        }

        private static IContainer Bootstrap(IConfiguration configuration)
        {
            var services = new ServiceCollection()
                .AddOptions()
                .Configure<CephOptions>(configuration.GetSection("Ceph"))
                .Configure<VStoreOptions>(configuration.GetSection("VStore"))
                .Configure<KafkaOptions>(configuration.GetSection("Kafka"))
                .AddLogging();

            var builder = new ContainerBuilder();
            builder.Populate(services);

            builder.Register(x => x.Resolve<IOptions<CephOptions>>().Value).SingleInstance();
            builder.Register(x => x.Resolve<IOptions<VStoreOptions>>().Value).SingleInstance();
            builder.Register(x => x.Resolve<IOptions<KafkaOptions>>().Value).SingleInstance();

            builder.RegisterType<MemoryCache>().As<IMemoryCache>().SingleInstance();

            builder.RegisterType<EventSender>().SingleInstance();

            builder.RegisterType<JobRegistry>().SingleInstance();
            builder.RegisterType<JobRunner>().SingleInstance();
            builder.RegisterType<BinariesCleanupJob>().SingleInstance();
            builder.RegisterType<ObjectEventsProcessingJob>().SingleInstance();

            builder.Register<IDistributedLockFactory>(
                       x =>
                           {
                               var lockOptions = x.Resolve<DistributedLockOptions>();
                               if (lockOptions.DeveloperMode)
                               {
                                   return new InMemoryLockFactory();
                               }

                               var loggerFactory = x.Resolve<ILoggerFactory>();
                               var logger = loggerFactory.CreateLogger<Program>();

                               var endpoints = lockOptions.EndPoints
                                                          .Aggregate(
                                                              new List<RedLockEndPoint>(),
                                                              (result, next) =>
                                                                  {
                                                                      var ipAddresses = Dns.GetHostAddressesAsync(next.Host).GetAwaiter().GetResult();
                                                                      var ipAddress = ipAddresses[0].ToString();
                                                                      result.Add(new DnsEndPoint(ipAddress, next.Port));

                                                                      logger.LogInformation("{host} ({ipAddress}) will be used as RedLock endpoint.");
                                                                      return result;
                                                                  });
                               return RedLockFactory.Create(endpoints, loggerFactory);
                           })
                   .As<IDistributedLockFactory>()
                   .PreserveExistingDefaults()
                   .SingleInstance();
            builder.Register(
                        x =>
                            {
                                var awsOptions = configuration.GetAWSOptions(Ceph);
                                var config = awsOptions.DefaultClientConfig.ToS3Config();
                                config.ForcePathStyle = true;

                                var cephOptions = x.Resolve<CephOptions>();
                                return new Amazon.S3.AmazonS3Client(new BasicAWSCredentials(cephOptions.AccessKey, cephOptions.SecretKey), config);
                            })
                    .Named<IAmazonS3>(Ceph)
                    .SingleInstance();
            builder.Register(
                        x =>
                            {
                                var options = configuration.GetAWSOptions(Aws);
                                return options.CreateServiceClient<IAmazonS3>();
                            })
                    .Named<IAmazonS3>(Aws)
                    .SingleInstance();
            builder.RegisterType<S3MultipartUploadClient>()
                    .As<IS3MultipartUploadClient>()
                    .WithParameter(
                        (parameterInfo, context) => parameterInfo.ParameterType == typeof(IAmazonS3),
                        (parameterInfo, context) => context.ResolveNamed<IAmazonS3>(Ceph))
                    .SingleInstance();
            builder.Register(
                        x =>
                            {
                                var amazonS3 = x.ResolveNamed<IAmazonS3>(Ceph);
                                var metricsProvider = x.Resolve<MetricsProvider>();
                                return new S3ClientPrometheusDecorator(new S3Client(amazonS3), metricsProvider, Labels.Backends.Ceph);
                            })
                    .Named<IS3Client>(Ceph)
                    .SingleInstance();
            builder.Register(
                        x =>
                            {
                                var amazonS3 = x.ResolveNamed<IAmazonS3>(Aws);
                                var metricsProvider = x.Resolve<MetricsProvider>();
                                return new S3ClientPrometheusDecorator(new S3Client(amazonS3), metricsProvider, Labels.Backends.Aws);
                            })
                    .Named<IS3Client>(Aws)
                    .SingleInstance();
            builder.RegisterType<CephS3Client>()
                    .As<ICephS3Client>()
                    .WithParameter(
                        (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                        (parameterInfo, context) => context.ResolveNamed<IS3Client>(Ceph))
                    .SingleInstance();
            builder.RegisterType<S3.AmazonS3Client>()
                    .As<IAmazonS3Client>()
                    .WithParameter(
                        (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                        (parameterInfo, context) => context.ResolveNamed<IS3Client>(Aws))
                    .SingleInstance();
            builder.RegisterType<LockSessionManager>().SingleInstance();
            builder.RegisterType<SessionCleanupService>().SingleInstance();
            builder.RegisterType<TemplatesStorageReader>()
                   .WithParameter(
                       (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                       (parameterInfo, context) => context.Resolve<ICephS3Client>())
                   .InstancePerDependency();
            builder.RegisterType<ObjectsStorageReader>()
                   .WithParameter(
                       (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                       (parameterInfo, context) => context.Resolve<ICephS3Client>())
                   .InstancePerDependency();
            builder.RegisterType<MetricsProvider>().SingleInstance();

            var container = builder.Build();

            ConfigureLogger(configuration, container.Resolve<ILoggerFactory>());

            return container;
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
            var workerId = app.Parent.Name;
            var jobId = app.Name;
            var args = app.Arguments.Select(x => x.Value).Where(x => !string.IsNullOrEmpty(x)).ToList();
            async Task ExecuteAsync() => await jobRunner.RunAsync(workerId, jobId, args, cts.Token);

            ExecuteAsync().GetAwaiter().GetResult();
            return 0;
        }
    }
}