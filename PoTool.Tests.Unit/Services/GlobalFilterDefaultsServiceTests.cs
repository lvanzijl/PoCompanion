using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Planning;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class GlobalFilterDefaultsServiceTests
{
    [TestMethod]
    public async Task BuildDefaultedUriAsync_SprintExecution_UsesFirstTeamAndCurrentSprintOncePerSession()
    {
        var store = CreateStore();
        await store.TrackNavigationAsync("http://localhost/home/delivery/execution", 42);

        var defaultsService = CreateDefaultsService(store);

        var firstUri = await defaultsService.BuildDefaultedUriAsync("http://localhost/home/delivery/execution", 42);
        var secondUri = await defaultsService.BuildDefaultedUriAsync("http://localhost/home/delivery/execution", 42);

        Assert.AreEqual("/home/delivery/execution?teamId=7&sprintId=701&timeMode=Sprint", firstUri);
        Assert.IsNull(secondUri);
    }

    [TestMethod]
    public async Task BuildDefaultedUriAsync_PlanBoard_KeepsAllProductsSelected()
    {
        var store = CreateStore();
        await store.TrackNavigationAsync("http://localhost/planning/plan-board", 42);

        var defaultsService = CreateDefaultsService(store);

        var uri = await defaultsService.BuildDefaultedUriAsync("http://localhost/planning/plan-board", 42);

        Assert.IsNull(uri);
    }

    [TestMethod]
    public async Task BuildDefaultedUriAsync_InvalidUsage_DoesNotApplyDefaults()
    {
        var store = CreateStore();
        await store.TrackNavigationAsync("http://localhost/planning/unknown-project/overview", 42);

        store.SetResolvedState(store.CurrentUsage! with { Status = FilterResolutionStatus.Invalid });
        var defaultsService = CreateDefaultsService(store);

        var uri = await defaultsService.BuildDefaultedUriAsync("http://localhost/planning/unknown-project/overview", 42);

        Assert.IsNull(uri);
    }

    [TestMethod]
    public async Task BuildDefaultedUriAsync_UserDrivenState_DoesNotReapplyDefaults()
    {
        var store = CreateStore();
        await store.TrackNavigationAsync("http://localhost/home/delivery/execution", 42);

        store.SetResolvedState(store.CurrentUsage! with { LastUpdateSource = FilterUpdateSource.Ui });
        var defaultsService = CreateDefaultsService(store);

        var uri = await defaultsService.BuildDefaultedUriAsync("http://localhost/home/delivery/execution", 42);

        Assert.IsNull(uri);
    }

    [TestMethod]
    public async Task BuildDefaultedUriAsync_ResolvedState_DoesNotChangeRoute()
    {
        var store = CreateStore();
        await store.TrackNavigationAsync("http://localhost/home/delivery/execution?teamId=7&sprintId=701&timeMode=Sprint", 42);

        var defaultsService = CreateDefaultsService(store);

        var uri = await defaultsService.BuildDefaultedUriAsync("http://localhost/home/delivery/execution?teamId=7&sprintId=701&timeMode=Sprint", 42);

        Assert.IsNull(uri);
    }

    [TestMethod]
    public async Task BuildDefaultedUriAsync_UsesHistoricalSprintWhenCurrentSprintIsUnavailable()
    {
        var store = CreateStore();
        await store.TrackNavigationAsync("http://localhost/home/delivery/execution", 42);

        var defaultsService = CreateDefaultsService(
            store,
            sprintsClient: new HistoricalOnlySprintsClient());

        var uri = await defaultsService.BuildDefaultedUriAsync("http://localhost/home/delivery/execution", 42);

        Assert.AreEqual("/home/delivery/execution?teamId=7&sprintId=799&timeMode=Sprint", uri);
    }

    [TestMethod]
    public async Task BuildDefaultedUriAsync_DoesNotFallbackToOtherProductTeams_WhenExplicitProductHasNoSprints()
    {
        var store = CreateStore();
        await store.TrackNavigationAsync("http://localhost/home/delivery/execution?productId=12", 42);

        var defaultsService = CreateDefaultsService(store);

        var uri = await defaultsService.BuildDefaultedUriAsync("http://localhost/home/delivery/execution?productId=12", 42);

        Assert.IsNull(uri);
    }

    private static GlobalFilterStore CreateStore()
    {
        var projectService = new ProjectService(new StubProjectsClient());
        var projectIdentityMapper = new ProjectIdentityMapper(projectService);
        var resolver = new FilterStateResolver(projectIdentityMapper);
        return new GlobalFilterStore(NullLogger<GlobalFilterStore>.Instance, resolver);
    }

    private static GlobalFilterDefaultsService CreateDefaultsService(
        GlobalFilterStore store,
        IProductsClient? productsClient = null,
        ITeamsClient? teamsClient = null,
        ISprintsClient? sprintsClient = null)
    {
        var projectService = new ProjectService(new StubProjectsClient());
        var projectIdentityMapper = new ProjectIdentityMapper(projectService);

        return new GlobalFilterDefaultsService(
            store,
            new GlobalFilterRouteService(projectIdentityMapper),
            new ProductService(productsClient ?? new StubProductsClient()),
            new TeamService(teamsClient ?? new StubTeamsClient()),
            new SprintService(sprintsClient ?? new StubSprintsClient()),
            new GlobalFilterContextResolver(),
            new InMemorySecureStorageService());
    }

    private sealed class InMemorySecureStorageService : ISecureStorageService
    {
        private readonly Dictionary<string, string> _storage = new(StringComparer.Ordinal);

        public Task<string?> GetAsync(string key)
            => Task.FromResult(_storage.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string value)
        {
            _storage[key] = value;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string key)
            => Task.FromResult(_storage.Remove(key));

        public Task RemoveAllAsync()
        {
            _storage.Clear();
            return Task.CompletedTask;
        }
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
            if (string.Equals(alias, PaymentsProject.Alias, StringComparison.OrdinalIgnoreCase)
                || string.Equals(alias, PaymentsProject.Id, StringComparison.OrdinalIgnoreCase))
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
        private static readonly TeamDto TeamAlpha = new(7, "Alpha", "\\Payments\\Alpha", false, TeamPictureType.Default, 0, null, Now, Now, "Payments Platform", "alpha", "Alpha", Now);
        private static readonly TeamDto TeamBravo = new(8, "Bravo", "\\Payments\\Bravo", false, TeamPictureType.Default, 1, null, Now, Now, "Payments Platform", "bravo", "Bravo", Now);

        public Task<ICollection<TeamDto>> GetAllTeamsAsync(bool? includeArchived)
            => Task.FromResult<ICollection<TeamDto>>([TeamAlpha, TeamBravo]);

        public Task<ICollection<TeamDto>> GetAllTeamsAsync(bool? includeArchived, CancellationToken cancellationToken)
            => Task.FromResult<ICollection<TeamDto>>([TeamAlpha, TeamBravo]);

        public Task<TeamDto> CreateTeamAsync(CreateTeamRequest request) => throw new NotSupportedException();
        public Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TeamDto> GetTeamByIdAsync(int id) => throw new NotSupportedException();
        public Task<TeamDto> GetTeamByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
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
        private static readonly SprintDto Sprint699 = new(699, 7, "699", "\\Payments\\Sprint 9", "Sprint 9", Now.AddDays(-28), Now.AddDays(-15), "past", Now);
        private static readonly SprintDto Sprint700 = new(700, 7, "700", "\\Payments\\Sprint 10", "Sprint 10", Now.AddDays(-14), Now.AddDays(-1), "past", Now);
        private static readonly SprintDto Sprint701 = new(701, 7, "701", "\\Payments\\Sprint 11", "Sprint 11", Now, Now.AddDays(13), "current", Now);

        public Task<ICollection<SprintDto>> GetSprintsForTeamAsync(int? teamId)
            => GetSprintsForTeamAsync(teamId, CancellationToken.None);

        public Task<ICollection<SprintDto>> GetSprintsForTeamAsync(int? teamId, CancellationToken cancellationToken)
            => Task.FromResult<ICollection<SprintDto>>(teamId == 7 ? [Sprint699, Sprint700, Sprint701] : []);

        public Task<SprintDto> GetCurrentSprintForTeamAsync(int? teamId)
            => GetCurrentSprintForTeamAsync(teamId, CancellationToken.None);

        public Task<SprintDto> GetCurrentSprintForTeamAsync(int? teamId, CancellationToken cancellationToken)
        {
            if (teamId == 7)
            {
                return Task.FromResult(Sprint701);
            }

            throw new ApiException("Not found", 404, string.Empty, new Dictionary<string, IEnumerable<string>>(), null);
        }
    }

    private sealed class HistoricalOnlySprintsClient : ISprintsClient
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-04T00:00:00Z");
        private static readonly SprintDto Sprint798 = new(798, 7, "798", "\\Payments\\Sprint 10", "Sprint 10", Now.AddDays(-28), Now.AddDays(-15), "past", Now);
        private static readonly SprintDto Sprint799 = new(799, 7, "799", "\\Payments\\Sprint 11", "Sprint 11", Now.AddDays(-14), Now.AddDays(-1), "past", Now);

        public Task<ICollection<SprintDto>> GetSprintsForTeamAsync(int? teamId)
            => GetSprintsForTeamAsync(teamId, CancellationToken.None);

        public Task<ICollection<SprintDto>> GetSprintsForTeamAsync(int? teamId, CancellationToken cancellationToken)
            => Task.FromResult<ICollection<SprintDto>>(teamId == 7 ? [Sprint798, Sprint799] : []);

        public Task<SprintDto> GetCurrentSprintForTeamAsync(int? teamId)
            => GetCurrentSprintForTeamAsync(teamId, CancellationToken.None);

        public Task<SprintDto> GetCurrentSprintForTeamAsync(int? teamId, CancellationToken cancellationToken)
            => throw new ApiException("Not found", 404, string.Empty, new Dictionary<string, IEnumerable<string>>(), null);
    }
}
