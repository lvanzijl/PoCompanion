using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.EffortPlanning;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Portfolio;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.WorkItems;
using PoTool.Core.Domain.WorkItems;
using PoTool.Shared.Settings;
using PoTool.Tests.Unit.TestSupport;
using DomainStateClassification = PoTool.Core.Domain.Models.StateClassification;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class CdcReplayFixtureValidationTests
{
    private static readonly IEffortDistributionService DistributionService = new EffortDistributionService();
    private static readonly IEffortEstimationQualityService QualityService = new EffortEstimationQualityService();
    private static readonly IEffortEstimationSuggestionService SuggestionService = new EffortEstimationSuggestionService();
    private static readonly ICompletionForecastService CompletionForecastService = new CompletionForecastService();

    [TestMethod]
    public void RecordedRevisionSnapshots_ReplayLocallyWithoutLiveTfs()
    {
        var firstReplay = LoadRecordedRevisionFieldChanges();
        var secondReplay = LoadRecordedRevisionFieldChanges();

        CollectionAssert.AreEqual(firstReplay.ToList(), secondReplay.ToList());
        Assert.HasCount(3, firstReplay);
        CollectionAssert.AreEqual(
            new[] { "System.State", "System.State", "System.Title" },
            firstReplay.Select(change => change.FieldRefName).ToList());
        CollectionAssert.AreEqual(
            new[] { "Active", "Done", "Cross-page delta sample updated" },
            firstReplay.Select(change => change.NewValue).ToList());
    }

    [TestMethod]
    public async Task SprintFacts_ReplayFixture_ReconstructsCommitmentCompletionAndSpillover()
    {
        await using var fixture = await ReplayFixtureContext.CreateAsync();
        var inputs = await fixture.LoadHistoricalInputsAsync();
        var sprint1 = inputs.SprintsByName["Sprint 1"];

        var firstReplay = fixture.CreateSprintFactService().BuildSprintFactResult(
            sprint1,
            inputs.CanonicalWorkItemsById,
            inputs.WorkItemSnapshotsById,
            inputs.IterationEventsByWorkItem,
            inputs.StateEventsByWorkItem,
            inputs.StateLookup,
            fixture.Sprint2Path);
        var secondReplay = fixture.CreateSprintFactService().BuildSprintFactResult(
            sprint1,
            inputs.CanonicalWorkItemsById,
            inputs.WorkItemSnapshotsById,
            inputs.IterationEventsByWorkItem,
            inputs.StateEventsByWorkItem,
            inputs.StateLookup,
            fixture.Sprint2Path);

        Assert.AreEqual(firstReplay, secondReplay);
        Assert.AreEqual(15d, firstReplay.CommittedStoryPoints, 0.001d);
        Assert.AreEqual(3d, firstReplay.AddedStoryPoints, 0.001d);
        Assert.AreEqual(2d, firstReplay.RemovedStoryPoints, 0.001d);
        Assert.AreEqual(8d, firstReplay.DeliveredStoryPoints, 0.001d);
        Assert.AreEqual(3d, firstReplay.DeliveredFromAddedStoryPoints, 0.001d);
        Assert.AreEqual(8d, firstReplay.SpilloverStoryPoints, 0.001d);
        Assert.AreEqual(8d, firstReplay.RemainingStoryPoints, 0.001d);
        Assert.IsGreaterThanOrEqualTo(0d, firstReplay.RemainingStoryPoints, "Remaining scope must stay non-negative.");
        Assert.AreEqual(
            firstReplay.CommittedStoryPoints + firstReplay.AddedStoryPoints - firstReplay.RemovedStoryPoints - firstReplay.DeliveredStoryPoints,
            firstReplay.RemainingStoryPoints,
            0.001d,
            "Remaining scope must match the corrected SprintFacts formula.");
        Assert.IsGreaterThanOrEqualTo(firstReplay.DeliveredStoryPoints, firstReplay.CommittedStoryPoints, "Delivered committed scope must not exceed committed scope.");
        Assert.IsGreaterThanOrEqualTo(firstReplay.DeliveredFromAddedStoryPoints, firstReplay.AddedStoryPoints, "Delivered added scope must stay within added scope.");
        Assert.IsGreaterThanOrEqualTo(firstReplay.SpilloverStoryPoints, firstReplay.RemainingStoryPoints, "Spillover must remain bounded by remaining scope.");
    }

    [TestMethod]
    public async Task PortfolioFlow_ReplayFixture_ReconstructsStockInflowAndThroughputDeterministically()
    {
        await using var fixture = await ReplayFixtureContext.CreateAsync();
        var service = fixture.CreatePortfolioFlowProjectionService();

        var firstReplay = (await service.ComputeProjectionsAsync(fixture.ProductOwnerId, fixture.SprintIds))
            .OrderBy(projection => projection.SprintId)
            .Select(MapPortfolioFlowSnapshot)
            .ToList();
        var secondReplay = (await service.ComputeProjectionsAsync(fixture.ProductOwnerId, fixture.SprintIds))
            .OrderBy(projection => projection.SprintId)
            .Select(MapPortfolioFlowSnapshot)
            .ToList();

        CollectionAssert.AreEqual(firstReplay, secondReplay);
        Assert.HasCount(2, firstReplay);

        var sprint1 = firstReplay[0];
        var sprint2 = firstReplay[1];

        Assert.AreEqual(18d, sprint1.StockStoryPoints, 0.001d);
        Assert.AreEqual(3d, sprint1.InflowStoryPoints, 0.001d);
        Assert.AreEqual(8d, sprint1.ThroughputStoryPoints, 0.001d);
        Assert.IsGreaterThanOrEqualTo(0d, sprint1.RemainingScopeStoryPoints, "Portfolio remaining scope must stay non-negative.");

        Assert.AreEqual(31d, sprint2.StockStoryPoints, 0.001d);
        Assert.AreEqual(13d, sprint2.InflowStoryPoints, 0.001d);
        Assert.AreEqual(13d, sprint2.ThroughputStoryPoints, 0.001d);
        Assert.IsGreaterThanOrEqualTo(0d, sprint2.RemainingScopeStoryPoints, "Portfolio remaining scope must stay non-negative.");

        var persisted = (await fixture.LoadPersistedPortfolioFlowSnapshotsAsync()).ToList();
        CollectionAssert.AreEqual(firstReplay, persisted);
    }

    [TestMethod]
    public async Task DeliveryTrends_ReplayFixture_RemainsDeterministicAcrossRebuilds()
    {
        await using var fixture = await ReplayFixtureContext.CreateAsync();
        var service = fixture.CreateSprintTrendProjectionService();

        var firstReplay = (await service.ComputeProjectionsAsync(fixture.ProductOwnerId, fixture.SprintIds))
            .OrderBy(projection => projection.SprintId)
            .Select(MapSprintProjectionSnapshot)
            .ToList();
        var secondReplay = (await service.ComputeProjectionsAsync(fixture.ProductOwnerId, fixture.SprintIds))
            .OrderBy(projection => projection.SprintId)
            .Select(MapSprintProjectionSnapshot)
            .ToList();
        var thirdReplay = (await service.ComputeProjectionsAsync(fixture.ProductOwnerId, fixture.SprintIds))
            .OrderBy(projection => projection.SprintId)
            .Select(MapSprintProjectionSnapshot)
            .ToList();

        CollectionAssert.AreEqual(firstReplay, secondReplay);
        CollectionAssert.AreEqual(firstReplay, thirdReplay);
        Assert.HasCount(2, firstReplay);
        Assert.AreEqual(15d, firstReplay[0].PlannedStoryPoints, 0.001d);
        Assert.AreEqual(8d, firstReplay[0].CompletedPbiStoryPoints, 0.001d);
        Assert.AreEqual(8d, firstReplay[0].SpilloverStoryPoints, 0.001d);
        Assert.AreEqual(21d, firstReplay[1].PlannedStoryPoints, 0.001d);
        Assert.AreEqual(13d, firstReplay[1].CompletedPbiStoryPoints, 0.001d);
        Assert.AreEqual(0d, firstReplay[1].SpilloverStoryPoints, 0.001d);

        var persisted = (await fixture.LoadPersistedSprintProjectionSnapshotsAsync()).ToList();
        CollectionAssert.AreEqual(firstReplay, persisted);
    }

    [TestMethod]
    public async Task Forecasting_ReplayFixture_RemainsStableOverPersistedSprintHistory()
    {
        await using var fixture = await ReplayFixtureContext.CreateAsync();
        var trendService = fixture.CreateSprintTrendProjectionService();

        await trendService.ComputeProjectionsAsync(fixture.ProductOwnerId, fixture.SprintIds);

        var history = await fixture.LoadHistoricalVelocitySamplesAsync();
        var totalScopeStoryPoints = await fixture.LoadCurrentProductScopeStoryPointsAsync();
        var completedScopeStoryPoints = history.Sum(sample => sample.CompletedStoryPoints);

        var firstReplay = CompletionForecastService.Forecast(totalScopeStoryPoints, completedScopeStoryPoints, history);
        var secondReplay = CompletionForecastService.Forecast(totalScopeStoryPoints, completedScopeStoryPoints, history);

        Assert.AreEqual(firstReplay.TotalScopeStoryPoints, secondReplay.TotalScopeStoryPoints, 0.001d);
        Assert.AreEqual(firstReplay.CompletedScopeStoryPoints, secondReplay.CompletedScopeStoryPoints, 0.001d);
        Assert.AreEqual(firstReplay.RemainingScopeStoryPoints, secondReplay.RemainingScopeStoryPoints, 0.001d);
        Assert.AreEqual(firstReplay.EstimatedVelocity, secondReplay.EstimatedVelocity, 0.001d);
        Assert.AreEqual(firstReplay.SprintsRemaining, secondReplay.SprintsRemaining);
        Assert.AreEqual(firstReplay.EstimatedCompletionDate, secondReplay.EstimatedCompletionDate);
        Assert.AreEqual(firstReplay.Confidence, secondReplay.Confidence);
        CollectionAssert.AreEqual(firstReplay.Projections.ToList(), secondReplay.Projections.ToList());

        Assert.AreEqual(31d, firstReplay.TotalScopeStoryPoints, 0.001d);
        Assert.AreEqual(21d, firstReplay.CompletedScopeStoryPoints, 0.001d);
        Assert.AreEqual(10d, firstReplay.RemainingScopeStoryPoints, 0.001d);
        Assert.IsGreaterThanOrEqualTo(0d, firstReplay.RemainingScopeStoryPoints, "Forecast remaining scope must stay non-negative.");
        Assert.IsGreaterThan(0d, firstReplay.EstimatedVelocity, "Historical replay should produce usable velocity.");
    }

    [TestMethod]
    public async Task EffortPlanning_ReplayFixture_RemainsConsistentOnRealisticWorkItemMixes()
    {
        await using var fixture = await ReplayFixtureContext.CreateAsync();
        var sprintFacts = fixture.CreateSprintFactService();
        var inputs = await fixture.LoadHistoricalInputsAsync();
        var sprint1 = sprintFacts.BuildSprintFactResult(
            inputs.SprintsByName["Sprint 1"],
            inputs.CanonicalWorkItemsById,
            inputs.WorkItemSnapshotsById,
            inputs.IterationEventsByWorkItem,
            inputs.StateEventsByWorkItem,
            inputs.StateLookup,
            fixture.Sprint2Path);
        var planningItems = await fixture.LoadEffortPlanningWorkItemsAsync();

        var firstDistribution = DistributionService.Analyze(planningItems, maxIterations: 10, defaultCapacityPerIteration: 40);
        var secondDistribution = DistributionService.Analyze(planningItems, maxIterations: 10, defaultCapacityPerIteration: 40);
        var firstQuality = QualityService.Analyze(planningItems, maxIterations: 10);
        var secondQuality = QualityService.Analyze(planningItems, maxIterations: 10);
        var suggestionTarget = new EffortPlanningWorkItem(9999, "Task", "Implement replay fixture import", "Area\\Replay", fixture.Sprint2Path, "New", DateTimeOffset.UtcNow, null);
        var firstSuggestion = SuggestionService.GenerateSuggestion(suggestionTarget, planningItems, EffortEstimationSettingsDto.Default);
        var secondSuggestion = SuggestionService.GenerateSuggestion(suggestionTarget, planningItems, EffortEstimationSettingsDto.Default);

        Assert.AreEqual(firstDistribution.TotalEffort, secondDistribution.TotalEffort);
        CollectionAssert.AreEqual(firstDistribution.EffortByArea.ToList(), secondDistribution.EffortByArea.ToList());
        CollectionAssert.AreEqual(firstDistribution.EffortByIteration.ToList(), secondDistribution.EffortByIteration.ToList());
        Assert.AreEqual(firstQuality.AverageEstimationAccuracy, secondQuality.AverageEstimationAccuracy, 0.0001d);
        CollectionAssert.AreEqual(firstQuality.QualityByType.ToList(), secondQuality.QualityByType.ToList());
        Assert.AreEqual(firstSuggestion.SuggestedEffort, secondSuggestion.SuggestedEffort);
        CollectionAssert.AreEqual(firstSuggestion.SimilarWorkItems.ToList(), secondSuggestion.SimilarWorkItems.ToList());

        Assert.AreEqual(120, firstDistribution.TotalEffort);
        Assert.AreEqual(120, firstDistribution.EffortByArea.Sum(area => area.TotalEffort));
        Assert.AreEqual(120, firstDistribution.EffortByIteration.Sum(iteration => iteration.TotalEffort));
        Assert.AreEqual(12, firstSuggestion.SuggestedEffort);
        Assert.IsGreaterThan(0, firstSuggestion.HistoricalMatchCount, "Replay history should provide comparable work items.");
        Assert.AreNotEqual((double)firstDistribution.TotalEffort, sprint1.CommittedStoryPoints, "Effort totals must not be mixed with SprintFacts story points.");
        Assert.AreNotEqual((double)firstDistribution.TotalEffort, sprint1.DeliveredStoryPoints, "Effort totals must remain independent from delivered story points.");
    }

    private static IReadOnlyList<FieldChangeEvent> LoadRecordedRevisionFieldChanges()
    {
        var recordedPayloadDirectory = Path.Combine(AppContext.BaseDirectory, "RecordedPayloads");
        var filePaths = new[]
        {
            Path.Combine(recordedPayloadDirectory, "per_item_revisions_page_1.json"),
            Path.Combine(recordedPayloadDirectory, "per_item_revisions_page_2.json")
        };

        var revisions = filePaths
            .SelectMany(LoadRecordedRevisionSnapshots)
            .OrderBy(snapshot => snapshot.WorkItemId)
            .ThenBy(snapshot => snapshot.Revision)
            .ToList();

        var fieldChanges = new List<FieldChangeEvent>();
        var eventId = 1;

        foreach (var revisionGroup in revisions.GroupBy(snapshot => snapshot.WorkItemId))
        {
            var orderedSnapshots = revisionGroup.OrderBy(snapshot => snapshot.Revision).ToList();

            for (var index = 1; index < orderedSnapshots.Count; index++)
            {
                var previous = orderedSnapshots[index - 1];
                var current = orderedSnapshots[index];

                foreach (var fieldName in new[] { "System.State", "System.Title" })
                {
                    previous.Fields.TryGetValue(fieldName, out var oldValue);
                    current.Fields.TryGetValue(fieldName, out var newValue);

                    if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
                    {
                        fieldChanges.Add(new FieldChangeEvent(
                            eventId++,
                            current.WorkItemId,
                            current.Revision,
                            fieldName,
                            current.ChangedDate,
                            current.ChangedDate.UtcDateTime,
                            oldValue,
                            newValue));
                    }
                }
            }
        }

        return fieldChanges;
    }

    private static IReadOnlyList<RecordedRevisionSnapshot> LoadRecordedRevisionSnapshots(string filePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        var revisions = new List<RecordedRevisionSnapshot>();

        foreach (var revisionElement in document.RootElement.GetProperty("value").EnumerateArray())
        {
            var fieldsElement = revisionElement.GetProperty("fields");
            var fields = fieldsElement.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString());
            var changedDate = DateTimeOffset.Parse(fields["System.ChangedDate"]!, CultureInfo.InvariantCulture);

            revisions.Add(new RecordedRevisionSnapshot(
                revisionElement.GetProperty("id").GetInt32(),
                revisionElement.GetProperty("rev").GetInt32(),
                changedDate,
                fields));
        }

        return revisions;
    }

    private static PortfolioFlowSnapshot MapPortfolioFlowSnapshot(PortfolioFlowProjectionEntity projection)
    {
        return new PortfolioFlowSnapshot(
            projection.SprintId,
            projection.ProductId,
            projection.StockStoryPoints,
            projection.RemainingScopeStoryPoints,
            projection.InflowStoryPoints,
            projection.ThroughputStoryPoints,
            projection.CompletionPercent);
    }

    private static SprintProjectionSnapshot MapSprintProjectionSnapshot(SprintMetricsProjectionEntity projection)
    {
        return new SprintProjectionSnapshot(
            projection.SprintId,
            projection.ProductId,
            projection.PlannedStoryPoints,
            projection.CompletedPbiStoryPoints,
            projection.SpilloverStoryPoints,
            projection.ProgressionDelta,
            projection.MissingStoryPointCount,
            projection.UnestimatedDeliveryCount);
    }

    private sealed record RecordedRevisionSnapshot(
        int WorkItemId,
        int Revision,
        DateTimeOffset ChangedDate,
        IReadOnlyDictionary<string, string?> Fields);

    private sealed record PortfolioFlowSnapshot(
        int SprintId,
        int ProductId,
        double StockStoryPoints,
        double RemainingScopeStoryPoints,
        double InflowStoryPoints,
        double ThroughputStoryPoints,
        double? CompletionPercent);

    private sealed record SprintProjectionSnapshot(
        int SprintId,
        int ProductId,
        double PlannedStoryPoints,
        double CompletedPbiStoryPoints,
        double SpilloverStoryPoints,
        double ProgressionDelta,
        int MissingStoryPointCount,
        int UnestimatedDeliveryCount);

    private sealed record HistoricalInputs(
        IReadOnlyDictionary<string, SprintDefinition> SprintsByName,
        IReadOnlyDictionary<int, CanonicalWorkItem> CanonicalWorkItemsById,
        IReadOnlyDictionary<int, WorkItemSnapshot> WorkItemSnapshotsById,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> StateEventsByWorkItem,
        IReadOnlyDictionary<int, IReadOnlyList<FieldChangeEvent>> IterationEventsByWorkItem,
        IReadOnlyDictionary<(string WorkItemType, string StateName), DomainStateClassification> StateLookup);

    private sealed class ReplayFixtureContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;

        private ReplayFixtureContext(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            int productOwnerId,
            int productId,
            int sprint1Id,
            int sprint2Id,
            string sprint1Path,
            string sprint2Path)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            ProductOwnerId = productOwnerId;
            ProductId = productId;
            SprintIds = new[] { sprint1Id, sprint2Id };
            Sprint1Path = sprint1Path;
            Sprint2Path = sprint2Path;
        }

        public int ProductOwnerId { get; }

        public int ProductId { get; }

        public IReadOnlyList<int> SprintIds { get; }

        public string Sprint1Path { get; }

        public string Sprint2Path { get; }

        public static async Task<ReplayFixtureContext> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddDbContext<PoToolDbContext>(options => options.UseSqlite(connection));
            services.AddLogging();
            services.AddSingleton<ISprintCommitmentService, SprintCommitmentService>();
            services.AddSingleton<ISprintScopeChangeService, SprintScopeChangeService>();
            services.AddSingleton<ISprintCompletionService, SprintCompletionService>();
            services.AddSingleton<ISprintSpilloverService, SprintSpilloverService>();
            services.AddSingleton<ICanonicalStoryPointResolutionService, CanonicalStoryPointResolutionService>();
            services.AddSingleton<IHierarchyRollupService, HierarchyRollupService>();
            services.AddSingleton<IDeliveryProgressRollupService, DeliveryProgressRollupService>();
            services.AddSingleton<IPortfolioFlowSummaryService, PortfolioFlowSummaryService>();
            services.AddSingleton<ISprintDeliveryProjectionService, SprintDeliveryProjectionService>();

            var serviceProvider = services.BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
                await context.Database.EnsureCreatedAsync();
            }

            int productOwnerId;
            int productId;
            int sprint1Id;
            int sprint2Id;
            const string sprint1Path = "\\Project\\Sprint 1";
            const string sprint2Path = "\\Project\\Sprint 2";

            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

                var productOwner = new ProfileEntity { Name = "Replay PO" };
                context.Profiles.Add(productOwner);
                await context.SaveChangesAsync();
                productOwnerId = productOwner.Id;

                var team = new TeamEntity { Name = "Replay Team", TeamAreaPath = "\\Project\\Replay" };
                context.Teams.Add(team);

                var product = new ProductEntity
                {
                    ProductOwnerId = productOwner.Id,
                    ProjectId = PersistenceTestGraph.DefaultProjectId,
                    Name = "Replay Product",
                    BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 300 } }
                };
                PersistenceTestGraph.EnsureProject(context);
                context.Products.Add(product);
                await context.SaveChangesAsync();
                productId = product.Id;

                var sprint1 = new SprintEntity
                {
                    TeamId = team.Id,
                    Name = "Sprint 1",
                    Path = sprint1Path,
                    StartUtc = new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                    StartDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndUtc = new DateTimeOffset(new DateTime(2026, 1, 14, 23, 59, 59, DateTimeKind.Utc)),
                    EndDateUtc = new DateTime(2026, 1, 14, 23, 59, 59, DateTimeKind.Utc),
                    LastSyncedUtc = DateTimeOffset.UtcNow,
                    LastSyncedDateUtc = DateTime.UtcNow
                };
                var sprint2 = new SprintEntity
                {
                    TeamId = team.Id,
                    Name = "Sprint 2",
                    Path = sprint2Path,
                    StartUtc = new DateTimeOffset(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)),
                    StartDateUtc = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                    EndUtc = new DateTimeOffset(new DateTime(2026, 1, 28, 23, 59, 59, DateTimeKind.Utc)),
                    EndDateUtc = new DateTime(2026, 1, 28, 23, 59, 59, DateTimeKind.Utc),
                    LastSyncedUtc = DateTimeOffset.UtcNow,
                    LastSyncedDateUtc = DateTime.UtcNow
                };

                context.Sprints.AddRange(sprint1, sprint2);
                await context.SaveChangesAsync();
                sprint1Id = sprint1.Id;
                sprint2Id = sprint2.Id;

                var retrievedAt = new DateTimeOffset(new DateTime(2026, 1, 28, 12, 0, 0, DateTimeKind.Utc));
                context.WorkItems.AddRange(
                    CreateWorkItem(300, WorkItemType.Epic, "Replay Epic", "Active", "\\Project\\Backlog", null, 55, null, retrievedAt),
                    CreateWorkItem(200, WorkItemType.Feature, "Replay Feature", "Active", "\\Project\\Backlog", 300, 21, null, retrievedAt),
                    CreateWorkItem(1001, WorkItemType.Pbi, "Committed PBI", "Done", sprint1Path, 200, 13, 5, retrievedAt),
                    CreateWorkItem(1002, WorkItemType.Pbi, "Added PBI", "Done", sprint1Path, 200, 8, 3, retrievedAt),
                    CreateWorkItem(1003, WorkItemType.Pbi, "Removed PBI", "Active", "\\Project\\Backlog", 200, 5, 2, retrievedAt),
                    CreateWorkItem(1004, WorkItemType.Pbi, "Spillover PBI", "Active", sprint2Path, 200, 20, 8, retrievedAt),
                    CreateWorkItem(1005, WorkItemType.Pbi, "Sprint 2 PBI", "Done", sprint2Path, 200, 34, 13, retrievedAt),
                    CreateWorkItem(1101, WorkItemType.Task, "Sprint 2 Task", "Done", sprint2Path, 1005, 12, null, retrievedAt),
                    CreateWorkItem(1102, WorkItemType.Bug, "Carry bug", "Done", sprint1Path, 200, 7, null, retrievedAt));

                context.ResolvedWorkItems.AddRange(
                    CreateResolvedWorkItem(300, WorkItemType.Epic, productId, null, null, null),
                    CreateResolvedWorkItem(200, WorkItemType.Feature, productId, null, null, 300),
                    CreateResolvedWorkItem(1001, WorkItemType.Pbi, productId, sprint1Id, 200, 300),
                    CreateResolvedWorkItem(1002, WorkItemType.Pbi, productId, sprint1Id, 200, 300),
                    CreateResolvedWorkItem(1003, WorkItemType.Pbi, productId, null, 200, 300),
                    CreateResolvedWorkItem(1004, WorkItemType.Pbi, productId, sprint2Id, 200, 300),
                    CreateResolvedWorkItem(1005, WorkItemType.Pbi, productId, sprint2Id, 200, 300),
                    CreateResolvedWorkItem(1101, WorkItemType.Task, productId, sprint2Id, 200, 300),
                    CreateResolvedWorkItem(1102, WorkItemType.Bug, productId, sprint1Id, 200, 300));

                context.ActivityEventLedgerEntries.AddRange(
                    CreateActivityEvent(productOwnerId, 1001, 1, "System.State", new DateTimeOffset(new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc)), "Active", "Done"),
                    CreateActivityEvent(productOwnerId, 1002, 2, "System.IterationPath", new DateTimeOffset(new DateTime(2026, 1, 4, 9, 0, 0, DateTimeKind.Utc)), "\\Project\\Backlog", sprint1Path),
                    CreateActivityEvent(productOwnerId, 1002, 3, PortfolioEntryLookup.ResolvedProductIdFieldRefName, new DateTimeOffset(new DateTime(2026, 1, 4, 9, 5, 0, DateTimeKind.Utc)), null, productId.ToString()),
                    CreateActivityEvent(productOwnerId, 1002, 4, "System.State", new DateTimeOffset(new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc)), "Active", "Done"),
                    CreateActivityEvent(productOwnerId, 1003, 5, "System.IterationPath", new DateTimeOffset(new DateTime(2026, 1, 6, 12, 0, 0, DateTimeKind.Utc)), sprint1Path, "\\Project\\Backlog"),
                    CreateActivityEvent(productOwnerId, 1004, 6, "System.IterationPath", new DateTimeOffset(new DateTime(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc)), sprint1Path, sprint2Path),
                    CreateActivityEvent(productOwnerId, 1005, 7, "System.IterationPath", new DateTimeOffset(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)), "\\Project\\Backlog", sprint2Path),
                    CreateActivityEvent(productOwnerId, 1005, 8, PortfolioEntryLookup.ResolvedProductIdFieldRefName, new DateTimeOffset(new DateTime(2026, 1, 15, 10, 5, 0, DateTimeKind.Utc)), null, productId.ToString()),
                    CreateActivityEvent(productOwnerId, 1005, 9, "System.State", new DateTimeOffset(new DateTime(2026, 1, 20, 12, 0, 0, DateTimeKind.Utc)), "Active", "Done"));

                await context.SaveChangesAsync();
            }

            return new ReplayFixtureContext(connection, serviceProvider, productOwnerId, productId, sprint1Id, sprint2Id, sprint1Path, sprint2Path);
        }

        public ISprintFactService CreateSprintFactService()
        {
            return new SprintFactService(
                new SprintCommitmentService(),
                new SprintScopeChangeService(),
                new SprintCompletionService(),
                new SprintSpilloverService(),
                new CanonicalStoryPointResolutionService());
        }

        public PortfolioFlowProjectionService CreatePortfolioFlowProjectionService()
        {
            return new PortfolioFlowProjectionService(
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<PortfolioFlowProjectionService>.Instance,
                stateClassificationService: null,
                _serviceProvider.GetRequiredService<ISprintCompletionService>(),
                _serviceProvider.GetRequiredService<ICanonicalStoryPointResolutionService>());
        }

        public SprintTrendProjectionService CreateSprintTrendProjectionService()
        {
            return new SprintTrendProjectionService(
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<SprintTrendProjectionService>.Instance,
                stateClassificationService: null,
                _serviceProvider.GetRequiredService<ICanonicalStoryPointResolutionService>(),
                _serviceProvider.GetRequiredService<IHierarchyRollupService>(),
                _serviceProvider.GetRequiredService<IDeliveryProgressRollupService>(),
                _serviceProvider.GetRequiredService<ISprintCommitmentService>(),
                _serviceProvider.GetRequiredService<ISprintCompletionService>(),
                _serviceProvider.GetRequiredService<ISprintSpilloverService>(),
                _serviceProvider.GetRequiredService<ISprintDeliveryProjectionService>());
        }

        public async Task<HistoricalInputs> LoadHistoricalInputsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            var sprints = await context.Sprints
                .AsNoTracking()
                .Where(sprint => SprintIds.Contains(sprint.Id))
                .OrderBy(sprint => sprint.Id)
                .ToListAsync();
            var workItems = await context.WorkItems
                .AsNoTracking()
                .Where(workItem => workItem.Type == WorkItemType.Pbi || workItem.Type == WorkItemType.Bug)
                .ToListAsync();
            var activityEvents = await context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(activityEvent => activityEvent.ProductOwnerId == ProductOwnerId)
                .OrderBy(activityEvent => activityEvent.WorkItemId)
                .ThenBy(activityEvent => activityEvent.EventTimestampUtc)
                .ThenBy(activityEvent => activityEvent.UpdateId)
                .ToListAsync();

            var fieldChanges = activityEvents.ToFieldChangeEvents();

            return new HistoricalInputs(
                sprints.ToDictionary(sprint => sprint.Name, sprint => sprint.ToDefinition()),
                workItems.ToDictionary(
                    workItem => workItem.TfsId,
                    workItem => workItem.ToCanonicalWorkItem()),
                workItems.ToSnapshotDictionary(),
                fieldChanges
                    .Where(change => string.Equals(change.FieldRefName, "System.State", StringComparison.OrdinalIgnoreCase))
                    .GroupByWorkItemId(),
                fieldChanges
                    .Where(change => string.Equals(change.FieldRefName, "System.IterationPath", StringComparison.OrdinalIgnoreCase))
                    .GroupByWorkItemId(),
                new Dictionary<(string WorkItemType, string StateName), DomainStateClassification>
                {
                    [(CanonicalWorkItemTypes.Pbi, "Active")] = DomainStateClassification.InProgress,
                    [(CanonicalWorkItemTypes.Pbi, "Done")] = DomainStateClassification.Done,
                    [(WorkItemType.Bug, "Done")] = DomainStateClassification.Done
                });
        }

        public async Task<IReadOnlyList<PortfolioFlowSnapshot>> LoadPersistedPortfolioFlowSnapshotsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            var projections = await context.PortfolioFlowProjections
                .AsNoTracking()
                .Where(projection => projection.ProductId == ProductId && SprintIds.Contains(projection.SprintId))
                .OrderBy(projection => projection.SprintId)
                .ToListAsync();

            return projections.Select(MapPortfolioFlowSnapshot).ToList();
        }

        public async Task<IReadOnlyList<SprintProjectionSnapshot>> LoadPersistedSprintProjectionSnapshotsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            var projections = await context.SprintMetricsProjections
                .AsNoTracking()
                .Where(projection => projection.ProductId == ProductId && SprintIds.Contains(projection.SprintId))
                .OrderBy(projection => projection.SprintId)
                .ToListAsync();

            return projections.Select(MapSprintProjectionSnapshot).ToList();
        }

        public async Task<IReadOnlyList<HistoricalVelocitySample>> LoadHistoricalVelocitySamplesAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            var sprintHistory = await context.SprintMetricsProjections
                .AsNoTracking()
                .Where(projection => projection.ProductId == ProductId && SprintIds.Contains(projection.SprintId))
                .Join(
                    context.Sprints.AsNoTracking(),
                    projection => projection.SprintId,
                    sprint => sprint.Id,
                    (projection, sprint) => new
                    {
                        sprint.Name,
                        sprint.EndDateUtc,
                        projection.CompletedPbiStoryPoints
                    })
                .OrderBy(entry => entry.EndDateUtc)
                .ToListAsync();

            return sprintHistory
                .Select(entry => new HistoricalVelocitySample(
                    entry.Name,
                    entry.EndDateUtc is null
                        ? null
                        : new DateTimeOffset(DateTime.SpecifyKind(entry.EndDateUtc.Value, DateTimeKind.Utc), TimeSpan.Zero),
                    entry.CompletedPbiStoryPoints))
                .ToList();
        }

        public async Task<double> LoadCurrentProductScopeStoryPointsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            return await context.WorkItems
                .AsNoTracking()
                .Join(
                    context.ResolvedWorkItems.AsNoTracking().Where(resolved => resolved.ResolvedProductId == ProductId),
                    workItem => workItem.TfsId,
                    resolved => resolved.WorkItemId,
                    (workItem, _) => workItem)
                .Where(workItem => workItem.Type == WorkItemType.Pbi)
                .Select(workItem => (double)(workItem.StoryPoints ?? 0))
                .SumAsync();
        }

        public async Task<IReadOnlyList<EffortPlanningWorkItem>> LoadEffortPlanningWorkItemsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            return await context.WorkItems
                .AsNoTracking()
                .Where(workItem => new[] { 200, 1001, 1002, 1003, 1004, 1005, 1101, 1102 }.Contains(workItem.TfsId))
                .OrderBy(workItem => workItem.TfsId)
                .Select(workItem => new EffortPlanningWorkItem(
                    workItem.TfsId,
                    workItem.Type,
                    workItem.Title,
                    workItem.AreaPath,
                    workItem.IterationPath,
                    workItem.State,
                    workItem.RetrievedAt,
                    workItem.Effort))
                .ToListAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
            await _serviceProvider.DisposeAsync();
        }

        private static WorkItemEntity CreateWorkItem(
            int workItemId,
            string workItemType,
            string title,
            string state,
            string iterationPath,
            int? parentTfsId,
            int? effort,
            int? storyPoints,
            DateTimeOffset retrievedAt)
        {
            return new WorkItemEntity
            {
                TfsId = workItemId,
                ParentTfsId = parentTfsId,
                Type = workItemType,
                Title = title,
                AreaPath = "Area\\Replay",
                IterationPath = iterationPath,
                State = state,
                Effort = effort,
                StoryPoints = storyPoints,
                RetrievedAt = retrievedAt,
                TfsChangedDate = retrievedAt,
                TfsChangedDateUtc = retrievedAt.UtcDateTime,
                TfsRevision = 1,
                CreatedDate = retrievedAt.AddDays(-30)
            };
        }

        private static ResolvedWorkItemEntity CreateResolvedWorkItem(
            int workItemId,
            string workItemType,
            int productId,
            int? sprintId,
            int? featureId,
            int? epicId)
        {
            return new ResolvedWorkItemEntity
            {
                WorkItemId = workItemId,
                WorkItemType = workItemType,
                ResolvedProductId = productId,
                ResolvedSprintId = sprintId,
                ResolvedFeatureId = featureId,
                ResolvedEpicId = epicId,
                ResolutionStatus = ResolutionStatus.Resolved,
                ResolvedAtRevision = 1
            };
        }

        private static ActivityEventLedgerEntryEntity CreateActivityEvent(
            int productOwnerId,
            int workItemId,
            int updateId,
            string fieldRefName,
            DateTimeOffset timestamp,
            string? oldValue,
            string? newValue)
        {
            return new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = productOwnerId,
                WorkItemId = workItemId,
                UpdateId = updateId,
                FieldRefName = fieldRefName,
                EventTimestamp = timestamp,
                EventTimestampUtc = timestamp.UtcDateTime,
                OldValue = oldValue,
                NewValue = newValue
            };
        }
    }
}
