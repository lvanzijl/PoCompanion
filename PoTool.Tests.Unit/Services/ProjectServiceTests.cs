using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.DataState;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class ProjectServiceTests
{
    [TestMethod]
    public async Task GetPlanningSummaryAsync_MalformedEnvelope_DoesNotReturnSuccess()
    {
        var service = new ProjectService(new StubProjectsClient());

        var result = await service.GetPlanningSummaryAsync("project-alpha");

        Assert.AreEqual(CacheBackedClientState.Failed, result.State);
        Assert.IsNull(result.Data);
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
            => GetPlanningSummaryAsync(alias, CancellationToken.None);

        public Task<DataStateResponseDtoOfProjectPlanningSummaryDto> GetPlanningSummaryAsync(string alias, CancellationToken cancellationToken)
            => Task.FromResult(new DataStateResponseDtoOfProjectPlanningSummaryDto
            {
                State = DataStateDto.Available,
                Data = null
            });
    }
}
