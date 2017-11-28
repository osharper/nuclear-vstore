using System.Collections.Generic;
using System.Globalization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using NuClear.VStore.Http;
using NuClear.VStore.Http.Core.Middleware;
using NuClear.VStore.Http.Core.Swashbuckle;

using Prometheus.Client.Collectors;
using Prometheus.Client.Owin;

using Swashbuckle.AspNetCore.Swagger;

namespace NuClear.VStore.Renderer
{
    public sealed class Startup
    {
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
            services.AddMvcCore()
                    .AddVersionedApiExplorer()
                    .AddApiExplorer()
                    .AddCors()
                    .AddJsonFormatters()
                    .AddJsonOptions(
                        options =>
                            {
                                var settings = options.SerializerSettings;

                                settings.Culture = CultureInfo.InvariantCulture;
                                settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                            });

            services.AddApiVersioning(options => options.ReportApiVersions = true);
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
                    });
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