using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

using Amazon.S3;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Moq;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Host;

using Xunit;

namespace VStore.UnitTests
{
    public class HostTests : IDisposable
    {
        private readonly TestServer _server;
        private readonly HttpClient _client;

        public HostTests()
        {
            Startup app = null;
            IHostingEnvironment hostingEnvironment = null;
            _server = new TestServer(
                new WebHostBuilder()
                    .UseEnvironment(EnvironmentName.Development)
                    .ConfigureServices(services =>
                                           {
                                               hostingEnvironment = GetHostingEnvironment(services);
                                               app = new Startup(hostingEnvironment);
                                               app.ConfigureServices(services);
                                               services.Replace(ServiceDescriptor.Singleton(x => Mock.Of<IAmazonS3>()));
                                           })
                    .Configure(builder => app.Configure(builder,
                                                        hostingEnvironment,
                                                        builder.ApplicationServices.GetRequiredService<ILoggerFactory>(),
                                                        builder.ApplicationServices.GetRequiredService<IApplicationLifetime>())));
            _client = _server.CreateClient();
        }

        [Fact]
        public async Task TestGetAvailableElementDescriptors()
        {
            using (var response = await _client.GetAsync("/api/1.0/templates/element-descriptors/available"))
            {
                response.EnsureSuccessStatusCode();
                var stringResponse = await response.Content.ReadAsStringAsync();
                var json = JArray.Parse(stringResponse);
                Assert.NotEmpty(json);
            }
        }

        public void Dispose()
        {
            _server?.Dispose();
            _client?.Dispose();
        }

        private static IHostingEnvironment GetHostingEnvironment(IServiceCollection services)
        {
            var hostingEnvironment = (IHostingEnvironment)services
                .Single(service => service.ImplementationInstance is IHostingEnvironment)
                .ImplementationInstance;
            var assembly = typeof(Startup).GetTypeInfo().Assembly;
            hostingEnvironment.ApplicationName = assembly.GetName().Name;
            return hostingEnvironment;
        }
    }
}
