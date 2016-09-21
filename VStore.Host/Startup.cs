using Amazon;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NuClear.VStore.Host.Options;
using NuClear.VStore.Host.Swashbuckle;

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
            services.AddMvc();
            services.AddSwaggerGen(x => x.OperationFilter<UploadFileOperationFilter>());

            services.AddOptions();

            services.AddDefaultAWSOptions(_configuration.GetAWSOptions());
            AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;
            AWSConfigsS3.UseSignatureVersion4 = false;

            services.Configure<CephOptions>(_configuration.GetSection("Ceph"));
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
