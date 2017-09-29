using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Prometheus.Client;
using Prometheus.Client.Collectors;

namespace NuClear.VStore.Worker
{
    internal class MetricServer : IMetricServer
    {
        private IWebHost _host;
        private readonly string _hostAddress;
        private readonly string _pathBase;
        private readonly ICollectorRegistry _registry;

        public const string DefaultPathBase = "/metrics";

        private MetricServer(IEnumerable<IOnDemandCollector> standardCollectors = null, ICollectorRegistry registry = null)
        {
            _registry = registry ?? CollectorRegistry.Instance;
            if (_registry != CollectorRegistry.Instance)
            {
                return;
            }

            if (standardCollectors == null)
            {
                standardCollectors = new[] { new DotNetStatsCollector() };
            }

            CollectorRegistry.Instance.RegisterOnDemandCollectors(standardCollectors);
        }

        public MetricServer(int port)
            : this("+", port, DefaultPathBase, null)
        {
        }

        public MetricServer(string host, int port)
            : this(host, port, DefaultPathBase, null)
        {
        }

        public MetricServer(string host, int port, string pathBase)
            : this(host, port, pathBase, null)
        {
        }

        public MetricServer(string hostname,
                            int port,
                            string pathBase,
                            IEnumerable<IOnDemandCollector> standardCollectors = null,
                            ICollectorRegistry registry = null)
            : this(standardCollectors, registry)
        {
            _pathBase = pathBase;
            _hostAddress = $"http://{hostname}:{port}/";
        }

        public bool IsRunning => _host != null;

        public void Start()
        {
            if (_host != null)
            {
                throw new Exception("Server is already running.");
            }

            _host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls(_hostAddress)
                .UseSetting(WebHostDefaults.ApplicationKey, typeof(MetricServer).FullName)
                .ConfigureServices(services => services.AddSingleton<IStartup>(new Startup(_registry, _pathBase)))
                .Build();

            _host.Start();
        }

        public void Stop()
        {
            if (_host == null)
            {
                return;
            }

            _host.Dispose();
            _host = null;
        }

        internal class Startup : IStartup
        {
            private readonly string _pathBase;
            private readonly ICollectorRegistry _registry;

            public Startup(ICollectorRegistry registry, string pathBase)
            {
                _registry = registry;
                _pathBase = pathBase;
            }

            public IServiceProvider ConfigureServices(IServiceCollection services)
            {
                return services.BuildServiceProvider();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UsePathBase(_pathBase)
                   .Run(context =>
                            {
                                var response = context.Response;
                                response.StatusCode = 200;

                                var acceptHeader = context.Request.Headers["Accept"];
                                var contentType = ScrapeHandler.GetContentType(acceptHeader);
                                response.ContentType = contentType;

                                using (var outputStream = response.Body)
                                {
                                    var collected = _registry.CollectAll();
                                    ScrapeHandler.ProcessScrapeRequest(collected, contentType, outputStream);
                                }

                                return Task.FromResult(true);
                            });
            }
        }
    }
}
