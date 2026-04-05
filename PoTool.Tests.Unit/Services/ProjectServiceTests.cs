using System.Net.Http;
using System.Reflection;
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

    [TestMethod]
    public void ProjectsClient_Uses_CaseInsensitive_Json_Settings()
    {
        var client = new ProjectsClient(new HttpClient());
        var settingsProperty = typeof(ProjectsClient).GetProperty(
            "JsonSerializerSettings",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(settingsProperty);
        Assert.AreEqual(typeof(System.Text.Json.JsonSerializerOptions), settingsProperty.PropertyType);

        var settings = settingsProperty.GetValue(client) as System.Text.Json.JsonSerializerOptions;

        Assert.IsNotNull(settings);
        Assert.IsTrue(settings.PropertyNameCaseInsensitive);
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
