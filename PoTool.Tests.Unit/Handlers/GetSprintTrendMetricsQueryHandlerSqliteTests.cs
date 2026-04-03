using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetSprintTrendMetricsQueryHandlerSqliteTests
{
    private SqliteConnection _connection = null!;
    private PoToolDbContext _context = null!;

    [TestInitialize]
    public async Task SetupAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new PoToolDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [TestMethod]
    public async Task Handle_WithSqlite_ExecutesWithoutTranslationFailure()
    {
        var owner = new ProfileEntity { Name = "PO 1" };
        _context.Profiles.Add(owner);
        await _context.SaveChangesAsync();

        var team = new TeamEntity { Name = "Team 1", TeamAreaPath = "\\Project\\Team 1" };
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        PersistenceTestGraph.EnsureProject(_context);
        var sprint = new SprintEntity
        {
            TeamId = team.Id,
            Name = "Sprint 1",
            Path = "\\Project\\Sprint 1",
            StartUtc = new DateTimeOffset(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            StartDateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTimeOffset(new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc)),
            EndDateUtc = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc),
            LastSyncedUtc = DateTimeOffset.UtcNow,
            LastSyncedDateUtc = DateTime.UtcNow
        };
        var product = new ProductEntity
        {
            Name = "Product A",
            ProductOwnerId = owner.Id,
            ProjectId = PersistenceTestGraph.DefaultProjectId,
            BacklogRoots = [new ProductBacklogRootEntity { WorkItemTfsId = 100 }]
        };

        _context.Sprints.Add(sprint);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var handler = new GetSprintTrendMetricsQueryHandler(
            _context,
            new StubSprintTrendProjectionService(
                [
                    new SprintMetricsProjectionEntity
                    {
                        SprintId = sprint.Id,
                        ProductId = product.Id,
                        PlannedCount = 3,
                        WorkedCount = 2,
                        CompletedPbiCount = 1,
                        LastComputedAt = DateTimeOffset.UtcNow,
                        IncludedUpToRevisionId = 1
                    }
                ]),
            new ProductAggregationService(),
            new PlanningQualityService(),
            new SnapshotComparisonService(),
            new InsightService(),
            NullLogger<GetSprintTrendMetricsQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetSprintTrendMetricsQuery(owner.Id, [sprint.Id], Recompute: false),
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Metrics);
        Assert.HasCount(1, result.Metrics);
        Assert.AreEqual("Sprint 1", result.Metrics[0].SprintName);
    }

    private sealed class StubSprintTrendProjectionService : SprintTrendProjectionService
    {
        private readonly IReadOnlyList<SprintMetricsProjectionEntity> _projections;

        public StubSprintTrendProjectionService(IReadOnlyList<SprintMetricsProjectionEntity> projections)
            : base(
                Mock.Of<IServiceScopeFactory>(),
                NullLogger<SprintTrendProjectionService>.Instance,
                stateClassificationService: null,
                new CanonicalStoryPointResolutionService(),
                new HierarchyRollupService(new CanonicalStoryPointResolutionService()),
                new DeliveryProgressRollupService(
                    new CanonicalStoryPointResolutionService(),
                    new HierarchyRollupService(new CanonicalStoryPointResolutionService())),
                new SprintCommitmentService(),
                new SprintCompletionService(),
                new SprintSpilloverService(),
                new SprintDeliveryProjectionService(
                    new CanonicalStoryPointResolutionService(),
                    new HierarchyRollupService(new CanonicalStoryPointResolutionService()),
                    new DeliveryProgressRollupService(
                        new CanonicalStoryPointResolutionService(),
                        new HierarchyRollupService(new CanonicalStoryPointResolutionService())),
                    new SprintCompletionService(),
                    new SprintSpilloverService()))
        {
            _projections = projections;
        }

        public override Task<IReadOnlyList<SprintMetricsProjectionEntity>> GetProjectionsAsync(
            int productOwnerId,
            IEnumerable<int> sprintIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_projections);

        public override Task<IReadOnlyList<SprintMetricsProjectionEntity>> ComputeProjectionsAsync(
            int productOwnerId,
            IEnumerable<int> sprintIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_projections);

        public override Task<IReadOnlyList<FeatureProgressDto>> ComputeFeatureProgressAsync(
            int productOwnerId,
            FeatureProgressMode progressMode,
            DateTime? sprintStartUtc = null,
            DateTime? sprintEndUtc = null,
            CancellationToken cancellationToken = default,
            int? sprintId = null)
            => Task.FromResult<IReadOnlyList<FeatureProgressDto>>(Array.Empty<FeatureProgressDto>());

        public override Task<IReadOnlyList<EpicProgressDto>> ComputeEpicProgressAsync(
            int productOwnerId,
            IReadOnlyList<FeatureProgressDto>? featureProgress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EpicProgressDto>>(Array.Empty<EpicProgressDto>());
    }
}
