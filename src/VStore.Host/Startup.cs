using System;
using System.Globalization;
using System.Reflection;
using System.Text;

using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;

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
        private static readonly JsonConverter[] CustomConverters =
            {
                new StringEnumConverter { CamelCaseText = true },
                new Int64ToStringJsonConverter(),
                new ElementDescriptorJsonConverter(),
                new ElementDescriptorCollectionJsonConverter(),
                new TemplateDescriptorJsonConverter(),
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
                .Configure<LockOptions>(_configuration.GetSection("Ceph:Locks"))
                .Configure<VStoreOptions>(_configuration.GetSection("VStore"))
                .Configure<JwtOptions>(_configuration.GetSection("Jwt"))
                .Configure<RouteOptions>(options => options.ConstraintMap.Add("lang", typeof(LanguageRouteConstraint)))
                .AddSingleton(x => x.GetRequiredService<IOptions<CephOptions>>().Value)
                .AddSingleton(x => x.GetRequiredService<IOptions<LockOptions>>().Value)
                .AddSingleton(x => x.GetRequiredService<IOptions<JwtOptions>>().Value)
                .AddSingleton(x => x.GetRequiredService<IOptions<VStoreOptions>>().Value);

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
                                for (var index = 0; index < CustomConverters.Length; index++)
                                {
                                    settings.Converters.Insert(index, CustomConverters[index]);
                                }
                            });

            services.AddApiVersioning(options => options.ReportApiVersions = true);

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
            services.AddSingleton<LockSessionManager>();
            services.AddSingleton<LockSessionFactory>();
            services.AddSingleton<TemplatesStorageReader>();
            services.AddSingleton<TemplatesManagementService>();
            services.AddSingleton(
                x => new SessionStorageReader(
                    x.GetService<CephOptions>().FilesBucketName,
                    x.GetService<IAmazonS3>()));
            services.AddSingleton(
                x => new SessionManagementService(
                         x.GetService<VStoreOptions>().FileStorageEndpoint,
                         x.GetService<CephOptions>().FilesBucketName,
                         x.GetService<IAmazonS3>(),
                         x.GetService<TemplatesStorageReader>()));
            services.AddSingleton<ObjectsStorageReader>();
            services.AddSingleton<ObjectsManagementService>();
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
            app.UseApiVersioning();

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
