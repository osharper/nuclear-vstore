using System.Globalization;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

using Amazon;
using Amazon.S3;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using NuClear.VStore.Host.Convensions;
using NuClear.VStore.Host.Logging;
using NuClear.VStore.Host.Swashbuckle;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;
using NuClear.VStore.Sessions;
using NuClear.VStore.Templates;

using Serilog;
using Serilog.Events;

namespace NuClear.VStore.Host
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Startup
    {
        private const string ApiVersion = "/api/1.0";

        private readonly IConfigurationRoot _configuration;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables("VSTORE_");

            _configuration = builder.Build();

            ConfigureLogger();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // ReSharper disable once UnusedMember.Global
        public void ConfigureServices(IServiceCollection services)
        {
            AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Log4Net;
            AWSConfigs.LoggingConfig.LogMetricsFormat = LogMetricsFormatOption.Standard;

            services.AddMvc(options => options.UseGlobalRoutePrefix(new RouteAttribute(ApiVersion)))
                    .AddJsonOptions(
                        options =>
                            {
                                var settings = options.SerializerSettings;

                                settings.Culture = CultureInfo.InvariantCulture;
                                settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                                settings.Converters.Insert(0, new StringEnumConverter { CamelCaseText = true });
                                settings.Converters.Insert(1, new TemplateDescriptorJsonConverter());
                            });

            services.AddSwaggerGen(x => x.OperationFilter<UploadFileOperationFilter>());

            services.AddOptions();
            services.AddDefaultAWSOptions(_configuration.GetAWSOptions());
            services.Configure<CephOptions>(_configuration.GetSection("Ceph"));
            services.Configure<LockOptions>(_configuration.GetSection("Ceph:Locks"));
            services.Configure<VStoreOptions>(_configuration.GetSection("VStore"));

            services.AddAWSService<IAmazonS3>();
            services.AddSingleton(x => new LockSessionManager(x.GetService<IAmazonS3>(), x.GetService<IOptions<LockOptions>>().Value));
            services.AddScoped(x => new LockSessionFactory(x.GetService<IAmazonS3>(), x.GetService<IOptions<LockOptions>>().Value));
            services.AddScoped(x => new TemplateStorageReader(x.GetService<IOptions<CephOptions>>().Value, x.GetService<IAmazonS3>()));
            services.AddScoped(
                x => new TemplateManagementService(
                         x.GetService<IOptions<CephOptions>>().Value,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplateStorageReader>(),
                         x.GetService<LockSessionFactory>()));
            services.AddScoped(
                x => new ObjectStorageReader(
                         x.GetService<IOptions<CephOptions>>().Value,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplateStorageReader>()));
            services.AddScoped(
                x => new ObjectManagementService(
                         x.GetService<IOptions<CephOptions>>().Value,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplateStorageReader>(),
                         x.GetService<ObjectStorageReader>(),
                         x.GetService<LockSessionFactory>()));
            services.AddScoped(
                x => new SessionManagementService(
                         x.GetService<IOptions<VStoreOptions>>().Value.FileStorageEndpoint,
                         x.GetService<IOptions<CephOptions>>().Value.FilesBucketName,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplateStorageReader>()));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime)
        {
            loggerFactory.AddSerilog();

#if DEBUG
            loggerFactory.AddDebug(LogLevel.Debug);
#endif

            // Ensure any buffered events are sent at shutdown
            appLifetime.ApplicationStopped.Register(Log.CloseAndFlush);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(options =>
                    {
                        options.Run(
                            async context =>
                                {
                                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    context.Response.ContentType = "application/json";
                                    await context.Response.WriteAsync("error").ConfigureAwait(false);
                                });
                    });
            }

            app.UseStatusCodePages(x => Task.Run(() => RedirectToCurrentApiVersion(x.HttpContext)));
            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            app.UseMvc();
            app.UseSwagger();
            app.UseSwaggerUi();
        }

        private static void ConfigureSerilogAppender(string loggerName, string level)
        {
            var serilogAppender = new SerilogAppender(Log.Logger);
            serilogAppender.ActivateOptions();
            var log = log4net.LogManager.GetLogger(Assembly.GetEntryAssembly(), loggerName);
            var wrapper = (log4net.Repository.Hierarchy.Logger)log.Logger;
            wrapper.Level = wrapper.Hierarchy.LevelMap[level];
            wrapper.AddAppender(serilogAppender);
            wrapper.Repository.Configured = true;
        }

        private static void RedirectToCurrentApiVersion(HttpContext httpContext)
        {
            if (httpContext.Response.StatusCode == (int)HttpStatusCode.NotFound && !httpContext.Request.Path.StartsWithSegments(ApiVersion))
            {
                httpContext.Response.Redirect($"{ApiVersion}{httpContext.Request.Path}");
            }
        }

        private void ConfigureLogger()
        {
            var loggerConfiguration = new LoggerConfiguration()
                .ReadFrom.Configuration(_configuration);

            Log.Logger = loggerConfiguration
                .CreateLogger();

            var serilogLevel = Log.IsEnabled(LogEventLevel.Verbose) ? "ALL"
                                   : Log.IsEnabled(LogEventLevel.Debug) ? "DEBUG"
                                       : Log.IsEnabled(LogEventLevel.Information) ? "INFO"
                                           : Log.IsEnabled(LogEventLevel.Warning) ? "WARN"
                                               : Log.IsEnabled(LogEventLevel.Error) ? "ERROR"
                                                   : Log.IsEnabled(LogEventLevel.Fatal) ? "FATAL" : "OFF";

            ConfigureSerilogAppender("Amazon", serilogLevel);
        }
    }
}
