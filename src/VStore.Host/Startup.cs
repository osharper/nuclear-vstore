using System.Globalization;
using System.Reflection;

using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using NuClear.VStore.Host.Logging;
using NuClear.VStore.Host.Swashbuckle;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions;
using NuClear.VStore.Templates;

using Serilog;
using Serilog.Events;

using Swashbuckle.AspNetCore.Swagger;

namespace NuClear.VStore.Host
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Startup
    {
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
            services.AddMvcCore()
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
                                settings.Converters.Insert(0, new StringEnumConverter { CamelCaseText = true });
                                settings.Converters.Insert(1, new ElementDescriptorJsonConverter());
                                settings.Converters.Insert(2, new ElementDescriptorCollectionJsonConverter());
                                settings.Converters.Insert(3, new TemplateDescriptorJsonConverter());
                                settings.Converters.Insert(4, new ObjectDescriptorJsonConverter());
                            });
            services.AddApiVersioning(options => options.ReportApiVersions = true);

            services.AddSwaggerGen(
                x =>
                    {
                        x.SwaggerDoc("1.0", new Info { Title = "VStore API", Version = "1.0" });
                        x.OperationFilter<UploadFileOperationFilter>();
                    });

            services.AddOptions();

            services.Configure<CephOptions>(_configuration.GetSection("Ceph"));
            services.Configure<LockOptions>(_configuration.GetSection("Ceph:Locks"));
            services.Configure<VStoreOptions>(_configuration.GetSection("VStore"));

            services.AddSingleton<IAmazonS3>(
                x =>
                    {
                        var options = _configuration.GetAWSOptions();

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
                    });
            services.AddSingleton(x => new LockSessionManager(x.GetService<IAmazonS3>(), x.GetService<IOptions<LockOptions>>().Value));
            services.AddScoped(x => new LockSessionFactory(x.GetService<IAmazonS3>(), x.GetService<IOptions<LockOptions>>().Value));
            services.AddScoped(x => new TemplatesStorageReader(x.GetService<IOptions<CephOptions>>().Value, x.GetService<IAmazonS3>()));
            services.AddScoped(
                x => new TemplatesManagementService(
                         x.GetService<IOptions<CephOptions>>().Value,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplatesStorageReader>(),
                         x.GetService<LockSessionFactory>()));
            services.AddScoped(x => new SessionStorageReader(x.GetService<IOptions<CephOptions>>().Value.FilesBucketName, x.GetService<IAmazonS3>()));
            services.AddScoped(
                x => new SessionManagementService(
                         x.GetService<IOptions<VStoreOptions>>().Value.FileStorageEndpoint,
                         x.GetService<IOptions<CephOptions>>().Value.FilesBucketName,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplatesStorageReader>()));
            services.AddScoped(
                x => new ObjectsStorageReader(
                         x.GetService<IOptions<CephOptions>>().Value,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplatesStorageReader>()));
            services.AddScoped(
                x => new ObjectsManagementService(
                         x.GetService<IOptions<CephOptions>>().Value,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplatesStorageReader>(),
                         x.GetService<ObjectsStorageReader>(),
                         x.GetService<SessionStorageReader>(),
                         x.GetService<LockSessionFactory>()));
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

                                    context.Response.ContentType = "application/json";
                                    await context.Response.WriteAsync(new JObject(new JProperty("error", error)).ToString());
                                }
                    });

            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("Location"));
            app.UseMvc();
            app.UseSwagger();
            app.UseSwaggerUI(
                c =>
                    {
                        c.SwaggerEndpoint("/swagger/1.0/swagger.json", "VStore API 1.0");
                        c.DocExpansion("none");
                        c.EnabledValidator();
                        c.ShowRequestHeaders();
                    });
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
