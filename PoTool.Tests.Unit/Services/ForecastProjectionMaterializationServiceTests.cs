using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Forecasting.Components.DeliveryForecast;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Shared.Settings;
using PoTool.Tests.Unit.TestSupport;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ForecastProjectionMaterializationServiceTests
{
    [TestMethod]
    public async Task ComputeProjectionsAsync_PersistsForecastVariantsForScopedEpic()
    {
        await using var context = CreateContext();
        SeedForecastingData(context);

        var stateClassificationService = new Mock<IWorkItemStateClassificationService>(MockBehavior.Strict);
        stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetStateClassificationsResponse
            {
                ProjectName = "Project",
                IsDefault = false,
                Classifications =
                [
                    new WorkItemStateClassificationDto { WorkItemType = "Epic", StateName = "Active", Classification = StateClassification.InProgress },
                    new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "Done", Classification = StateClassification.Done },
                    new WorkItemStateClassificationDto { WorkItemType = "Product Backlog Item", StateName = "Active", Classification = StateClassification.InProgress }
                ]
            });

        var hierarchy = new HierarchyRollupService(new CanonicalStoryPointResolutionService());
        var service = new ForecastProjectionMaterializationService(
            context,
            hierarchy,
            stateClassificationService.Object,
            new DeliveryForecastProjector(),
            NullLogger<ForecastProjectionMaterializationService>.Instance);

        var result = await service.ComputeProjectionsAsync(1);

        Assert.HasCount(1, result);

        var entity = await context.ForecastProjections.SingleAsync();
        Assert.AreEqual(1, entity.WorkItemId);
        Assert.AreEqual("Epic", entity.WorkItemType);
        Assert.IsFalse(string.IsNullOrWhiteSpace(entity.ProjectionVariantsJson));
        Assert.AreEqual(3, entity.SprintsRemaining);
        Assert.AreEqual(nameof(ForecastConfidenceLevel.Low), entity.Confidence);

        var variants = System.Text.Json.JsonSerializer.Deserialize<List<StoredProjectionVariant>>(
            entity.ProjectionVariantsJson,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.IsNotNull(variants);
        Assert.HasCount(20, variants);
        Assert.AreEqual(5, variants[4].MaxSprintsForVelocity);
        Assert.AreEqual(13d, variants[4].RemainingScopeStoryPoints, 0.001);
        Assert.AreEqual(6d, variants[4].EstimatedVelocity, 0.001);
        Assert.AreEqual(3, variants[4].SprintsRemaining);

        var cacheState = await context.ProductOwnerCacheStates.SingleAsync();
        Assert.IsTrue(cacheState.ForecastProjectionAsOfUtc.HasValue);

        stateClassificationService.VerifyAll();
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"ForecastProjectionMaterializer_{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }

    private static void SeedForecastingData(PoToolDbContext context)
    {
        PersistenceTestGraph.EnsureProfile(context, 1, "PO");
        PersistenceTestGraph.EnsureProject(context, "Project", "project", "Project");
        var team = new TeamEntity
        {
            Id = 10,
            Name = "Team A",
            TeamAreaPath = "Area"
        };
        var product = new ProductEntity
        {
            Id = 20,
            ProductOwnerId = 1,
            Name = "Product A",
            ProjectId = "Project"
        };
        var productLink = new ProductTeamLinkEntity
        {
            ProductId = product.Id,
            TeamId = team.Id
        };
        var sprint = new SprintEntity
        {
            Id = 30,
            TeamId = team.Id,
            Path = "Sprint 3",
            Name = "Sprint 3",
            StartUtc = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            StartDateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero),
            EndDateUtc = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc),
            LastSyncedUtc = DateTimeOffset.UtcNow,
            LastSyncedDateUtc = DateTime.UtcNow,
            Team = team
        };

        team.ProductTeamLinks.Add(productLink);
        team.Sprints.Add(sprint);
        product.ProductTeamLinks.Add(productLink);
        productLink.Product = product;
        productLink.Team = team;

        context.Teams.Add(team);
        context.Products.Add(product);
        context.ProductTeamLinks.Add(productLink);
        context.Sprints.Add(sprint);
        context.ProductOwnerCacheStates.Add(new ProductOwnerCacheStateEntity { ProductOwnerId = 1 });

        context.WorkItems.AddRange(
            new WorkItemEntity
            {
                TfsId = 1,
                Type = "Epic",
                Title = "Epic",
                AreaPath = "Area\\Epic",
                IterationPath = "Sprint 3",
                State = "Active",
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                TfsChangedDateUtc = DateTime.UtcNow
            },
            new WorkItemEntity
            {
                TfsId = 2,
                ParentTfsId = 1,
                Type = "Product Backlog Item",
                Title = "Done PBI",
                AreaPath = "Area\\Epic\\Feature",
                IterationPath = "Sprint 3",
                State = "Done",
                StoryPoints = 8,
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                TfsChangedDateUtc = DateTime.UtcNow
            },
            new WorkItemEntity
            {
                TfsId = 3,
                ParentTfsId = 1,
                Type = "Product Backlog Item",
                Title = "Active PBI",
                AreaPath = "Area\\Epic\\Feature",
                IterationPath = "Sprint 3",
                State = "Active",
                StoryPoints = 5,
                RetrievedAt = DateTimeOffset.UtcNow,
                TfsChangedDate = DateTimeOffset.UtcNow,
                TfsChangedDateUtc = DateTime.UtcNow
            });

        context.ResolvedWorkItems.AddRange(
            new ResolvedWorkItemEntity { WorkItemId = 1, WorkItemType = "Epic", ResolvedProductId = 20, ResolvedEpicId = 1, ResolutionStatus = ResolutionStatus.Resolved, LastResolvedAt = DateTimeOffset.UtcNow },
            new ResolvedWorkItemEntity { WorkItemId = 2, WorkItemType = "Product Backlog Item", ResolvedProductId = 20, ResolvedEpicId = 1, ResolutionStatus = ResolutionStatus.Resolved, LastResolvedAt = DateTimeOffset.UtcNow },
            new ResolvedWorkItemEntity { WorkItemId = 3, WorkItemType = "Product Backlog Item", ResolvedProductId = 20, ResolvedEpicId = 1, ResolutionStatus = ResolutionStatus.Resolved, LastResolvedAt = DateTimeOffset.UtcNow });

        context.SprintMetricsProjections.Add(new SprintMetricsProjectionEntity
        {
            SprintId = 30,
            ProductId = 20,
            CompletedPbiStoryPoints = 6,
            LastComputedAt = DateTimeOffset.UtcNow,
            IncludedUpToRevisionId = 0
        });

        context.SaveChanges();
    }

    private sealed record StoredProjectionVariant(
        int MaxSprintsForVelocity,
        int WorkItemId,
        string WorkItemType,
        double TotalScopeStoryPoints,
        double CompletedScopeStoryPoints,
        double RemainingScopeStoryPoints,
        double EstimatedVelocity,
        int SprintsRemaining,
        DateTimeOffset? EstimatedCompletionDate,
        ForecastConfidenceLevel Confidence,
        DateTimeOffset LastUpdated,
        IReadOnlyList<CompletionProjection> ForecastByDate);
}
