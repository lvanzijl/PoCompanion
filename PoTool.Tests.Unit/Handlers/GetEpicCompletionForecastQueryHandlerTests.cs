using Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Metrics;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetEpicCompletionForecastQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<IWorkItemStateClassificationService> _mockStateService = null!;
    private Mock<IHierarchyRollupService> _mockHierarchyRollupService = null!;
    private Mock<ICompletionForecastService> _mockCompletionForecastService = null!;
    private Mock<ILogger<GetEpicCompletionForecastQueryHandler>> _mockLogger = null!;
    private GetEpicCompletionForecastQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>(MockBehavior.Strict);
        _mockProductRepository = new Mock<IProductRepository>(MockBehavior.Strict);
        _mockMediator = new Mock<IMediator>(MockBehavior.Strict);
        _mockStateService = new Mock<IWorkItemStateClassificationService>(MockBehavior.Strict);
        _mockHierarchyRollupService = new Mock<IHierarchyRollupService>(MockBehavior.Strict);
        _mockCompletionForecastService = new Mock<ICompletionForecastService>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GetEpicCompletionForecastQueryHandler>>();

        _handler = new GetEpicCompletionForecastQueryHandler(
            _mockRepository.Object,
            _mockProductRepository.Object,
            _mockMediator.Object,
            _mockStateService.Object,
            _mockHierarchyRollupService.Object,
            _mockCompletionForecastService.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNonExistentEpic_ReturnsNull()
    {
        _mockRepository
            .Setup(repository => repository.GetByTfsIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);

        var result = await _handler.Handle(new GetEpicCompletionForecastQuery(999), CancellationToken.None);

        Assert.IsNull(result);
        _mockRepository.VerifyAll();
    }

    [TestMethod]
    public async Task Handle_UsesProductRootLoadingAndMapsForecastDto()
    {
        var epic = CreateWorkItem(1, "Epic", "In Progress", "Area\\Epic", "Sprint 3", null, storyPoints: null);
        var child = CreateWorkItem(2, "PBI", "Done", "Area\\Epic\\Feature", "Sprint 3", 1, storyPoints: 8);
        var products = new[]
        {
            CreateProduct(10, backlogRootWorkItemId: 500)
        };
        var deliveryForecast = new DeliveryForecast(
            totalScopeStoryPoints: 21,
            completedScopeStoryPoints: 8,
            remainingScopeStoryPoints: 13,
            estimatedVelocity: 6,
            sprintsRemaining: 3,
            estimatedCompletionDate: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            confidence: ForecastConfidenceLevel.Medium,
            projections:
            [
                new CompletionProjection(
                    sprintName: "Sprint 4",
                    iterationPath: "Sprint 4",
                    sprintStartDate: new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
                    sprintEndDate: new DateTimeOffset(2026, 4, 14, 0, 0, 0, TimeSpan.Zero),
                    expectedCompletedStoryPoints: 6,
                    remainingStoryPointsAfterSprint: 7,
                    progressPercentage: 66.6)
            ]);

        _mockRepository
            .Setup(repository => repository.GetByTfsIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(epic);
        _mockProductRepository
            .Setup(repository => repository.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);
        _mockMediator
            .Setup(mediator => mediator.Send(
                It.Is<GetWorkItemsByRootIdsQuery>(query => query.RootIds.SequenceEqual(new[] { 500 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> { child });
        _mockStateService
            .Setup(service => service.IsDoneStateAsync("PBI", "Done", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockStateService
            .Setup(service => service.IsDoneStateAsync("Epic", "In Progress", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockHierarchyRollupService
            .Setup(service => service.RollupCanonicalScope(
                It.Is<PoTool.Core.Domain.Models.CanonicalWorkItem>(workItem => workItem.WorkItemId == 1),
                It.Is<IReadOnlyList<PoTool.Core.Domain.Models.CanonicalWorkItem>>(workItems => workItems.Select(item => item.WorkItemId).OrderBy(id => id).SequenceEqual(new[] { 1, 2 })),
                It.Is<IReadOnlyDictionary<int, bool>>(doneLookup => !doneLookup[1] && doneLookup[2])))
            .Returns(new HierarchyScopeRollup(Total: 21, Completed: 8));
        _mockMediator
            .Setup(mediator => mediator.Send(
                It.Is<GetSprintMetricsQuery>(query => query.IterationPath == "Sprint 3"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SprintMetricsDto(
                IterationPath: "Sprint 3",
                SprintName: "Sprint 3",
                StartDate: DateTimeOffset.UtcNow.AddDays(-14),
                EndDate: DateTimeOffset.UtcNow.AddDays(-1),
                CompletedStoryPoints: 6,
                PlannedStoryPoints: 8,
                CompletedWorkItemCount: 1,
                TotalWorkItemCount: 1,
                CompletedPBIs: 1,
                CompletedBugs: 0,
                CompletedTasks: 0));
        _mockCompletionForecastService
            .Setup(service => service.Forecast(
                totalScopeStoryPoints: 21,
                completedScopeStoryPoints: 8,
                It.Is<IReadOnlyList<HistoricalVelocitySample>>(samples =>
                    samples.Count == 1 &&
                    samples[0].SprintName == "Sprint 3" &&
                    samples[0].CompletedStoryPoints == 6)))
            .Returns(deliveryForecast);

        var result = await _handler.Handle(new GetEpicCompletionForecastQuery(1), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.EpicId);
        Assert.AreEqual(epic.Title, result.Title);
        Assert.AreEqual(epic.Type, result.Type);
        Assert.AreEqual(21d, result.TotalStoryPoints, 0.001);
        Assert.AreEqual(8d, result.DoneStoryPoints, 0.001);
        Assert.AreEqual(8d, result.DeliveredStoryPoints, 0.001);
        Assert.AreEqual(13d, result.RemainingStoryPoints, 0.001);
        Assert.AreEqual(6d, result.EstimatedVelocity, 0.001);
        Assert.AreEqual(3, result.SprintsRemaining);
        Assert.AreEqual(ForecastConfidence.Medium, result.Confidence);
        Assert.AreEqual(epic.AreaPath, result.AreaPath);
        Assert.HasCount(1, result.ForecastByDate);
        Assert.AreEqual("Sprint 4", result.ForecastByDate[0].SprintName);
        Assert.AreEqual(6d, result.ForecastByDate[0].ExpectedCompletedStoryPoints, 0.001);

        _mockRepository.Verify(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockRepository.VerifyAll();
        _mockProductRepository.VerifyAll();
        _mockMediator.VerifyAll();
        _mockStateService.VerifyAll();
        _mockHierarchyRollupService.VerifyAll();
        _mockCompletionForecastService.VerifyAll();
    }

    private static ProductDto CreateProduct(int id, int backlogRootWorkItemId)
    {
        return new ProductDto(
            Id: id,
            ProductOwnerId: 1,
            Name: $"Product {id}",
            BacklogRootWorkItemIds: [backlogRootWorkItemId],
            Order: id,
            PictureType: ProductPictureType.Default,
            DefaultPictureId: 0,
            CustomPicturePath: null,
            CreatedAt: DateTimeOffset.UtcNow,
            LastModified: DateTimeOffset.UtcNow,
            LastSyncedAt: null,
            TeamIds: [],
            Repositories: []);
    }

    private static WorkItemDto CreateWorkItem(
        int id,
        string type,
        string state,
        string areaPath,
        string iterationPath,
        int? parentTfsId,
        int? storyPoints)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Work Item {id}",
            ParentTfsId: parentTfsId,
            AreaPath: areaPath,
            IterationPath: iterationPath,
            State: state,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null,
            Tags: null,
            BusinessValue: null,
            StoryPoints: storyPoints);
    }
}
