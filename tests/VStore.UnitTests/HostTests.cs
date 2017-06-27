using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

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
        private readonly Mock<IAmazonS3> _mockS3;

        public HostTests()
        {
            Startup app = null;
            IHostingEnvironment hostingEnvironment = null;
            _mockS3 = new Mock<IAmazonS3>();
            SetupMockS3();
            _server = new TestServer(
                new WebHostBuilder()
                    .UseEnvironment(EnvironmentName.Development)
                    .ConfigureServices(services =>
                                           {
                                               hostingEnvironment = GetHostingEnvironment(services);
                                               app = new Startup(hostingEnvironment);
                                               app.ConfigureServices(services);
                                               services.Replace(ServiceDescriptor.Singleton(x => _mockS3.Object));
                                           })
                    .Configure(builder => app.Configure(builder,
                                                        hostingEnvironment,
                                                        builder.ApplicationServices.GetRequiredService<ILoggerFactory>(),
                                                        builder.ApplicationServices.GetRequiredService<IApplicationLifetime>())));
            _client = _server.CreateClient();
        }

        [Theory]
        [InlineData("1.0")]
        [InlineData("1.1")]
        public async Task TestGetAvailableElementDescriptors(string version)
        {
            using (var response = await _client.GetAsync($"/api/{version}/templates/element-descriptors/available"))
            {
                response.EnsureSuccessStatusCode();
                var stringResponse = await response.Content.ReadAsStringAsync();
                var json = JArray.Parse(stringResponse);
                Assert.NotEmpty(json);
            }
        }

        [Fact]
        public async Task TestAmbigiousRoutes()
        {
            _mockS3.ResetCalls();
            using (var response = await _client.GetAsync("/api/1.0/objects/123/some_version_id"))
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                _mockS3.Verify(s3 => s3.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            }

            _mockS3.ResetCalls();
            using (var response = await _client.GetAsync("/api/1.0/objects/123/versions"))
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                _mockS3.Verify(s3 => s3.ListVersionsAsync(It.IsAny<ListVersionsRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
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

        private void SetupMockS3()
        {
            _mockS3.Setup(s3 => s3.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Throws(new AmazonS3Exception("Mock error", ErrorType.Unknown, "NoSuchKey", String.Empty, HttpStatusCode.NotFound));
            _mockS3.Setup(s3 => s3.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Throws(new AmazonS3Exception("Mock error", ErrorType.Unknown, "NoSuchKey", String.Empty, HttpStatusCode.NotFound));
            _mockS3.Setup(s3 => s3.ListVersionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .Returns(() => Task.FromResult(new ListVersionsResponse()));
            _mockS3.Setup(s3 => s3.ListVersionsAsync(It.IsAny<ListVersionsRequest>(), It.IsAny<CancellationToken>()))
                   .Returns(() => Task.FromResult(new ListVersionsResponse()));
        }
    }
}
