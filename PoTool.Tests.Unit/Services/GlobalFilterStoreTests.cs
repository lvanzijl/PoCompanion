using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Planning;
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

        await store.TrackNavigationAsync("http://localhost/home/delivery/sprint", 42);

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual("SprintTrend", store.CurrentUsage.PageName);
        Assert.AreEqual(7, store.CurrentState.TeamId);
        Assert.AreEqual(701, store.CurrentState.Time.SprintId);
        Assert.IsFalse(store.CurrentUsage.MissingTeam);
        Assert.IsFalse(store.CurrentUsage.MissingSprint);
        Assert.AreEqual(FilterResolutionStatus.ResolvedWithNormalization, store.CurrentUsage.Status);
    }

    [TestMethod]
    public async Task TrackNavigation_RangeRouteWithoutSelections_DefaultsToHistoricalSprintWindow()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/home/delivery/portfolio", 42);

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual("PortfolioDelivery", store.CurrentUsage.PageName);
        Assert.AreEqual(7, store.CurrentState.TeamId);
        Assert.AreEqual(FilterTimeMode.Range, store.CurrentState.Time.Mode);
        Assert.AreEqual(697, store.CurrentState.Time.StartSprintId);
        Assert.AreEqual(701, store.CurrentState.Time.EndSprintId);
        Assert.AreEqual(FilterResolutionStatus.ResolvedWithNormalization, store.CurrentUsage.Status);
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
    public async Task TrackNavigation_ProductScopedEditorRoute_TreatsRouteProductAsHintOnly()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/planning/product-roadmaps/11");

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual("ProductRoadmapEditor", store.CurrentUsage.PageName);
        Assert.IsFalse(store.CurrentUsage.HasRouteProductAuthority);
        Assert.IsTrue(store.CurrentState.AllProducts);
        CollectionAssert.Contains(store.CurrentUsage.NormalizationDecisions.ToArray(), "route productId 11 is treated as a lookup hint only");
    }

    [TestMethod]
    public async Task TrackNavigation_ProductScopedEditorRoute_MismatchedGlobalProduct_IsInvalid()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/planning/product-roadmaps/11?productId=12");

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual(FilterResolutionStatus.Invalid, store.CurrentUsage.Status);
        CollectionAssert.Contains(
            store.CurrentUsage.StateIssues.ToArray(),
            "Route product '11' does not match the selected global product '12'.");
    }

    [TestMethod]
    public async Task TrackNavigation_ProjectScopedPlanningRoute_ProductOutsideAllowedUniverse_IsInvalid()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/planning/payments-platform/plan-board?productId=99");

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual(FilterResolutionStatus.Invalid, store.CurrentUsage.Status);
        CollectionAssert.Contains(
            store.CurrentUsage.StateIssues.ToArray(),
            "Selected global product '99' is not available in project route 'payments-platform'.");
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
    public async Task TrackNavigation_UnknownProjectAlias_PreservesRouteAuthority()
    {
        var store = CreateStore();

        await store.TrackNavigationAsync("http://localhost/planning/unknown-project/overview");

        Assert.IsNotNull(store.CurrentUsage);
        Assert.AreEqual(FilterResolutionStatus.ResolvedWithNormalization, store.CurrentUsage.Status);
        CollectionAssert.Contains(store.CurrentUsage.NormalizationDecisions.ToArray(), "route project alias 'unknown-project' is authoritative without a resolved projectId");
    }

    [TestMethod]
    public async Task BuildCorrectedUriAsync_ProjectScopedRouteAliasIssue_DoesNotRedirectAway()
    {
        var projectService = new ProjectService(new StubProjectsClient());
        var projectIdentityMapper = new ProjectIdentityMapper(projectService);
        var resolver = new FilterStateResolver(projectIdentityMapper);
        var store = new GlobalFilterStore(
            NullLogger<GlobalFilterStore>.Instance,
            resolver,
            CreateAutoResolveService(),
            CreateLabelService());
        var correctionService = new GlobalFilterCorrectionService(
            store,
            new GlobalFilterRouteService(projectIdentityMapper),
            new GlobalFilterUiState());

        await store.TrackNavigationAsync("http://localhost/planning/unknown-project/overview");

        var correctedUri = await correctionService.BuildCorrectedUriAsync("http://localhost/planning/unknown-project/overview");

        Assert.IsNull(correctedUri, "Project-scoped route aliases should stay on-page so the route itself can handle unknown projects.");
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
        var projectService = new ProjectService(new StubProjectsClient());
        var projectIdentityMapper = new ProjectIdentityMapper(projectService);
        var resolver = new FilterStateResolver(projectIdentityMapper);
        return new GlobalFilterStore(
            NullLogger<GlobalFilterStore>.Instance,
            resolver,
            CreateAutoResolveService(),
            CreateLabelService());
    }

    private static GlobalFilterAutoResolveService CreateAutoResolveService()
        => new(
            new ProductService(new StubProductsClient()),
            new TeamService(new StubTeamsClient()),
            new SprintService(new StubSprintsClient()),
            new GlobalFilterContextResolver());

    private static GlobalFilterLabelService CreateLabelService()
        => new(new TeamService(new StubTeamsClient()), new SprintService(new StubSprintsClient()));

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

    private sealed class StubProductsClient : IProductsClient
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-04T00:00:00Z");
        private static readonly ProductDto PrimaryProduct = new(11, 42, "Incident Response Control", [1000], 0, ProductPictureType.Default, 9, null, Now, Now, Now, [7], []);
        private static readonly ProductDto SecondaryProduct = new(12, 42, "Crew Safety Operations", [2000], 1, ProductPictureType.Default, 11, null, Now, Now, Now, [8], []);

        public Task<ICollection<ProductDto>> GetProductsByOwnerAsync(int? productOwnerId)
            => Task.FromResult<ICollection<ProductDto>>([PrimaryProduct, SecondaryProduct]);

        public Task<ICollection<ProductDto>> GetProductsByOwnerAsync(int? productOwnerId, CancellationToken cancellationToken)
            => Task.FromResult<ICollection<ProductDto>>([PrimaryProduct, SecondaryProduct]);

        public Task<ProductDto> CreateProductAsync(CreateProductRequest request) => throw new NotSupportedException();
        public Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProductDto> GetProductByIdAsync(int id) => throw new NotSupportedException();
        public Task<ProductDto> GetProductByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProductDto> UpdateProductAsync(int id, UpdateProductRequest request) => throw new NotSupportedException();
        public Task<ProductDto> UpdateProductAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteProductAsync(int id) => throw new NotSupportedException();
        public Task DeleteProductAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ICollection<PlanningEpicProjectionDto>> GetPlanningProjectionsAsync(int productId) => throw new NotSupportedException();
        public Task<ICollection<PlanningEpicProjectionDto>> GetPlanningProjectionsAsync(int productId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ICollection<ProductDto>> ReorderProductsAsync(ReorderProductsRequest request) => throw new NotSupportedException();
        public Task<ICollection<ProductDto>> ReorderProductsAsync(ReorderProductsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LinkTeamToProductAsync(int productId, int teamId) => throw new NotSupportedException();
        public Task LinkTeamToProductAsync(int productId, int teamId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UnlinkTeamFromProductAsync(int productId, int teamId) => throw new NotSupportedException();
        public Task UnlinkTeamFromProductAsync(int productId, int teamId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ICollection<ProductDto>> GetAllProductsAsync() => throw new NotSupportedException();
        public Task<ICollection<ProductDto>> GetAllProductsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ICollection<ProductDto>> GetSelectableProductsAsync(int? productOwnerId) => throw new NotSupportedException();
        public Task<ICollection<ProductDto>> GetSelectableProductsAsync(int? productOwnerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ProductDto> ChangeProductOwnerAsync(int productId, ChangeProductOwnerRequest request) => throw new NotSupportedException();
        public Task<ProductDto> ChangeProductOwnerAsync(int productId, ChangeProductOwnerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RepositoryDto> CreateRepositoryAsync(int productId, CreateRepositoryRequest request) => throw new NotSupportedException();
        public Task<RepositoryDto> CreateRepositoryAsync(int productId, CreateRepositoryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteRepositoryAsync(int productId, int repositoryId) => throw new NotSupportedException();
        public Task DeleteRepositoryAsync(int productId, int repositoryId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubTeamsClient : ITeamsClient
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-04T00:00:00Z");
        private static readonly TeamDto TeamSeven = new(7, "Atlas", "\\Payments\\Atlas", false, TeamPictureType.Default, 0, null, Now, Now, "Payments Platform", "atlas", "Atlas", Now);
        private static readonly TeamDto TeamEight = new(8, "Beacon", "\\Payments\\Beacon", false, TeamPictureType.Default, 1, null, Now, Now, "Payments Platform", "beacon", "Beacon", Now);

        public Task<ICollection<TeamDto>> GetAllTeamsAsync(bool? includeArchived)
            => Task.FromResult<ICollection<TeamDto>>([TeamSeven, TeamEight]);

        public Task<ICollection<TeamDto>> GetAllTeamsAsync(bool? includeArchived, CancellationToken cancellationToken)
            => Task.FromResult<ICollection<TeamDto>>([TeamSeven, TeamEight]);

        public Task<TeamDto> GetTeamByIdAsync(int id)
            => GetTeamByIdAsync(id, CancellationToken.None);

        public Task<TeamDto> GetTeamByIdAsync(int id, CancellationToken cancellationToken)
            => id switch
            {
                7 => Task.FromResult(TeamSeven),
                8 => Task.FromResult(TeamEight),
                _ => throw new ApiException("Not found", 404, string.Empty, new Dictionary<string, IEnumerable<string>>(), null)
            };

        public Task<TeamDto> CreateTeamAsync(CreateTeamRequest request) => throw new NotSupportedException();
        public Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TeamDto> UpdateTeamAsync(int id, UpdateTeamRequest request) => throw new NotSupportedException();
        public Task<TeamDto> UpdateTeamAsync(int id, UpdateTeamRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteTeamAsync(int id) => throw new NotSupportedException();
        public Task DeleteTeamAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TeamDto> ArchiveTeamAsync(int id, ArchiveTeamRequest request) => throw new NotSupportedException();
        public Task<TeamDto> ArchiveTeamAsync(int id, ArchiveTeamRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubSprintsClient : ISprintsClient
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-04T00:00:00Z");
        private static readonly SprintDto[] TeamSevenSprints =
        [
            new(697, 7, "697", "\\Payments\\Sprint 7", "Sprint 7", Now.AddDays(-56), Now.AddDays(-43), "past", Now),
            new(698, 7, "698", "\\Payments\\Sprint 8", "Sprint 8", Now.AddDays(-42), Now.AddDays(-29), "past", Now),
            new(699, 7, "699", "\\Payments\\Sprint 9", "Sprint 9", Now.AddDays(-28), Now.AddDays(-15), "past", Now),
            new(700, 7, "700", "\\Payments\\Sprint 10", "Sprint 10", Now.AddDays(-14), Now.AddDays(-1), "past", Now),
            new(701, 7, "701", "\\Payments\\Sprint 11", "Sprint 11", Now, Now.AddDays(13), "current", Now)
        ];

        public Task<ICollection<SprintDto>> GetSprintsForTeamAsync(int? teamId)
            => GetSprintsForTeamAsync(teamId, CancellationToken.None);

        public Task<ICollection<SprintDto>> GetSprintsForTeamAsync(int? teamId, CancellationToken cancellationToken)
            => Task.FromResult<ICollection<SprintDto>>(teamId == 7 ? TeamSevenSprints : []);

        public Task<SprintDto> GetCurrentSprintForTeamAsync(int? teamId)
            => GetCurrentSprintForTeamAsync(teamId, CancellationToken.None);

        public Task<SprintDto> GetCurrentSprintForTeamAsync(int? teamId, CancellationToken cancellationToken)
        {
            if (teamId == 7)
            {
                return Task.FromResult(TeamSevenSprints[^1]);
            }

            throw new ApiException("Not found", 404, string.Empty, new Dictionary<string, IEnumerable<string>>(), null);
        }
    }
}
