using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Domain.Cdc.ExecutionRealityCheck;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.Estimation;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ExecutionRealityCheckCdcSliceServiceTests
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public async Task SetupAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<PoToolDbContext>(options => options.UseSqlite(_connection));
        services.AddLogging();
        services.AddSingleton<ISprintCommitmentService, SprintCommitmentService>();
        services.AddSingleton<ISprintScopeChangeService, SprintScopeChangeService>();
        services.AddSingleton<ISprintCompletionService, SprintCompletionService>();
        services.AddSingleton<ISprintSpilloverService, SprintSpilloverService>();
        services.AddSingleton<ICanonicalStoryPointResolutionService, CanonicalStoryPointResolutionService>();
        services.AddSingleton<ISprintFactService, SprintFactService>();
        services.AddSingleton<PoTool.Core.Domain.Cdc.Sprints.SprintExecutionMetricsCalculator>();
        services.AddSingleton<ISprintExecutionMetricsCalculator>(sp =>
            sp.GetRequiredService<PoTool.Core.Domain.Cdc.Sprints.SprintExecutionMetricsCalculator>());
        services.AddSingleton<IExecutionRealityCheckCdcSliceProjector, ExecutionRealityCheckCdcSliceProjector>();
        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _connection.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [TestMethod]
    public async Task BuildAsync_SelectsLatestEightCompletedSprintsInTeamOrder()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var scenario = await SeedScenarioAsync(context, totalSprints: 10, completedSprintCount: 9, addFactsForWindow: true);
        var service = CreateService(context);

        var result = await service.BuildAsync(scenario.ProductOwnerId, scenario.AnchorSprintId, [scenario.ProductId]);

        Assert.IsTrue(result.HasSufficientEvidence);
        Assert.IsNotNull(result.Slice);
        CollectionAssert.AreEqual(
            scenario.WindowSprintIds.ToList(),
            result.Slice.WindowRows.Select(row => row.SprintId).ToList());
        Assert.IsTrue(result.Slice.WindowRows.All(row => row.HasContinuousOrdering));
        Assert.IsTrue(result.Slice.WindowRows.All(row => row.HasAuthoritativeDenominator));
    }

    [TestMethod]
    public async Task BuildAsync_ComputesCanonicalSeriesBaselinesAndAnomalyInputs()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var scenario = await SeedScenarioAsync(context, totalSprints: 10, completedSprintCount: 9, addFactsForWindow: true);
        var service = CreateService(context);

        var result = await service.BuildAsync(scenario.ProductOwnerId, scenario.AnchorSprintId, [scenario.ProductId]);

        Assert.IsTrue(result.HasSufficientEvidence);
        Assert.IsNotNull(result.Slice);
        Assert.HasCount(8, result.Slice.WindowRows);
        Assert.IsTrue(result.Slice.WindowRows.All(row => Math.Abs(row.CommitmentCompletion - 0.8d) < 0.0001d));
        Assert.IsTrue(result.Slice.WindowRows.All(row => Math.Abs(row.SpilloverRate - 0.2d) < 0.0001d));

        var completionBaseline = result.Slice.Baselines.Single(baseline =>
            baseline.MetricKey == ExecutionRealityCheckCdcKeys.CommitmentCompletionMetricKey);
        var spilloverBaseline = result.Slice.Baselines.Single(baseline =>
            baseline.MetricKey == ExecutionRealityCheckCdcKeys.SpilloverRateMetricKey);

        Assert.AreEqual(0.8d, completionBaseline.Median, 0.0001d);
        Assert.AreEqual(0.2d, spilloverBaseline.Median, 0.0001d);
        Assert.AreEqual(scenario.WindowSprintIds[^1], result.Slice.AnomalyInputs[0].CurrentSprintId);
        CollectionAssert.AreEquivalent(
            new[]
            {
                ExecutionRealityCheckCdcKeys.CompletionBelowTypicalAnomalyKey,
                ExecutionRealityCheckCdcKeys.CompletionVariabilityAnomalyKey,
                ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey
            },
            result.Slice.AnomalyInputs.Select(input => input.AnomalyKey).ToList());
    }

    [TestMethod]
    public async Task BuildAsync_ReturnsInsufficientEvidence_WhenFewerThanEightCompletedSprintsExist()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var scenario = await SeedScenarioAsync(context, totalSprints: 8, completedSprintCount: 7, addFactsForWindow: true);
        var service = CreateService(context);

        var result = await service.BuildAsync(scenario.ProductOwnerId, scenario.AnchorSprintId, [scenario.ProductId]);

        Assert.IsFalse(result.HasSufficientEvidence);
        Assert.IsNull(result.Slice);
    }

    [TestMethod]
    public async Task BuildAsync_ReturnsInsufficientEvidence_WhenAnyWindowSprintLacksAuthoritativeDenominator()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var scenario = await SeedScenarioAsync(
            context,
            totalSprints: 10,
            completedSprintCount: 9,
            addFactsForWindow: true,
            missingFactSprintNumber: 5);
        var service = CreateService(context);

        var result = await service.BuildAsync(scenario.ProductOwnerId, scenario.AnchorSprintId, [scenario.ProductId]);

        Assert.IsFalse(result.HasSufficientEvidence);
        Assert.IsNull(result.Slice);
    }

    private async Task<ScenarioContext> SeedScenarioAsync(
        PoToolDbContext context,
        int totalSprints,
        int completedSprintCount,
        bool addFactsForWindow,
        int? missingFactSprintNumber = null)
    {
        var productOwner = new ProfileEntity
        {
            Name = "PO"
        };
        context.Profiles.Add(productOwner);
        await context.SaveChangesAsync();

        var team = new TeamEntity
        {
            Name = "Team 1",
            TeamAreaPath = "\\Project\\Team 1"
        };
        context.Teams.Add(team);

        PersistenceTestGraph.EnsureProject(context);
        var product = new ProductEntity
        {
            ProductOwnerId = productOwner.Id,
            ProjectId = PersistenceTestGraph.DefaultProjectId,
            Name = "Product 1"
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        context.ProductTeamLinks.Add(new ProductTeamLinkEntity
        {
            ProductId = product.Id,
            TeamId = team.Id
        });

        var baseStartUtc = DateTime.UtcNow.Date.AddDays(-(completedSprintCount * 14));
        var sprints = new List<SprintEntity>();

        for (var sprintNumber = 1; sprintNumber <= totalSprints; sprintNumber++)
        {
            var startUtc = baseStartUtc.AddDays((sprintNumber - 1) * 14);
            var endUtc = startUtc.AddDays(13);
            var sprint = new SprintEntity
            {
                TeamId = team.Id,
                Name = $"Sprint {sprintNumber}",
                Path = $"\\Project\\Sprint {sprintNumber}",
                StartUtc = new DateTimeOffset(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), TimeSpan.Zero),
                StartDateUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
                EndUtc = new DateTimeOffset(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc), TimeSpan.Zero),
                EndDateUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
                TimeFrame = sprintNumber <= completedSprintCount ? "past" : "current",
                LastSyncedUtc = DateTimeOffset.UtcNow,
                LastSyncedDateUtc = DateTime.UtcNow
            };

            sprints.Add(sprint);
            context.Sprints.Add(sprint);
        }

        await context.SaveChangesAsync();

        var completedSprints = sprints.Take(completedSprintCount).ToList();
        var selectedWindow = completedSprints.TakeLast(ExecutionRealityCheckCdcSliceProjector.RequiredWindowSize).ToList();

        if (addFactsForWindow)
        {
            var firstWindowSprintNumber = ExtractSprintNumber(selectedWindow[0].Name);
            var lastWindowSprintNumber = ExtractSprintNumber(selectedWindow[^1].Name);

            for (var index = 0; index < selectedWindow.Count; index++)
            {
                var sprint = selectedWindow[index];
                var sprintNumber = ExtractSprintNumber(sprint.Name);
                if (missingFactSprintNumber.HasValue && sprintNumber == missingFactSprintNumber.Value)
                {
                    continue;
                }

                SeedDeliveredWorkItem(context, productOwner.Id, product.Id, sprint, sprintNumber);
            }

            for (var sprintNumber = Math.Max(1, firstWindowSprintNumber - 1); sprintNumber <= lastWindowSprintNumber; sprintNumber++)
            {
                if (missingFactSprintNumber.HasValue
                    && (sprintNumber == missingFactSprintNumber.Value || sprintNumber == missingFactSprintNumber.Value - 1))
                {
                    continue;
                }

                var sourceSprint = sprints.Single(candidate => candidate.Name == $"Sprint {sprintNumber}");
                var targetSprint = sprints.Single(candidate => candidate.Name == $"Sprint {sprintNumber + 1}");
                SeedSpilloverWorkItem(
                    context,
                    productOwner.Id,
                    product.Id,
                    sourceSprint,
                    targetSprint,
                    sprintNumber,
                    deliveredInTargetSprint: sprintNumber < lastWindowSprintNumber);
            }

            await context.SaveChangesAsync();
        }

        return new ScenarioContext(
            productOwner.Id,
            product.Id,
            sprints.Single(sprint => sprint.Name == $"Sprint {completedSprintCount + 1}").Id,
            selectedWindow.Select(sprint => sprint.Id).ToArray());
    }

    private ExecutionRealityCheckCdcSliceService CreateService(PoToolDbContext context)
    {
        return new ExecutionRealityCheckCdcSliceService(
            context,
            NullLogger<ExecutionRealityCheckCdcSliceService>.Instance,
            stateClassificationService: null,
            _serviceProvider.GetRequiredService<ISprintSpilloverService>(),
            _serviceProvider.GetRequiredService<ISprintFactService>(),
            _serviceProvider.GetRequiredService<ISprintExecutionMetricsCalculator>(),
            _serviceProvider.GetRequiredService<IExecutionRealityCheckCdcSliceProjector>());
    }

    private static void SeedDeliveredWorkItem(
        PoToolDbContext context,
        int productOwnerId,
        int productId,
        SprintEntity sprint,
        int sprintNumber)
    {
        var deliveredWorkItemId = 10_000 + (sprintNumber * 100);
        var doneTimestamp = sprint.StartUtc!.Value.AddDays(2);

        context.WorkItems.Add(
            new WorkItemEntity
            {
                TfsId = deliveredWorkItemId,
                Type = "Product Backlog Item",
                Title = $"Delivered Sprint {sprintNumber}",
                AreaPath = "\\Project",
                IterationPath = sprint.Path,
                State = "Done",
                StoryPoints = 6,
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = doneTimestamp,
                TfsChangedDateUtc = doneTimestamp.UtcDateTime
            });

        context.ResolvedWorkItems.Add(
            new ResolvedWorkItemEntity
            {
                WorkItemId = deliveredWorkItemId,
                WorkItemType = "Product Backlog Item",
                ResolvedProductId = productId,
                ResolvedSprintId = sprint.Id,
                ResolutionStatus = ResolutionStatus.Resolved,
                ResolvedAtRevision = 1
            });

        context.ActivityEventLedgerEntries.Add(
            new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = productOwnerId,
                WorkItemId = deliveredWorkItemId,
                UpdateId = 1,
                FieldRefName = "System.State",
                EventTimestamp = doneTimestamp,
                EventTimestampUtc = doneTimestamp.UtcDateTime,
                OldValue = "Active",
                NewValue = "Done"
            });
    }

    private static void SeedSpilloverWorkItem(
        PoToolDbContext context,
        int productOwnerId,
        int productId,
        SprintEntity sourceSprint,
        SprintEntity targetSprint,
        int sourceSprintNumber,
        bool deliveredInTargetSprint)
    {
        var spilloverWorkItemId = 20_000 + (sourceSprintNumber * 100);
        var spilloverTimestamp = sourceSprint.EndUtc!.Value.AddHours(1);
        var doneTimestamp = targetSprint.StartUtc!.Value.AddDays(2);

        context.WorkItems.Add(
            new WorkItemEntity
            {
                TfsId = spilloverWorkItemId,
                Type = "Product Backlog Item",
                Title = $"Spillover Sprint {sourceSprintNumber}",
                AreaPath = "\\Project",
                IterationPath = targetSprint.Path,
                State = deliveredInTargetSprint ? "Done" : "Active",
                StoryPoints = 2,
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = deliveredInTargetSprint ? doneTimestamp : spilloverTimestamp,
                TfsChangedDateUtc = deliveredInTargetSprint ? doneTimestamp.UtcDateTime : spilloverTimestamp.UtcDateTime
            });

        context.ResolvedWorkItems.Add(
            new ResolvedWorkItemEntity
            {
                WorkItemId = spilloverWorkItemId,
                WorkItemType = "Product Backlog Item",
                ResolvedProductId = productId,
                ResolvedSprintId = targetSprint.Id,
                ResolutionStatus = ResolutionStatus.Resolved,
                ResolvedAtRevision = 1
            });

        context.ActivityEventLedgerEntries.Add(
            new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = productOwnerId,
                WorkItemId = spilloverWorkItemId,
                UpdateId = 2,
                FieldRefName = "System.IterationPath",
                EventTimestamp = spilloverTimestamp,
                EventTimestampUtc = spilloverTimestamp.UtcDateTime,
                OldValue = sourceSprint.Path,
                NewValue = targetSprint.Path
            });

        if (deliveredInTargetSprint)
        {
            context.ActivityEventLedgerEntries.Add(
                new ActivityEventLedgerEntryEntity
                {
                    ProductOwnerId = productOwnerId,
                    WorkItemId = spilloverWorkItemId,
                    UpdateId = 3,
                    FieldRefName = "System.State",
                    EventTimestamp = doneTimestamp,
                    EventTimestampUtc = doneTimestamp.UtcDateTime,
                    OldValue = "Active",
                    NewValue = "Done"
                });
        }
    }

    private static int ExtractSprintNumber(string sprintName)
    {
        return int.Parse(sprintName["Sprint ".Length..]);
    }

    private sealed record ScenarioContext(
        int ProductOwnerId,
        int ProductId,
        int AnchorSprintId,
        IReadOnlyList<int> WindowSprintIds);
}
