using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Repositories;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ProjectPlanningSummaryServiceTests
{
    private PoToolDbContext _context = null!;
    private Mock<IWorkItemReadProvider> _workItemReadProvider = null!;
    private ProjectPlanningSummaryService _service = null!;

    [TestInitialize]
    public void Initialize()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite("Data Source=:memory:", sqliteOptions =>
            {
                sqliteOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .Options;

        _context = new PoToolDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _workItemReadProvider = new Mock<IWorkItemReadProvider>();

        _service = new ProjectPlanningSummaryService(
            _context,
            new ProjectRepository(_context),
            _workItemReadProvider.Object,
            new VelocityCalibrationService());
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [TestMethod]
    public async Task GetSummaryAsync_WhenProjectMissing_ReturnsNull()
    {
        var result = await _service.GetSummaryAsync("missing-project");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetSummaryAsync_AggregatesProjectPlanningSignalsAcrossProducts()
    {
        SeedProjectAndProducts();
        SeedStateClassifications();
        SeedSprintsAndProjections();

        _workItemReadProvider
            .Setup(provider => provider.GetByRootIdsAsync(It.IsAny<int[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildWorkItems());

        var result = await _service.GetSummaryAsync("project-alpha");

        Assert.IsNotNull(result);
        Assert.AreEqual("project-alpha", result.ProjectAlias);
        Assert.AreEqual(2, result.ProductCount);
        Assert.AreEqual(2, result.TotalEpics);
        Assert.AreEqual(3, result.TotalPBIs);
        Assert.AreEqual(2, result.PlannedPBIs);
        Assert.AreEqual(1, result.UnplannedPBIs);
        Assert.AreEqual(60, result.TotalEffort);
        Assert.AreEqual(50, result.PlannedEffort);
        Assert.AreEqual(16d, result.CapacityPerSprint, 0.001d);
        Assert.IsTrue(result.OvercommitIndicator);
        Assert.HasCount(2, result.Products);

        var leadingProduct = result.Products.First();
        Assert.AreEqual("Alpha Product", leadingProduct.ProductName);
        Assert.AreEqual(40, leadingProduct.TotalEffort);
        Assert.AreEqual(30, leadingProduct.PlannedEffort);
        Assert.AreEqual(11d, leadingProduct.CapacityPerSprint, 0.001d);
    }

    private void SeedProjectAndProducts()
    {
        var project = new ProjectEntity
        {
            Id = "project-1",
            Alias = "project-alpha",
            Name = "Project Alpha"
        };

        var product1 = new ProductEntity
        {
            Id = 1,
            Name = "Alpha Product",
            ProjectId = project.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow,
            PictureType = (int)ProductPictureType.Default,
            DefaultPictureId = 0,
            Order = 0,
            EstimationMode = 0
        };

        var product2 = new ProductEntity
        {
            Id = 2,
            Name = "Beta Product",
            ProjectId = project.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow,
            PictureType = (int)ProductPictureType.Default,
            DefaultPictureId = 0,
            Order = 1,
            EstimationMode = 0
        };

        var team1 = new TeamEntity
        {
            Id = 10,
            Name = "Alpha Team",
            TeamAreaPath = "Alpha"
        };

        var team2 = new TeamEntity
        {
            Id = 20,
            Name = "Beta Team",
            TeamAreaPath = "Beta"
        };

        _context.Projects.Add(project);
        _context.Products.AddRange(product1, product2);
        _context.Teams.AddRange(team1, team2);
        _context.ProductBacklogRoots.AddRange(
            new ProductBacklogRootEntity { ProductId = 1, WorkItemTfsId = 100 },
            new ProductBacklogRootEntity { ProductId = 2, WorkItemTfsId = 200 });
        _context.ProductTeamLinks.AddRange(
            new ProductTeamLinkEntity { ProductId = 1, TeamId = 10 },
            new ProductTeamLinkEntity { ProductId = 2, TeamId = 20 });
        _context.SaveChanges();
    }

    private void SeedStateClassifications()
    {
        _context.WorkItemStateClassifications.AddRange(
            new WorkItemStateClassificationEntity
            {
                TfsProjectName = "Alpha",
                WorkItemType = "Product Backlog Item",
                StateName = "Closed",
                Classification = (int)StateClassification.Done
            },
            new WorkItemStateClassificationEntity
            {
                TfsProjectName = "Alpha",
                WorkItemType = "PBI",
                StateName = "Closed",
                Classification = (int)StateClassification.Done
            });

        _context.SaveChanges();
    }

    private void SeedSprintsAndProjections()
    {
        var now = DateTimeOffset.UtcNow;

        _context.Sprints.AddRange(
            new SprintEntity { Id = 101, TeamId = 10, Name = "Alpha Future 1", Path = @"Alpha\Sprint 1", StartUtc = now.AddDays(1), EndUtc = now.AddDays(14), StartDateUtc = now.AddDays(1).UtcDateTime, EndDateUtc = now.AddDays(14).UtcDateTime },
            new SprintEntity { Id = 102, TeamId = 10, Name = "Alpha Future 2", Path = @"Alpha\Sprint 2", StartUtc = now.AddDays(15), EndUtc = now.AddDays(28), StartDateUtc = now.AddDays(15).UtcDateTime, EndDateUtc = now.AddDays(28).UtcDateTime },
            new SprintEntity { Id = 103, TeamId = 10, Name = "Alpha Future 3", Path = @"Alpha\Sprint 3", StartUtc = now.AddDays(29), EndUtc = now.AddDays(42), StartDateUtc = now.AddDays(29).UtcDateTime, EndDateUtc = now.AddDays(42).UtcDateTime },
            new SprintEntity { Id = 104, TeamId = 10, Name = "Alpha Past 1", Path = @"Alpha\Past 1", StartUtc = now.AddDays(-28), EndUtc = now.AddDays(-14), StartDateUtc = now.AddDays(-28).UtcDateTime, EndDateUtc = now.AddDays(-14).UtcDateTime },
            new SprintEntity { Id = 105, TeamId = 10, Name = "Alpha Past 2", Path = @"Alpha\Past 2", StartUtc = now.AddDays(-14), EndUtc = now.AddDays(-1), StartDateUtc = now.AddDays(-14).UtcDateTime, EndDateUtc = now.AddDays(-1).UtcDateTime },
            new SprintEntity { Id = 201, TeamId = 20, Name = "Beta Future 1", Path = @"Beta\Sprint 1", StartUtc = now.AddDays(1), EndUtc = now.AddDays(14), StartDateUtc = now.AddDays(1).UtcDateTime, EndDateUtc = now.AddDays(14).UtcDateTime },
            new SprintEntity { Id = 202, TeamId = 20, Name = "Beta Future 2", Path = @"Beta\Sprint 2", StartUtc = now.AddDays(15), EndUtc = now.AddDays(28), StartDateUtc = now.AddDays(15).UtcDateTime, EndDateUtc = now.AddDays(28).UtcDateTime },
            new SprintEntity { Id = 203, TeamId = 20, Name = "Beta Future 3", Path = @"Beta\Sprint 3", StartUtc = now.AddDays(29), EndUtc = now.AddDays(42), StartDateUtc = now.AddDays(29).UtcDateTime, EndDateUtc = now.AddDays(42).UtcDateTime },
            new SprintEntity { Id = 204, TeamId = 20, Name = "Beta Past 1", Path = @"Beta\Past 1", StartUtc = now.AddDays(-28), EndUtc = now.AddDays(-14), StartDateUtc = now.AddDays(-28).UtcDateTime, EndDateUtc = now.AddDays(-14).UtcDateTime },
            new SprintEntity { Id = 205, TeamId = 20, Name = "Beta Past 2", Path = @"Beta\Past 2", StartUtc = now.AddDays(-14), EndUtc = now.AddDays(-1), StartDateUtc = now.AddDays(-14).UtcDateTime, EndDateUtc = now.AddDays(-1).UtcDateTime });

        _context.SprintMetricsProjections.AddRange(
            new SprintMetricsProjectionEntity { SprintId = 104, ProductId = 1, PlannedCount = 2, PlannedEffort = 20, PlannedStoryPoints = 10, CompletedPbiCount = 1, CompletedPbiEffort = 10, CompletedPbiStoryPoints = 10, DerivedStoryPoints = 0, IncludedUpToRevisionId = 1 },
            new SprintMetricsProjectionEntity { SprintId = 105, ProductId = 1, PlannedCount = 2, PlannedEffort = 24, PlannedStoryPoints = 12, CompletedPbiCount = 1, CompletedPbiEffort = 12, CompletedPbiStoryPoints = 12, DerivedStoryPoints = 0, IncludedUpToRevisionId = 1 },
            new SprintMetricsProjectionEntity { SprintId = 204, ProductId = 2, PlannedCount = 1, PlannedEffort = 8, PlannedStoryPoints = 4, CompletedPbiCount = 1, CompletedPbiEffort = 4, CompletedPbiStoryPoints = 4, DerivedStoryPoints = 0, IncludedUpToRevisionId = 1 },
            new SprintMetricsProjectionEntity { SprintId = 205, ProductId = 2, PlannedCount = 1, PlannedEffort = 12, PlannedStoryPoints = 6, CompletedPbiCount = 1, CompletedPbiEffort = 6, CompletedPbiStoryPoints = 6, DerivedStoryPoints = 0, IncludedUpToRevisionId = 1 });

        _context.SaveChanges();
    }

    private static IEnumerable<WorkItemDto> BuildWorkItems()
    {
        var now = DateTimeOffset.UtcNow;

        return
        [
            new WorkItemDto(100, "Objective", "Alpha Root", null, "Alpha", @"Alpha\Backlog", "Active", now, null, null),
            new WorkItemDto(101, "Epic", "Alpha Epic", 100, "Alpha", @"Alpha\Backlog", "Active", now, null, null, Tags: "roadmap"),
            new WorkItemDto(111, "Product Backlog Item", "Alpha Planned", 101, "Alpha", @"Alpha\Sprint 1", "Active", now, 30, null),
            new WorkItemDto(112, "Product Backlog Item", "Alpha Unplanned", 101, "Alpha", @"Alpha\Backlog", "Active", now, 10, null),

            new WorkItemDto(200, "Objective", "Beta Root", null, "Beta", @"Beta\Backlog", "Active", now, null, null),
            new WorkItemDto(201, "Epic", "Beta Epic", 200, "Beta", @"Beta\Backlog", "Active", now, null, null, Tags: "roadmap"),
            new WorkItemDto(211, "PBI", "Beta Planned", 201, "Beta", @"Beta\Sprint 1", "Active", now, 20, null),
            new WorkItemDto(212, "PBI", "Beta Done", 201, "Beta", @"Beta\Done", "Closed", now, 5, null)
        ];
    }
}
