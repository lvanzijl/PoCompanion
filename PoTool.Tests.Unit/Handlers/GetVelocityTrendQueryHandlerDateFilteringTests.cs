using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Core.Contracts;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;
using PoTool.Shared.Settings;
using Mediator;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Handlers;

/// <summary>
/// Tests for GetVelocityTrendQueryHandler 6-month date filtering.
/// Validates that sprints older than 6 months are filtered out.
/// </summary>
[TestClass]
public class GetVelocityTrendQueryHandlerDateFilteringTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<ILogger<GetVelocityTrendQueryHandler>> _mockLogger = null!;
    private GetVelocityTrendQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<GetVelocityTrendQueryHandler>>();
        _handler = new GetVelocityTrendQueryHandler(
            _mockRepository.Object,
            _mockProductRepository.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithSprintsOlderThan6Months_FiltersOldSprints()
    {
        // Arrange
        var product = CreateProduct(1, 1, "Product A", 100);
        var now = DateTimeOffset.UtcNow;
        var sevenMonthsAgo = now.AddMonths(-7);
        var fiveMonthsAgo = now.AddMonths(-5);

        var workItems = new List<WorkItemDto>
        {
            // Items in recent sprint
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            // Items in old sprint
            CreateWorkItem(102, "Story", "Done", "Sprint 2", 3, null, 100)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        
        // Mock GetWorkItemsByRootIdsQuery to return work items
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        // Mock GetSprintMetricsQuery to return different dates
        _mockMediator.Setup(m => m.Send(
            It.Is<GetSprintMetricsQuery>(q => q.IterationPath == "Sprint 1"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SprintMetricsDto(
                IterationPath: "Sprint 1",
                SprintName: "Sprint 1",
                StartDate: fiveMonthsAgo.AddDays(-14),
                EndDate: fiveMonthsAgo, // Within 6-month window
                CompletedStoryPoints: 5,
                PlannedStoryPoints: 5,
                CompletedWorkItemCount: 1,
                TotalWorkItemCount: 1,
                CompletedPBIs: 1,
                CompletedBugs: 0,
                CompletedTasks: 0
            ));

        _mockMediator.Setup(m => m.Send(
            It.Is<GetSprintMetricsQuery>(q => q.IterationPath == "Sprint 2"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SprintMetricsDto(
                IterationPath: "Sprint 2",
                SprintName: "Sprint 2",
                StartDate: sevenMonthsAgo.AddDays(-14),
                EndDate: sevenMonthsAgo, // Outside 6-month window
                CompletedStoryPoints: 3,
                PlannedStoryPoints: 3,
                CompletedWorkItemCount: 1,
                TotalWorkItemCount: 1,
                CompletedPBIs: 1,
                CompletedBugs: 0,
                CompletedTasks: 0
            ));

        var query = new GetVelocityTrendQuery(ProductIds: new[] { 1 }, MaxSprints: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalSprints, "Should only include sprints within 6-month window");
        Assert.AreEqual(5, result.TotalCompletedStoryPoints, "Should only count story points from recent sprint");
        Assert.AreEqual("Sprint 1", result.Sprints.First().SprintName);
    }

    [TestMethod]
    public async Task Handle_WithSprintsWithNullEndDate_IncludesSprintsWithNullEndDate()
    {
        // Arrange
        var product = CreateProduct(1, 1, "Product A", 100);
        var now = DateTimeOffset.UtcNow;
        var sevenMonthsAgo = now.AddMonths(-7);

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            CreateWorkItem(102, "Story", "Done", "Sprint 2", 3, null, 100)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        
        // Mock GetWorkItemsByRootIdsQuery to return work items
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        // Sprint 1 has null end date (should be included)
        _mockMediator.Setup(m => m.Send(
            It.Is<GetSprintMetricsQuery>(q => q.IterationPath == "Sprint 1"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SprintMetricsDto(
                IterationPath: "Sprint 1",
                SprintName: "Sprint 1",
                StartDate: null,
                EndDate: null, // Null end date - should be included
                CompletedStoryPoints: 5,
                PlannedStoryPoints: 5,
                CompletedWorkItemCount: 1,
                TotalWorkItemCount: 1,
                CompletedPBIs: 1,
                CompletedBugs: 0,
                CompletedTasks: 0
            ));

        // Sprint 2 is old (should be excluded)
        _mockMediator.Setup(m => m.Send(
            It.Is<GetSprintMetricsQuery>(q => q.IterationPath == "Sprint 2"),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SprintMetricsDto(
                IterationPath: "Sprint 2",
                SprintName: "Sprint 2",
                StartDate: sevenMonthsAgo.AddDays(-14),
                EndDate: sevenMonthsAgo,
                CompletedStoryPoints: 3,
                PlannedStoryPoints: 3,
                CompletedWorkItemCount: 1,
                TotalWorkItemCount: 1,
                CompletedPBIs: 1,
                CompletedBugs: 0,
                CompletedTasks: 0
            ));

        var query = new GetVelocityTrendQuery(ProductIds: new[] { 1 }, MaxSprints: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalSprints, "Should include sprint with null end date");
        Assert.AreEqual(5, result.TotalCompletedStoryPoints);
        Assert.AreEqual("Sprint 1", result.Sprints.First().SprintName);
    }

    [TestMethod]
    public async Task Handle_WithAllRecentSprints_IncludesAllSprints()
    {
        // Arrange
        var product = CreateProduct(1, 1, "Product A", 100);
        var now = DateTimeOffset.UtcNow;

        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(101, "Story", "Done", "Sprint 1", 5, null, 100),
            CreateWorkItem(102, "Story", "Done", "Sprint 2", 3, null, 100),
            CreateWorkItem(103, "Story", "Done", "Sprint 3", 8, null, 100)
        };

        _mockProductRepository.Setup(r => r.GetProductByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        
        // Mock GetWorkItemsByRootIdsQuery to return work items
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        // Setup sprint metrics responses using callback to handle different sprints  
        _mockMediator.Setup(m => m.Send(It.IsAny<GetSprintMetricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetSprintMetricsQuery query, CancellationToken ct) =>
            {
                int sprintNum = int.Parse(query.IterationPath.Replace("Sprint ", ""));
                var sprintEndDate = now.AddMonths(-sprintNum);
                
                return new SprintMetricsDto(
                    IterationPath: query.IterationPath,
                    SprintName: query.IterationPath,
                    StartDate: sprintEndDate.AddDays(-14),
                    EndDate: sprintEndDate,
                    CompletedStoryPoints: sprintNum * 2 + 3,
                    PlannedStoryPoints: sprintNum * 2 + 3,
                    CompletedWorkItemCount: 1,
                    TotalWorkItemCount: 1,
                    CompletedPBIs: 1,
                    CompletedBugs: 0,
                    CompletedTasks: 0
                );
            });

        var query = new GetVelocityTrendQuery(ProductIds: new[] { 1 }, MaxSprints: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.TotalSprints, "Should include all recent sprints");
        Assert.AreEqual(21, result.TotalCompletedStoryPoints); // (1*2+3) + (2*2+3) + (3*2+3) = 5 + 7 + 9 = 21
    }

    // Helper methods
    private static ProductDto CreateProduct(int id, int ownerId, string name, int rootWorkItemId)
    {
        return new ProductDto(
            Id: id,
            ProductOwnerId: ownerId,
            Name: name,
            BacklogRootWorkItemId: rootWorkItemId,
            Order: 0,
            PictureType: ProductPictureType.Default,
            DefaultPictureId: 0,
            CustomPicturePath: null,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow,
            LastSyncedAt: null,
            TeamIds: new List<int>(),
            Repositories: new List<RepositoryDto>()
        );
    }

    private static WorkItemDto CreateWorkItem(
        int tfsId,
        string type,
        string state,
        string iterationPath,
        int? effort,
        int? originalEstimate = null,
        int? parentTfsId = null)
    {
        return new WorkItemDto(
            TfsId: tfsId,
            Type: type,
            Title: $"Work Item {tfsId}",
            ParentTfsId: parentTfsId,
            AreaPath: "MyProject",
            IterationPath: iterationPath,
            State: state,
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
            Description: null,
            Tags: null
        );
    }
}
