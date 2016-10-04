using Amazon;
using Amazon.S3;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NuClear.VStore.Host.Bindings;
using NuClear.VStore.Host.Convensions;
using NuClear.VStore.Host.Locks;
using NuClear.VStore.Host.Options;
using NuClear.VStore.Host.Swashbuckle;
using NuClear.VStore.Host.Templates;

namespace NuClear.VStore.Host
{
    public class Startup
    {
        private readonly IConfigurationRoot _configuration;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            _configuration = builder.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;

            services.AddMvc(options => options.UseGlobalRoutePrefix(new RouteAttribute("api/1.0")))
                    .AddJsonOptions(options => options.SerializerSettings.Converters.Insert(0, new TemplateDescriptorJsonConverter()));

            services.AddSwaggerGen(x => x.OperationFilter<UploadFileOperationFilter>());

            services.AddOptions();
            services.AddDefaultAWSOptions(_configuration.GetAWSOptions());
            services.Configure<CephOptions>(_configuration.GetSection("Ceph"));
            services.Configure<LockOptions>(_configuration.GetSection("Ceph:Locks"));

            services.AddAWSService<IAmazonS3>();
            services.AddSingleton<LockSessionManager>();
            services.AddScoped<LockSessionFactory>();
            services.AddScoped<TemplateManagementService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(_configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseDeveloperExceptionPage();

            app.UseMvc();
            app.UseSwagger();
            app.UseSwaggerUi();
        }
    }
}
