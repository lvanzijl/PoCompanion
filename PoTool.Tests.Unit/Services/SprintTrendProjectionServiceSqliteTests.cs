using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.Portfolio;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.Metrics;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class SprintTrendProjectionServiceSqliteTests
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
        services.AddSingleton<IHierarchyRollupService, HierarchyRollupService>();
        services.AddSingleton<IDeliveryProgressRollupService, DeliveryProgressRollupService>();
        services.AddSingleton<IPortfolioFlowSummaryService, PortfolioFlowSummaryService>();
        services.AddSingleton<ISprintDeliveryProjectionService, SprintDeliveryProjectionService>();

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
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
    public async Task ComputeProjectionsAsync_WithSqlite_ExecutesWithoutTranslationFailure()
    {
        int productOwnerId;
        int sprintId;

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            var productOwner = new ProfileEntity { Name = "PO 1" };
            context.Profiles.Add(productOwner);
            await context.SaveChangesAsync();
            productOwnerId = productOwner.Id;

            var team = new TeamEntity { Name = "Team 1", TeamAreaPath = "\\Project\\Team 1" };
            context.Teams.Add(team);

            var product = new ProductEntity
            {
                ProductOwnerId = productOwner.Id,
                Name = "Product 1",
                BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 1000 } }
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var sprintStartUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var sprintEndUtc = new DateTime(2026, 1, 14, 23, 59, 59, DateTimeKind.Utc);
            var sprint = new SprintEntity
            {
                TeamId = team.Id,
                Name = "Sprint 1",
                Path = "\\Project\\Sprint 1",
                StartDateUtc = sprintStartUtc,
                EndDateUtc = sprintEndUtc,
                LastSyncedDateUtc = DateTime.UtcNow
            };
            context.Sprints.Add(sprint);
            await context.SaveChangesAsync();
            sprintId = sprint.Id;

            var workItem = new WorkItemEntity
            {
                TfsId = 1001,
                Type = "Product Backlog Item",
                Title = "PBI 1",
                AreaPath = "Area",
                IterationPath = sprint.Path,
                State = "Done",
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                TfsChangedDateUtc = DateTime.UtcNow
            };
            context.WorkItems.Add(workItem);

            context.ResolvedWorkItems.Add(new ResolvedWorkItemEntity
            {
                WorkItemId = 1001,
                WorkItemType = WorkItemType.Pbi,
                ResolvedProductId = product.Id,
                ResolvedSprintId = sprint.Id,
                ResolutionStatus = ResolutionStatus.Resolved,
                ResolvedAtRevision = 1
            });

            var eventTimestamp = new DateTimeOffset(new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc));
            context.ActivityEventLedgerEntries.Add(new ActivityEventLedgerEntryEntity
            {
                ProductOwnerId = productOwnerId,
                WorkItemId = 1001,
                UpdateId = 1,
                FieldRefName = "System.State",
                EventTimestamp = eventTimestamp,
                EventTimestampUtc = eventTimestamp.UtcDateTime,
                NewValue = "Done"
            });

            await context.SaveChangesAsync();
        }

        var service = new SprintTrendProjectionService(
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

        var projections = await service.ComputeProjectionsAsync(productOwnerId, new[] { sprintId });

        Assert.HasCount(1, projections);
    }

    [TestMethod]
    public async Task ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionInTheSprintPipeline()
    {
        int productOwnerId;
        int sprintId;
        int productId;

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            var productOwner = new ProfileEntity { Name = "PO 1" };
            context.Profiles.Add(productOwner);
            await context.SaveChangesAsync();
            productOwnerId = productOwner.Id;

            var team = new TeamEntity { Name = "Team 1", TeamAreaPath = "\\Project\\Team 1" };
            context.Teams.Add(team);

            var product = new ProductEntity
            {
                ProductOwnerId = productOwner.Id,
                Name = "Product 1",
                BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 1000 } }
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            productId = product.Id;

            var sprintStartUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            var sprintEndUtc = new DateTime(2026, 2, 14, 23, 59, 59, DateTimeKind.Utc);
            var sprint = new SprintEntity
            {
                TeamId = team.Id,
                Name = "Sprint 2",
                Path = "\\Project\\Sprint 2",
                StartDateUtc = sprintStartUtc,
                EndDateUtc = sprintEndUtc,
                LastSyncedDateUtc = DateTime.UtcNow
            };
            context.Sprints.Add(sprint);
            await context.SaveChangesAsync();
            sprintId = sprint.Id;

            context.WorkItems.Add(new WorkItemEntity
            {
                TfsId = 2001,
                Type = WorkItemType.Pbi,
                Title = "Portfolio flow PBI",
                AreaPath = "Area",
                IterationPath = sprint.Path,
                State = "Done",
                StoryPoints = 8,
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                TfsChangedDateUtc = DateTime.UtcNow
            });

            context.ResolvedWorkItems.Add(new ResolvedWorkItemEntity
            {
                WorkItemId = 2001,
                WorkItemType = WorkItemType.Pbi,
                ResolvedProductId = product.Id,
                ResolvedSprintId = sprint.Id,
                ResolutionStatus = ResolutionStatus.Resolved,
                ResolvedAtRevision = 1
            });

            var entryTimestamp = new DateTimeOffset(new DateTime(2026, 2, 2, 12, 0, 0, DateTimeKind.Utc));
            var doneTimestamp = new DateTimeOffset(new DateTime(2026, 2, 4, 12, 0, 0, DateTimeKind.Utc));

            context.ActivityEventLedgerEntries.AddRange(
                new ActivityEventLedgerEntryEntity
                {
                    ProductOwnerId = productOwnerId,
                    WorkItemId = 2001,
                    UpdateId = 1,
                    FieldRefName = PortfolioEntryLookup.ResolvedProductIdFieldRefName,
                    EventTimestamp = entryTimestamp,
                    EventTimestampUtc = entryTimestamp.UtcDateTime,
                    NewValue = product.Id.ToString()
                },
                new ActivityEventLedgerEntryEntity
                {
                    ProductOwnerId = productOwnerId,
                    WorkItemId = 2001,
                    UpdateId = 2,
                    FieldRefName = "System.State",
                    EventTimestamp = doneTimestamp,
                    EventTimestampUtc = doneTimestamp.UtcDateTime,
                    OldValue = "Active",
                    NewValue = "Done"
                });

            await context.SaveChangesAsync();
        }

        var portfolioFlowProjectionService = new PortfolioFlowProjectionService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PortfolioFlowProjectionService>.Instance,
            stateClassificationService: null,
            _serviceProvider.GetRequiredService<ISprintCompletionService>(),
            _serviceProvider.GetRequiredService<ICanonicalStoryPointResolutionService>());
        var service = new SprintTrendProjectionService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SprintTrendProjectionService>.Instance,
            stateClassificationService: null,
            _serviceProvider.GetRequiredService<ICanonicalStoryPointResolutionService>(),
            _serviceProvider.GetRequiredService<IHierarchyRollupService>(),
            _serviceProvider.GetRequiredService<IDeliveryProgressRollupService>(),
            _serviceProvider.GetRequiredService<ISprintCommitmentService>(),
            _serviceProvider.GetRequiredService<ISprintCompletionService>(),
            _serviceProvider.GetRequiredService<ISprintSpilloverService>(),
            _serviceProvider.GetRequiredService<ISprintDeliveryProjectionService>(),
            portfolioFlowProjectionService);

        await service.ComputeProjectionsAsync(productOwnerId, new[] { sprintId });

        using var verificationScope = _serviceProvider.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var projection = await verificationContext.PortfolioFlowProjections.SingleAsync(
            item => item.SprintId == sprintId && item.ProductId == productId);

        Assert.AreEqual(8d, projection.StockStoryPoints, 0.001d);
        Assert.AreEqual(8d, projection.InflowStoryPoints, 0.001d);
        Assert.AreEqual(8d, projection.ThroughputStoryPoints, 0.001d);
        Assert.IsNotNull(projection.CompletionPercent);
        Assert.AreEqual(100d, projection.CompletionPercent.Value, 0.001d);
    }

    [TestMethod]
    public async Task LegacyAndPortfolioFlowRepresentativeDataset_ProduceExplainableDifferences()
    {
        int productOwnerId;
        int productId;
        int sprint1Id;
        int sprint2Id;

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            var productOwner = new ProfileEntity { Name = "PO 1" };
            context.Profiles.Add(productOwner);
            await context.SaveChangesAsync();
            productOwnerId = productOwner.Id;

            var team = new TeamEntity { Name = "Team 1", TeamAreaPath = "\\Project\\Team 1" };
            context.Teams.Add(team);

            var product = new ProductEntity
            {
                ProductOwnerId = productOwner.Id,
                Name = "Product 1",
                BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 1000 } }
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            productId = product.Id;

            var sprint1 = new SprintEntity
            {
                TeamId = team.Id,
                Name = "Sprint 1",
                Path = "\\Project\\Sprint 1",
                StartDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDateUtc = new DateTime(2026, 1, 14, 23, 59, 59, DateTimeKind.Utc),
                LastSyncedDateUtc = DateTime.UtcNow
            };
            var sprint2 = new SprintEntity
            {
                TeamId = team.Id,
                Name = "Sprint 2",
                Path = "\\Project\\Sprint 2",
                StartDateUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDateUtc = new DateTime(2026, 2, 14, 23, 59, 59, DateTimeKind.Utc),
                LastSyncedDateUtc = DateTime.UtcNow
            };

            context.Sprints.AddRange(sprint1, sprint2);
            await context.SaveChangesAsync();
            sprint1Id = sprint1.Id;
            sprint2Id = sprint2.Id;

            context.WorkItems.AddRange(
                CreateWorkItem(3001, WorkItemType.Pbi, "Done", storyPoints: 8, effort: 20, createdDate: new DateTimeOffset(new DateTime(2025, 12, 10, 0, 0, 0, DateTimeKind.Utc))),
                CreateWorkItem(3002, WorkItemType.Pbi, "Active", storyPoints: 5, effort: 13, createdDate: new DateTimeOffset(new DateTime(2025, 12, 12, 0, 0, 0, DateTimeKind.Utc))),
                CreateWorkItem(3003, WorkItemType.Pbi, "Done", storyPoints: 3, effort: 8, createdDate: new DateTimeOffset(new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc))),
                CreateWorkItem(3004, WorkItemType.Pbi, "Active", storyPoints: 12, effort: 21, createdDate: new DateTimeOffset(new DateTime(2026, 2, 4, 0, 0, 0, DateTimeKind.Utc))));

            context.ResolvedWorkItems.AddRange(
                CreateResolvedWorkItem(3001, product.Id),
                CreateResolvedWorkItem(3002, product.Id),
                CreateResolvedWorkItem(3003, product.Id),
                CreateResolvedWorkItem(3004, product.Id));

            context.ActivityEventLedgerEntries.AddRange(
                CreateActivityEvent(productOwnerId, 3001, 1, "System.State", new DateTimeOffset(new DateTime(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc)), "Active", "Done"),
                CreateActivityEvent(productOwnerId, 3003, 2, PortfolioEntryLookup.ResolvedProductIdFieldRefName, new DateTimeOffset(new DateTime(2026, 2, 3, 12, 0, 0, DateTimeKind.Utc)), null, product.Id.ToString()),
                CreateActivityEvent(productOwnerId, 3003, 3, "System.State", new DateTimeOffset(new DateTime(2026, 2, 5, 12, 0, 0, DateTimeKind.Utc)), "Active", "Done"),
                CreateActivityEvent(productOwnerId, 3004, 4, PortfolioEntryLookup.ResolvedProductIdFieldRefName, new DateTimeOffset(new DateTime(2026, 2, 4, 12, 0, 0, DateTimeKind.Utc)), null, product.Id.ToString()));

            context.SprintMetricsProjections.AddRange(
                new SprintMetricsProjectionEntity
                {
                    SprintId = sprint1.Id,
                    ProductId = product.Id,
                    PlannedEffort = 13,
                    CompletedPbiCount = 1,
                    CompletedPbiEffort = 20,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 1
                },
                new SprintMetricsProjectionEntity
                {
                    SprintId = sprint2.Id,
                    ProductId = product.Id,
                    PlannedEffort = 34,
                    CompletedPbiCount = 1,
                    CompletedPbiEffort = 8,
                    LastComputedAt = DateTimeOffset.UtcNow,
                    IncludedUpToRevisionId = 2
                });

            await context.SaveChangesAsync();
        }

        var portfolioFlowProjectionService = CreatePortfolioFlowProjectionService();
        var projections = (await portfolioFlowProjectionService.ComputeProjectionsAsync(productOwnerId, new[] { sprint1Id, sprint2Id }))
            .OrderBy(item => item.SprintId)
            .ToList();

        PortfolioProgressTrendDto trend;

        await using (var scope = _serviceProvider.CreateAsyncScope())
        {
            var handler = new GetPortfolioProgressTrendQueryHandler(
                scope.ServiceProvider.GetRequiredService<PoToolDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPortfolioFlowSummaryService>(),
                NullLogger<GetPortfolioProgressTrendQueryHandler>.Instance);

            trend = await handler.Handle(
                new GetPortfolioProgressTrendQuery(
                    DeliveryFilterTestFactory.MultiSprint(
                        [productId],
                        [sprint1Id, sprint2Id],
                        new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                        new DateTimeOffset(new DateTime(2026, 2, 14, 23, 59, 59, DateTimeKind.Utc)))),
                CancellationToken.None);
        }

        Assert.AreEqual(PortfolioTrajectory.Expanding, trend.Summary.Trajectory);
        Assert.HasCount(2, trend.Sprints);
        Assert.HasCount(2, projections);

        Assert.AreEqual(13d, trend.Sprints[0].StockStoryPoints!.Value, 0.001d);
        Assert.AreEqual(5d, trend.Sprints[0].RemainingScopeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(0d, trend.Sprints[0].InflowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(8d, trend.Sprints[0].ThroughputStoryPoints!.Value, 0.001d);
        Assert.AreEqual(8d, trend.Sprints[0].NetFlowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(61.538d, trend.Sprints[0].CompletionPercent!.Value, 0.001d);

        Assert.AreEqual(28d, trend.Sprints[1].StockStoryPoints!.Value, 0.001d);
        Assert.AreEqual(17d, trend.Sprints[1].RemainingScopeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(15d, trend.Sprints[1].InflowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(3d, trend.Sprints[1].ThroughputStoryPoints!.Value, 0.001d);
        Assert.AreEqual(-12d, trend.Sprints[1].NetFlowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(39.286d, trend.Sprints[1].CompletionPercent!.Value, 0.001d);

        Assert.AreEqual(13d, projections[0].StockStoryPoints, 0.001d);
        Assert.AreEqual(5d, projections[0].RemainingScopeStoryPoints, 0.001d);
        Assert.AreEqual(0d, projections[0].InflowStoryPoints, 0.001d);
        Assert.AreEqual(8d, projections[0].ThroughputStoryPoints, 0.001d);
        Assert.AreEqual(61.538d, projections[0].CompletionPercent!.Value, 0.001d);

        Assert.AreEqual(28d, projections[1].StockStoryPoints, 0.001d);
        Assert.AreEqual(17d, projections[1].RemainingScopeStoryPoints, 0.001d);
        Assert.AreEqual(15d, projections[1].InflowStoryPoints, 0.001d);
        Assert.AreEqual(3d, projections[1].ThroughputStoryPoints, 0.001d);
        Assert.AreEqual(39.286d, projections[1].CompletionPercent!.Value, 0.001d);

        Assert.AreEqual(projections[0].StockStoryPoints, trend.Sprints[0].StockStoryPoints!.Value, 0.001d);
        Assert.AreEqual(projections[0].RemainingScopeStoryPoints, trend.Sprints[0].RemainingScopeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(projections[0].InflowStoryPoints, trend.Sprints[0].InflowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(projections[0].ThroughputStoryPoints, trend.Sprints[0].ThroughputStoryPoints!.Value, 0.001d);
        Assert.AreEqual(projections[1].StockStoryPoints, trend.Sprints[1].StockStoryPoints!.Value, 0.001d);
        Assert.AreEqual(projections[1].RemainingScopeStoryPoints, trend.Sprints[1].RemainingScopeStoryPoints!.Value, 0.001d);
        Assert.AreEqual(projections[1].InflowStoryPoints, trend.Sprints[1].InflowStoryPoints!.Value, 0.001d);
        Assert.AreEqual(projections[1].ThroughputStoryPoints, trend.Sprints[1].ThroughputStoryPoints!.Value, 0.001d);

        var trendCumulativeNet = trend.Sprints.Sum(item => item.NetFlowStoryPoints ?? 0d);
        var portfolioFlowCumulativeNet = projections.Sum(item => item.ThroughputStoryPoints - item.InflowStoryPoints);

        Assert.IsLessThan(0d, trendCumulativeNet);
        Assert.IsLessThan(0d, portfolioFlowCumulativeNet);
    }

    [TestMethod]
    public async Task ComputeProjectionsAsync_RebuildsPortfolioFlowProjectionDeterministicallyWithoutDuplicates()
    {
        int productOwnerId;
        int productId;
        int sprintId;

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            var productOwner = new ProfileEntity { Name = "PO 1" };
            context.Profiles.Add(productOwner);
            await context.SaveChangesAsync();
            productOwnerId = productOwner.Id;

            var team = new TeamEntity { Name = "Team 1", TeamAreaPath = "\\Project\\Team 1" };
            context.Teams.Add(team);

            var product = new ProductEntity
            {
                ProductOwnerId = productOwner.Id,
                Name = "Product 1",
                BacklogRoots = new List<ProductBacklogRootEntity> { new() { WorkItemTfsId = 1000 } }
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            productId = product.Id;

            var sprint = new SprintEntity
            {
                TeamId = team.Id,
                Name = "Sprint 1",
                Path = "\\Project\\Sprint 1",
                StartDateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDateUtc = new DateTime(2026, 3, 14, 23, 59, 59, DateTimeKind.Utc),
                LastSyncedDateUtc = DateTime.UtcNow
            };
            context.Sprints.Add(sprint);
            await context.SaveChangesAsync();
            sprintId = sprint.Id;

            context.WorkItems.Add(CreateWorkItem(
                4001,
                WorkItemType.Pbi,
                "Done",
                storyPoints: 8,
                effort: 13,
                createdDate: new DateTimeOffset(new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc))));

            context.ResolvedWorkItems.Add(CreateResolvedWorkItem(4001, product.Id));

            var entryTimestamp = new DateTimeOffset(new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc));
            var doneTimestamp = new DateTimeOffset(new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc));

            context.ActivityEventLedgerEntries.AddRange(
                CreateActivityEvent(productOwnerId, 4001, 1, PortfolioEntryLookup.ResolvedProductIdFieldRefName, entryTimestamp, null, product.Id.ToString()),
                CreateActivityEvent(productOwnerId, 4001, 2, "System.State", doneTimestamp, "Active", "Done"));

            await context.SaveChangesAsync();
        }

        var service = CreatePortfolioFlowProjectionService();

        var firstRun = await service.ComputeProjectionsAsync(productOwnerId, new[] { sprintId });
        var secondRun = await service.ComputeProjectionsAsync(productOwnerId, new[] { sprintId });
        var thirdRun = await service.ComputeProjectionsAsync(productOwnerId, new[] { sprintId });

        Assert.HasCount(1, firstRun);
        Assert.HasCount(1, secondRun);
        Assert.HasCount(1, thirdRun);
        Assert.AreEqual(8d, firstRun[0].InflowStoryPoints, 0.001d);
        Assert.AreEqual(8d, secondRun[0].InflowStoryPoints, 0.001d);
        Assert.AreEqual(8d, thirdRun[0].InflowStoryPoints, 0.001d);
        Assert.AreEqual(8d, firstRun[0].ThroughputStoryPoints, 0.001d);
        Assert.AreEqual(8d, secondRun[0].ThroughputStoryPoints, 0.001d);
        Assert.AreEqual(8d, thirdRun[0].ThroughputStoryPoints, 0.001d);

        using var verificationScope = _serviceProvider.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var persisted = await verificationContext.PortfolioFlowProjections
            .Where(item => item.SprintId == sprintId && item.ProductId == productId)
            .ToListAsync();

        Assert.HasCount(1, persisted);
        Assert.AreEqual(8d, persisted[0].StockStoryPoints, 0.001d);
        Assert.AreEqual(8d, persisted[0].InflowStoryPoints, 0.001d);
        Assert.AreEqual(8d, persisted[0].ThroughputStoryPoints, 0.001d);
        Assert.IsNotNull(persisted[0].CompletionPercent);
        Assert.AreEqual(100d, persisted[0].CompletionPercent!.Value, 0.001d);
    }

    [TestMethod]
    public void Sqlite_ModelHasNoIndexedDateTimeOffsetProperties()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var indexedDateTimeOffsetProperties = context.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetIndexes()
                .SelectMany(index => index.Properties)
                .Where(property => property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                .Select(property => $"{entityType.ClrType.Name}.{property.Name}"))
            .Distinct()
            .ToList();

        Assert.IsEmpty(indexedDateTimeOffsetProperties);
    }

    private PortfolioFlowProjectionService CreatePortfolioFlowProjectionService()
    {
        return new PortfolioFlowProjectionService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PortfolioFlowProjectionService>.Instance,
            stateClassificationService: null,
            _serviceProvider.GetRequiredService<ISprintCompletionService>(),
            _serviceProvider.GetRequiredService<ICanonicalStoryPointResolutionService>());
    }

    private static WorkItemEntity CreateWorkItem(
        int workItemId,
        string workItemType,
        string state,
        int storyPoints,
        int effort,
        DateTimeOffset createdDate)
    {
        return new WorkItemEntity
        {
            TfsId = workItemId,
            Type = workItemType,
            Title = $"PBI {workItemId}",
            AreaPath = "Area",
            IterationPath = "\\Project\\Sprint",
            State = state,
            StoryPoints = storyPoints,
            Effort = effort,
            CreatedDate = createdDate,
            RetrievedAt = DateTimeOffset.UtcNow,
            TfsChangedDate = DateTimeOffset.UtcNow,
            TfsChangedDateUtc = DateTime.UtcNow
        };
    }

    private static ResolvedWorkItemEntity CreateResolvedWorkItem(int workItemId, int productId)
    {
        return new ResolvedWorkItemEntity
        {
            WorkItemId = workItemId,
            WorkItemType = WorkItemType.Pbi,
            ResolvedProductId = productId,
            ResolutionStatus = ResolutionStatus.Resolved,
            ResolvedAtRevision = 1
        };
    }

    private static ActivityEventLedgerEntryEntity CreateActivityEvent(
        int productOwnerId,
        int workItemId,
        int updateId,
        string fieldRefName,
        DateTimeOffset eventTimestamp,
        string? oldValue,
        string? newValue)
    {
        return new ActivityEventLedgerEntryEntity
        {
            ProductOwnerId = productOwnerId,
            WorkItemId = workItemId,
            UpdateId = updateId,
            FieldRefName = fieldRefName,
            EventTimestamp = eventTimestamp,
            EventTimestampUtc = eventTimestamp.UtcDateTime,
            OldValue = oldValue,
            NewValue = newValue
        };
    }
}
