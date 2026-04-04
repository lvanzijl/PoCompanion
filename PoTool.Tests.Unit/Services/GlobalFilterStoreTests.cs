using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Settings;
using System.Net.Http;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class GlobalFilterStoreTests
{
    [TestMethod]
    public async Task TrackNavigation_UnknownRoute_ResetsToNeutral()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/settings");

        Assert.IsNull(store.CurrentUsage);
        Assert.IsTrue(store.CurrentState.AllProducts);
        Assert.IsTrue(store.CurrentState.AllProjects);
        Assert.AreEqual(FilterTimeMode.Snapshot, store.CurrentState.Time.Mode);
    }

    [TestMethod]
    public async Task TrackNavigation_ProjectScopedPlanningRoute_UsesProjectIdFromPathAlias()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/planning/payments-platform/overview", 42);

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual("ProjectPlanningOverview", store.CurrentUsage.PageName);
        CollectionAssert.AreEqual(new[] { "project-payments" }, store.CurrentState.ProjectIds.ToArray());
        Assert.AreEqual(42, store.CurrentUsage.ActiveProfileId);
        Assert.AreEqual(FilterUpdateSource.Route, store.CurrentUsage.LastUpdateSource);
    }

    [TestMethod]
    public async Task TrackNavigation_RouteQueryProjectConflict_PrefersRouteAliasDeterministically()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/planning/payments-platform/overview?projectId=project-other&projectAlias=other-project");

        Assert.IsNotNull(store.CurrentUsage);
        CollectionAssert.AreEqual(new[] { "project-payments" }, store.CurrentState.ProjectIds.ToArray());
        Assert.AreEqual(FilterResolutionStatus.ResolvedWithNormalization, store.CurrentUsage.Status);
    }

    [TestMethod]
    public async Task TrackNavigation_SprintRouteWithoutSelections_FlagsMissingTeamAndSprint()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/home/delivery/sprint");

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual("SprintTrend", store.CurrentUsage.PageName);
        Assert.IsTrue(store.CurrentUsage.MissingTeam);
        Assert.IsTrue(store.CurrentUsage.MissingSprint);
        Assert.AreEqual(FilterResolutionStatus.Unresolved, store.CurrentUsage.Status);
    }

    [TestMethod]
    public async Task TrackNavigation_QueryBackedRoute_TracksKnownFilterValues()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/home/trends?productId=5&teamId=7&fromSprintId=101&toSprintId=100");

        Assert.IsNotNull(store.CurrentUsage);
        CollectionAssert.AreEqual(new[] { 5 }, store.CurrentState.ProductIds.ToArray());
        Assert.AreEqual(7, store.CurrentState.TeamId);
        Assert.AreEqual(FilterTimeMode.Range, store.CurrentState.Time.Mode);
        Assert.AreEqual(100, store.CurrentState.Time.StartSprintId);
        Assert.AreEqual(101, store.CurrentState.Time.EndSprintId);
        Assert.IsFalse(store.CurrentUsage.MissingTeam);
        Assert.IsFalse(store.CurrentUsage.MissingSprint);
        CollectionAssert.Contains(store.CurrentUsage.NormalizationDecisions.ToArray(), "invalid range normalized by swapping from/to sprint IDs");
        Assert.AreEqual(FilterResolutionStatus.ResolvedWithNormalization, store.CurrentUsage.Status);
    }

    [TestMethod]
    public async Task TrackNavigation_RollingQuery_TracksExplicitWindowAndUnit()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/home/pull-requests?teamId=7&timeMode=Rolling&rollingWindow=30&rollingUnit=Days");

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual(FilterTimeMode.Rolling, store.CurrentState.Time.Mode);
        Assert.AreEqual(30, store.CurrentState.Time.RollingWindow);
        Assert.AreEqual(FilterTimeUnit.Days, store.CurrentState.Time.RollingUnit);
    }

    [TestMethod]
    public async Task TrackNavigation_UnknownProjectAlias_ClassifiesStateAsInvalid()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/planning/unknown-project/overview");

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual(FilterResolutionStatus.Invalid, store.CurrentUsage.Status);
        CollectionAssert.Contains(store.CurrentUsage.StateIssues.ToArray(), "route project alias 'unknown-project' could not be resolved");
    }

    [TestMethod]
    public async Task TryPrepareNavigation_IgnoresEquivalentRoutesAndPendingDuplicates()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/home/trends?teamId=7&fromSprintId=100&toSprintId=101");

        Assert.IsFalse(store.TryPrepareNavigation(
            "http://localhost/home/trends?fromSprintId=100&toSprintId=101&teamId=7",
            "http://localhost/home/trends?teamId=7&toSprintId=101&fromSprintId=100"));

        Assert.IsTrue(store.TryPrepareNavigation(
            "http://localhost/home/trends?teamId=7&fromSprintId=100&toSprintId=101",
            "http://localhost/home/trends?teamId=7&fromSprintId=100&toSprintId=102"));

        Assert.IsFalse(store.TryPrepareNavigation(
            "http://localhost/home/trends?teamId=7&fromSprintId=100&toSprintId=101",
            "http://localhost/home/trends?toSprintId=102&teamId=7&fromSprintId=100"));
    }

    [TestMethod]
    public async Task TryPrepareNavigation_ClearsPendingSignatureAfterObservedNavigation()
    {
        var store = CreateStore();

        Assert.IsTrue(store.TryPrepareNavigation(
            "http://localhost/home/trends?teamId=7&fromSprintId=100&toSprintId=101",
            "http://localhost/home/trends?teamId=7&fromSprintId=101&toSprintId=102"));

        await store.TrackNavigationAsync("http://localhost/home/trends?teamId=7&fromSprintId=101&toSprintId=102");

        Assert.IsNull(store.PendingRouteSignature);
        Assert.AreEqual("home/trends?fromSprintId=101&teamId=7&toSprintId=102", store.CurrentRouteSignature);
    }

    private static GlobalFilterStore CreateStore()
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        var projectService = new ProjectService(new StubProjectsClient(), httpClient);
        var projectIdentityMapper = new ProjectIdentityMapper(projectService);
        var resolver = new FilterStateResolver(projectIdentityMapper);
        return new GlobalFilterStore(NullLogger<GlobalFilterStore>.Instance, resolver);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }

    private sealed class StubProjectsClient : IProjectsClient
    {
        private static readonly ProjectDto PaymentsProject = new("project-payments", "payments-platform", "Payments Platform", [11, 12]);

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
