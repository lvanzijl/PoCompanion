using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Settings;
using System.Net.Http;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class GlobalFilterRouteServiceTests
{
    [TestMethod]
    public async Task BuildCurrentPageUriAsync_PlanningOverview_EmitsAliasPathAndCanonicalQuery()
    {
        var routeService = CreateRouteService();

        var uri = await routeService.BuildCurrentPageUriAsync(
            "http://localhost/planning/payments-platform/overview?teamId=7",
            new FilterState([11], ["project-payments"], 7, new FilterTimeSelection(FilterTimeMode.Snapshot)));

        Assert.AreEqual("/planning/payments-platform/overview?productId=11&teamId=7&timeMode=Snapshot", uri);
    }

    [TestMethod]
    public async Task BuildCurrentPageUriAsync_ProjectScopedPlanBoard_PreservesRouteAliasWhenStateHasNoProject()
    {
        var routeService = CreateRouteService();

        var uri = await routeService.BuildCurrentPageUriAsync(
            "http://localhost/planning/payments-platform/plan-board",
            new FilterState([11], Array.Empty<string>(), null, FilterTimeSelection.Snapshot));

        Assert.AreEqual("/planning/payments-platform/plan-board?productId=11&timeMode=Snapshot", uri);
    }

    [TestMethod]
    public async Task BuildCurrentPageUriAsync_ProductScopedEditor_UsesSelectedProductPathAndQuery()
    {
        var routeService = CreateRouteService();

        var uri = await routeService.BuildCurrentPageUriAsync(
            "http://localhost/planning/product-roadmaps/11?teamId=7",
            new FilterState([12], Array.Empty<string>(), 7, FilterTimeSelection.Snapshot));

        Assert.AreEqual("/planning/product-roadmaps/12?productId=12&teamId=7&timeMode=Snapshot", uri);
    }

    [TestMethod]
    public async Task BuildUriAsync_RollingState_EmitsExplicitRollingQuery()
    {
        var routeService = CreateRouteService();

        var uri = await routeService.BuildUriAsync(
            "/home/pull-requests",
            new FilterState(
                Array.Empty<int>(),
                Array.Empty<string>(),
                7,
                new FilterTimeSelection(FilterTimeMode.Rolling, RollingWindow: 30, RollingUnit: FilterTimeUnit.Days)));

        Assert.AreEqual("/home/pull-requests?teamId=7&timeMode=Rolling&rollingWindow=30&rollingUnit=Days", uri);
    }

    private static GlobalFilterRouteService CreateRouteService()
    {
        var projectService = new ProjectService(new StubProjectsClient());
        var projectIdentityMapper = new ProjectIdentityMapper(projectService);
        return new GlobalFilterRouteService(projectIdentityMapper);
    }

    private sealed class StubProjectsClient : IProjectsClient
    {
        private static readonly ProjectDto PaymentsProject = new("project-payments", "payments-platform", "Payments Platform", [11]);

        public Task<ICollection<ProjectDto>> GetProjectsAsync()
            => Task.FromResult<ICollection<ProjectDto>>([PaymentsProject]);

        public Task<ICollection<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken)
            => Task.FromResult<ICollection<ProjectDto>>([PaymentsProject]);

        public Task<ProjectDto> GetProjectAsync(string alias)
            => GetProjectAsync(alias, CancellationToken.None);

        public Task<ProjectDto> GetProjectAsync(string alias, CancellationToken cancellationToken)
        {
            if (string.Equals(alias, "payments-platform", StringComparison.OrdinalIgnoreCase)
                || string.Equals(alias, "project-payments", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(PaymentsProject);
            }

            throw new ApiException("Not found", 404, string.Empty, new Dictionary<string, IEnumerable<string>>(), null);
        }

        public Task<ICollection<ProductDto>> GetProjectProductsAsync(string alias)
            => Task.FromResult<ICollection<ProductDto>>([]);

        public Task<ICollection<ProductDto>> GetProjectProductsAsync(string alias, CancellationToken cancellationToken)
            => Task.FromResult<ICollection<ProductDto>>([]);

        public Task<DataStateResponseDtoOfProjectPlanningSummaryDto> GetPlanningSummaryAsync(string alias)
            => throw new NotSupportedException();

        public Task<DataStateResponseDtoOfProjectPlanningSummaryDto> GetPlanningSummaryAsync(string alias, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
