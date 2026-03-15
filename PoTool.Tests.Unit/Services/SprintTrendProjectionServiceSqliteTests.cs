using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.WorkItems;

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
        services.AddSingleton<ICanonicalStoryPointResolutionService, CanonicalStoryPointResolutionService>();
        services.AddSingleton<IHierarchyRollupService, HierarchyRollupService>();
        services.AddSingleton<IDeliveryProgressRollupService, DeliveryProgressRollupService>();
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
            _serviceProvider.GetRequiredService<ISprintDeliveryProjectionService>());

        var projections = await service.ComputeProjectionsAsync(productOwnerId, new[] { sprintId });

        Assert.HasCount(1, projections);
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
}
