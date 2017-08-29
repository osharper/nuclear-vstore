using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

using Autofac;
using Autofac.Extensions.DependencyInjection;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

using Moq;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Host;
using NuClear.VStore.S3;

using Xunit;

namespace VStore.UnitTests
{
    public class HostTests : IDisposable
    {
        private readonly TestServer _server;
        private readonly HttpClient _client;
        private readonly Mock<IS3Client> _mockS3Client;

        public HostTests()
        {
            _mockS3Client = new Mock<IS3Client>();
            SetupMockS3();

            _server = new TestServer(
                new WebHostBuilder()
                    .UseEnvironment(EnvironmentName.Development)
                    .ConfigureServices(
                        services =>
                            {
                                services.AddAutofac(
                                    x =>
                                        {
                                            x.RegisterInstance(_mockS3Client.Object).Named<IS3Client>("AWS");
                                            x.RegisterInstance(_mockS3Client.Object).Named<IS3Client>("Ceph");
                                        });
                            })
                    .UseStartup<Startup>());

            _client = _server.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJva2FwaSJ9.001QCdGC5mXuecjP1OfhafA6BsBB56ASHdoKA4btkak");
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
            _mockS3Client.ResetCalls();
            using (var response = await _client.GetAsync("/api/1.0/objects/123/some_version_id"))
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                _mockS3Client.Verify(s3 => s3.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(1));
            }

            _mockS3Client.ResetCalls();
            using (var response = await _client.GetAsync("/api/1.0/objects/123/versions"))
            {
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                _mockS3Client.Verify(s3 => s3.ListVersionsAsync(It.IsAny<ListVersionsRequest>()), Times.Exactly(2));
            }
        }

        public void Dispose()
        {
            _server?.Dispose();
            _client?.Dispose();
        }

        private void SetupMockS3()
        {
            _mockS3Client.Setup(s3 => s3.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>()))
                   .Throws(new AmazonS3Exception("Mock error", ErrorType.Unknown, "NoSuchKey", String.Empty, HttpStatusCode.NotFound));
            _mockS3Client.Setup(s3 => s3.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                   .Throws(new AmazonS3Exception("Mock error", ErrorType.Unknown, "NoSuchKey", String.Empty, HttpStatusCode.NotFound));
            _mockS3Client.Setup(s3 => s3.ListVersionsAsync(It.IsAny<string>(), It.IsAny<string>()))
                   .Returns(() => Task.FromResult(new ListVersionsResponse()));
            _mockS3Client.Setup(s3 => s3.ListVersionsAsync(It.IsAny<ListVersionsRequest>()))
                   .Returns(() => Task.FromResult(new ListVersionsResponse()));
        }
    }
}
