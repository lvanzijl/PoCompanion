using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class SprintTrendProjectionServiceSqliteTests
{
    private ServiceProvider _serviceProvider = null!;
    private string _databasePath = null!;

    [TestInitialize]
    public void Setup()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"sprint-trend-{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddDbContext<PoToolDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        context.Database.EnsureCreated();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [TestMethod]
    public async Task ComputeProjectionsAsync_WithSprintDateRange_DoesNotThrowOnSqlite()
    {
        int productOwnerId;
        int sprintId;

        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

            var profile = new ProfileEntity { Name = "PO Test" };
            context.Profiles.Add(profile);
            await context.SaveChangesAsync();
            productOwnerId = profile.Id;

            var product = new ProductEntity
            {
                ProductOwnerId = profile.Id,
                Name = "Product A",
                BacklogRootWorkItemId = 1000
            };
            var team = new TeamEntity
            {
                Name = "Team A",
                TeamAreaPath = "\\Project\\TeamA"
            };
            context.Products.Add(product);
            context.Teams.Add(team);
            await context.SaveChangesAsync();

            var sprint = new SprintEntity
            {
                TeamId = team.Id,
                Path = "\\Project\\Sprint 1",
                Name = "Sprint 1",
                StartUtc = new DateTimeOffset(2026, 01, 01, 0, 0, 0, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(2026, 01, 14, 23, 59, 59, TimeSpan.Zero)
            };
            context.Sprints.Add(sprint);
            await context.SaveChangesAsync();
            sprintId = sprint.Id;

            context.ResolvedWorkItems.Add(new ResolvedWorkItemEntity
            {
                WorkItemId = 101,
                WorkItemType = "Task",
                ResolvedProductId = product.Id,
                ResolutionStatus = ResolutionStatus.Resolved,
                ResolvedAtRevision = 1
            });

            var revision = new RevisionHeaderEntity
            {
                WorkItemId = 101,
                RevisionNumber = 1,
                WorkItemType = "Task",
                Title = "Task 101",
                State = "In Progress",
                IterationPath = sprint.Path,
                AreaPath = "\\Project\\TeamA",
                ChangedDate = new DateTimeOffset(2026, 01, 07, 12, 0, 0, TimeSpan.Zero)
            };
            revision.FieldDeltas.Add(new RevisionFieldDeltaEntity
            {
                FieldName = "System.State",
                OldValue = "New",
                NewValue = "In Progress"
            });

            context.RevisionHeaders.Add(revision);
            await context.SaveChangesAsync();
        }

        var service = new SprintTrendProjectionService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SprintTrendProjectionService>.Instance);

        var projections = await service.ComputeProjectionsAsync(productOwnerId, new[] { sprintId });

        Assert.HasCount(1, projections);
        Assert.AreEqual(1, projections[0].PlannedCount);
        Assert.AreEqual(1, projections[0].WorkedCount);
    }
}
