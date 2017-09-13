using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

using Amazon;
using Amazon.Runtime;
using Amazon.S3;

using Autofac;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using NuClear.VStore.Host.Json;
using NuClear.VStore.Host.Logging;
using NuClear.VStore.Host.Middleware;
using NuClear.VStore.Host.Options;
using NuClear.VStore.Host.Routing;
using NuClear.VStore.Host.Swashbuckle;
using NuClear.VStore.Http;
using NuClear.VStore.Json;
using NuClear.VStore.Kafka;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;
using NuClear.VStore.Prometheus;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions;
using NuClear.VStore.Templates;

using Prometheus.Client.Collectors;
using Prometheus.Client.Owin;

using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;

using Serilog;
using Serilog.Events;

using Swashbuckle.AspNetCore.Swagger;

namespace NuClear.VStore.Host
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Startup
    {
        private const string Aws = "AWS";
        private const string Ceph = "Ceph";

        private static readonly JsonConverter[] CustomConverters =
            {
                new StringEnumConverter { CamelCaseText = true },
                new Int64ToStringJsonConverter(),
                new ElementDescriptorJsonConverter(),
                new ElementDescriptorCollectionJsonConverter(),
                new TemplateDescriptorJsonConverter(),
                new ObjectElementDescriptorJsonConverter(),
                new ObjectDescriptorJsonConverter()
            };

        private readonly IConfigurationRoot _configuration;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName?.ToLower()}.json")
                .AddEnvironmentVariables("VSTORE_");

            _configuration = builder.Build();

            ConfigureLogger();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // ReSharper disable once UnusedMember.Global
        public void ConfigureServices(IServiceCollection services)
        {
            services
                 .AddOptions()
                 .Configure<CephOptions>(_configuration.GetSection("Ceph"))
                 .Configure<DistributedLockOptions>(_configuration.GetSection("DistributedLocks"))
                 .Configure<VStoreOptions>(_configuration.GetSection("VStore"))
                 .Configure<JwtOptions>(_configuration.GetSection("Jwt"))
                 .Configure<KafkaOptions>(_configuration.GetSection("Kafka"))
                 .Configure<RouteOptions>(options => options.ConstraintMap.Add("lang", typeof(LanguageRouteConstraint)));

            services.AddMvcCore(
                        options =>
                            {
                                var policy = new AuthorizationPolicyBuilder()
                                    .RequireAuthenticatedUser()
                                    .Build();
                                options.Filters.Add(new AuthorizeFilter(policy));
                            })
                    .AddVersionedApiExplorer()
                    .AddApiExplorer()
                    .AddAuthorization()
                    .AddCors()
                    .AddJsonFormatters()
                    .AddJsonOptions(
                        options =>
                            {
                                var settings = options.SerializerSettings;

                                settings.Culture = CultureInfo.InvariantCulture;
                                settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                                for (var index = 0; index < CustomConverters.Length; ++index)
                                {
                                    settings.Converters.Insert(index, CustomConverters[index]);
                                }
                            });

            services.AddApiVersioning(options => options.ReportApiVersions = true);
            services.AddMemoryCache();

            services.AddSwaggerGen(
                options =>
                    {
                        var provider = services.BuildServiceProvider().GetRequiredService<IApiVersionDescriptionProvider>();
                        foreach (var description in provider.ApiVersionDescriptions)
                        {
                            options.SwaggerDoc(description.GroupName, new Info { Title = $"VStore API {description.ApiVersion}", Version = description.ApiVersion.ToString() });
                        }

                        options.AddSecurityDefinition(
                            "Bearer",
                            new ApiKeyScheme
                                {
                                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                                    Name = "Authorization",
                                    In = "header",
                                    Type = "apiKey"
                                });

                        options.OperationFilter<ImplicitApiVersionParameter>();
                        options.OperationFilter<UploadFileOperationFilter>();
                    });
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.Register(x => x.Resolve<IOptions<CephOptions>>().Value).SingleInstance();
            builder.Register(x => x.Resolve<IOptions<DistributedLockOptions>>().Value).SingleInstance();
            builder.Register(x => x.Resolve<IOptions<VStoreOptions>>().Value).SingleInstance();
            builder.Register(x => x.Resolve<IOptions<JwtOptions>>().Value).SingleInstance();
            builder.Register(x => x.Resolve<IOptions<KafkaOptions>>().Value).SingleInstance();

            builder.Register<IDistributedLockFactory>(
                       x =>
                           {
                               var lockOptions = x.Resolve<DistributedLockOptions>();
                               if (lockOptions.DeveloperMode)
                               {
                                   return new InMemoryLockFactory();
                               }

                               var loggerFactory = x.Resolve<ILoggerFactory>();
                               var logger = loggerFactory.CreateLogger<Startup>();

                               var endpoints = lockOptions.GetEndPoints();
                               var redLockEndPoints = new List<RedLockEndPoint>();
                               foreach (var endpoint in endpoints)
                               {
                                   redLockEndPoints.Add(new DnsEndPoint(endpoint.IpAddress, endpoint.Port));
                                   logger.LogInformation(
                                       "{host}:{port} ({ipAddress}:{port}) will be used as RedLock endpoint.",
                                       endpoint.Host,
                                       endpoint.Port,
                                       endpoint.IpAddress,
                                       endpoint.Port);
                               }

                               return RedLockFactory.Create(redLockEndPoints, loggerFactory);
                           })
                   .As<IDistributedLockFactory>()
                   .PreserveExistingDefaults()
                   .SingleInstance();
            builder.Register(
                        x =>
                            {
                                var awsOptions = _configuration.GetAWSOptions(Ceph);
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
                                var options = _configuration.GetAWSOptions(Aws);
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
                    .PreserveExistingDefaults()
                    .SingleInstance();
            builder.Register(
                        x =>
                            {
                                var amazonS3 = x.ResolveNamed<IAmazonS3>(Aws);
                                var metricsProvider = x.Resolve<MetricsProvider>();
                                return new S3ClientPrometheusDecorator(new S3Client(amazonS3), metricsProvider, Labels.Backends.Aws);
                            })
                    .Named<IS3Client>(Aws)
                    .PreserveExistingDefaults()
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
            builder.RegisterType<SessionStorageReader>().SingleInstance();
            builder.RegisterType<SessionManagementService>().SingleInstance();
            builder.RegisterType<TemplatesStorageReader>()
                   .WithParameter(
                       (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                       (parameterInfo, context) => context.Resolve<ICephS3Client>())
                   .SingleInstance();
            builder.RegisterType<TemplatesManagementService>()
                   .WithParameter(
                       (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                       (parameterInfo, context) => context.Resolve<ICephS3Client>())
                   .SingleInstance();
            builder.RegisterType<ObjectsStorageReader>()
                   .WithParameter(
                       (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                       (parameterInfo, context) => context.Resolve<ICephS3Client>())
                   .SingleInstance();
            builder.RegisterType<ObjectsManagementService>()
                   .WithParameter(
                       (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                       (parameterInfo, context) => context.Resolve<ICephS3Client>())
                   .SingleInstance();
            builder.RegisterType<EventSender>().SingleInstance();
            builder.RegisterType<MetricsProvider>().SingleInstance();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime)
        {
            loggerFactory.AddSerilog();

            // Ensure any buffered events are sent at shutdown
            appLifetime.ApplicationStopped.Register(Log.CloseAndFlush);

            app.UseExceptionHandler(
                new ExceptionHandlerOptions
                    {
                        ExceptionHandler =
                            async context =>
                                {
                                    var feature = context.Features.Get<IExceptionHandlerFeature>();
                                    var error = new JObject
                                                    {
                                                        { "requestId", context.TraceIdentifier },
                                                        { "code", "unhandledException" },
                                                        { "message", feature.Error.Message }
                                                    };

                                    if (env.IsDevelopment())
                                    {
                                        error.Add("details", feature.Error.ToString());
                                    }

                                    context.Response.ContentType = ContentType.Json;
                                    await context.Response.WriteAsync(new JObject(new JProperty("error", error)).ToString());
                                }
                    });
            app.UseMiddleware<HealthCheckMiddleware>();
            app.UsePrometheusServer(
                new PrometheusOptions
                    {
                        Collectors = new List<IOnDemandCollector> { new DotNetStatsCollector(), new WindowsDotNetStatsCollector() }
                    });
            app.UseMiddleware<CrosscuttingTraceIdentifierMiddleware>();
            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("Location"));

            var jwtOptions = app.ApplicationServices.GetRequiredService<JwtOptions>();
            app.UseJwtBearerAuthentication(
                new JwtBearerOptions
                    {
                        AutomaticAuthenticate = true,
                        AutomaticChallenge = true,
                        TokenValidationParameters =
                            new TokenValidationParameters
                                {
                                    ValidateIssuer = true,
                                    ValidIssuer = jwtOptions.Issuer,

                                    ValidateAudience = false,

                                    ValidateIssuerSigningKey = true,
                                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtOptions.SecretKey)),

                                    ValidateLifetime = false,
                                    LifetimeValidator = (notBefore, expires, securityToken, validationParameters) =>
                                                            {
                                                                var utcNow = DateTime.UtcNow;
                                                                return !(notBefore > utcNow || utcNow > expires);
                                                            }
                                }
                    });

            app.UseMvc();

            if (!env.IsProduction())
            {
                app.UseSwagger();
                app.UseSwaggerUI(
                    options =>
                        {
                            var provider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();
                            foreach (var description in provider.ApiVersionDescriptions)
                            {
                                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                            }

                            options.DocExpansion("list");
                            options.EnabledValidator();
                            options.ShowRequestHeaders();
                        });
            }
        }

        private static void AttachToLog4Net(Serilog.ILogger logger, string loggerName, string level)
        {
            var serilogAppender = new SerilogAppender(logger);
            serilogAppender.ActivateOptions();
            var log = log4net.LogManager.GetLogger(Assembly.GetEntryAssembly(), loggerName);
            var wrapper = (log4net.Repository.Hierarchy.Logger)log.Logger;
            wrapper.Level = wrapper.Hierarchy.LevelMap[level];
            wrapper.AddAppender(serilogAppender);
            wrapper.Repository.Configured = true;
        }

        private void ConfigureLogger()
        {
            var loggerConfiguration = new LoggerConfiguration().ReadFrom.Configuration(_configuration);
            Log.Logger = loggerConfiguration.CreateLogger();

            var log4NetLevel = Log.IsEnabled(LogEventLevel.Verbose) ? "ALL"
                                   : Log.IsEnabled(LogEventLevel.Debug) ? "DEBUG"
                                       : Log.IsEnabled(LogEventLevel.Information) ? "INFO"
                                           : Log.IsEnabled(LogEventLevel.Warning) ? "WARN"
                                               : Log.IsEnabled(LogEventLevel.Error) ? "ERROR"
                                                   : Log.IsEnabled(LogEventLevel.Fatal) ? "FATAL" : "OFF";

            AttachToLog4Net(Log.Logger, "Amazon", log4NetLevel);

            AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Log4Net;
            AWSConfigs.LoggingConfig.LogMetricsFormat = LogMetricsFormatOption.Standard;
        }
    }
}
