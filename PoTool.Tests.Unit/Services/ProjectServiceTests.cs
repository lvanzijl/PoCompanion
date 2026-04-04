using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class ProjectServiceTests
{
    [TestMethod]
    public async Task GetPlanningSummaryAsync_MalformedEnvelope_DoesNotReturnSuccess()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ \"state\": 2, \"data\": null }", Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("http://localhost")
        };

        var service = new ProjectService(new StubProjectsClient(), httpClient);

        var result = await service.GetPlanningSummaryAsync("project-alpha");

        Assert.AreEqual(CacheBackedClientState.Failed, result.State);
        Assert.IsNull(result.Data);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    private sealed class StubProjectsClient : IProjectsClient
    {
        public Task<ICollection<ProjectDto>> GetProjectsAsync()
            => Task.FromResult<ICollection<ProjectDto>>([]);

        public Task<ICollection<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken)
            => Task.FromResult<ICollection<ProjectDto>>([]);

        public Task<ProjectDto> GetProjectAsync(string alias)
            => throw new NotSupportedException();

        public Task<ProjectDto> GetProjectAsync(string alias, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ICollection<ProductDto>> GetProjectProductsAsync(string alias)
            => throw new NotSupportedException();

        public Task<ICollection<ProductDto>> GetProjectProductsAsync(string alias, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<DataStateResponseDtoOfProjectPlanningSummaryDto> GetPlanningSummaryAsync(string alias)
            => throw new NotSupportedException();

        public Task<DataStateResponseDtoOfProjectPlanningSummaryDto> GetPlanningSummaryAsync(string alias, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
