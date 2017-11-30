using System;
using System.Collections.Generic;

using Amazon.Runtime;
using Amazon.S3;

using Autofac;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Http;
using NuClear.VStore.Http.Core.Middleware;
using NuClear.VStore.Http.Core.Routing;
using NuClear.VStore.Http.Core.Swashbuckle;
using NuClear.VStore.ImageRendering;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;
using NuClear.VStore.Prometheus;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

using Prometheus.Client.Collectors;
using Prometheus.Client.Owin;

using RedLockNet;

using Swashbuckle.AspNetCore.Swagger;

namespace NuClear.VStore.Renderer
{
    public sealed class Startup
    {
        private const string Aws = "AWS";
        private const string Ceph = "Ceph";

        private readonly IConfiguration _configuration;

        public Startup(IHostingEnvironment env)
        {
            _configuration = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName?.ToLower()}.json")
                .AddEnvironmentVariables("VSTORE_")
                .Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddOptions()
                .Configure<CephOptions>(_configuration.GetSection("Ceph"))
                .Configure<RouteOptions>(options => options.ConstraintMap.Add("lang", typeof(LanguageRouteConstraint)));

            services.AddMvcCore()
                    .AddVersionedApiExplorer()
                    .AddApiExplorer()
                    .AddCors();

            services.AddApiVersioning(
                options =>
                    {
                        options.ReportApiVersions = true;
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                    });
            services.AddMemoryCache();
            services.AddSwaggerGen(
                options =>
                    {
                        var provider = services.BuildServiceProvider().GetRequiredService<IApiVersionDescriptionProvider>();
                        foreach (var description in provider.ApiVersionDescriptions)
                        {
                            options.SwaggerDoc(description.GroupName, new Info { Title = $"VStore Renderer API {description.ApiVersion}", Version = description.ApiVersion.ToString() });
                        }

                        options.OperationFilter<ImplicitApiVersionParameter>();
                        options.OperationFilter<UploadFileOperationFilter>();
                        options.OperationFilter<ViewFileFilter>();
                    });
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.Register(x => x.Resolve<IOptions<CephOptions>>().Value).SingleInstance();
            builder.RegisterInstance(new VStoreOptions { FileStorageEndpoint = new Uri("ams://unused") }).SingleInstance();
            builder.RegisterInstance(new DistributedLockOptions()).SingleInstance();

            builder.RegisterType<InMemoryLockFactory>().As<IDistributedLockFactory>().PreserveExistingDefaults().SingleInstance();
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
            builder.RegisterType<DistributedLockManager>().SingleInstance();
            builder.RegisterType<TemplatesStorageReader>()
                   .WithParameter(
                       (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                       (parameterInfo, context) => context.Resolve<ICephS3Client>())
                   .SingleInstance();
            builder.RegisterType<ObjectsStorageReader>()
                   .WithParameter(
                       (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                       (parameterInfo, context) => context.Resolve<ICephS3Client>())
                   .SingleInstance();
            builder.RegisterType<ImagePreviewService>()
                   .WithParameter(
                       (parameterInfo, context) => parameterInfo.ParameterType == typeof(IS3Client),
                       (parameterInfo, context) => context.Resolve<ICephS3Client>())
                   .SingleInstance();
            builder.RegisterType<MetricsProvider>().SingleInstance();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
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
    }
}