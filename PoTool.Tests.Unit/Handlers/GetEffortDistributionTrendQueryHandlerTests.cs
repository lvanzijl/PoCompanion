using Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Metrics;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetEffortDistributionTrendQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<ILogger<GetEffortDistributionTrendQueryHandler>> _mockLogger = null!;
    private Mock<IEffortTrendForecastService> _mockEffortTrendForecastService = null!;
    private GetEffortDistributionTrendQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>(MockBehavior.Strict);
        _mockProductRepository = new Mock<IProductRepository>(MockBehavior.Strict);
        _mockMediator = new Mock<IMediator>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<GetEffortDistributionTrendQueryHandler>>();
        _mockEffortTrendForecastService = new Mock<IEffortTrendForecastService>(MockBehavior.Strict);

        _handler = new GetEffortDistributionTrendQueryHandler(
            _mockRepository.Object,
            _mockProductRepository.Object,
            _mockMediator.Object,
            _mockEffortTrendForecastService.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_FiltersAreaPathBeforeAnalyzing()
    {
        IReadOnlyList<EffortDistributionWorkItem>? capturedWorkItems = null;

        _mockProductRepository
            .Setup(repository => repository.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ProductDto>());
        _mockRepository
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>
            {
                CreateWorkItem(1, "Project\\TeamA", "Sprint 1", 10),
                CreateWorkItem(2, "Project\\TeamB", "Sprint 2", 20),
                CreateWorkItem(3, "Other\\TeamC", "Sprint 3", 30)
            });
        _mockEffortTrendForecastService
            .Setup(service => service.Analyze(It.IsAny<IReadOnlyList<EffortDistributionWorkItem>>(), 5, 40))
            .Callback<IReadOnlyList<EffortDistributionWorkItem>, int, int?>((workItems, _, _) => capturedWorkItems = workItems)
            .Returns(new EffortDistributionAnalysis(
                trendBySprint: Array.Empty<EffortSprintTrend>(),
                trendByAreaPath: Array.Empty<EffortAreaPathTrend>(),
                overallTrend: EffortForecastDirection.Stable,
                trendSlope: 0,
                forecasts: Array.Empty<EffortDistributionForecast>()));

        var result = await _handler.Handle(
            new GetEffortDistributionTrendQuery(AreaPathFilter: "Project", MaxIterations: 5, DefaultCapacityPerIteration: 40),
            CancellationToken.None);

        Assert.IsNotNull(capturedWorkItems);
        Assert.HasCount(2, capturedWorkItems);
        CollectionAssert.AreEquivalent(
            new[] { "Project\\TeamA", "Project\\TeamB" },
            capturedWorkItems.Select(item => item.AreaPath).ToArray());
        Assert.AreEqual(EffortTrendDirection.Stable, result.OverallTrend);

        _mockProductRepository.VerifyAll();
        _mockRepository.VerifyAll();
        _mockEffortTrendForecastService.VerifyAll();
    }

    [TestMethod]
    public async Task Handle_UsesProductRootLoadingAndMapsAnalysisIntoDto()
    {
        _mockProductRepository
            .Setup(repository => repository.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateProduct(1, backlogRootWorkItemId: 100)]);
        _mockMediator
            .Setup(mediator => mediator.Send(
                It.Is<GetWorkItemsByRootIdsQuery>(query => query.RootIds.SequenceEqual(new[] { 100 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>
            {
                CreateWorkItem(1, "TeamA", "Sprint 1", 10)
            });
        _mockEffortTrendForecastService
            .Setup(service => service.Analyze(It.IsAny<IReadOnlyList<EffortDistributionWorkItem>>(), 10, null))
            .Returns(new EffortDistributionAnalysis(
                trendBySprint:
                [
                    new EffortSprintTrend("Sprint 1", "Sprint 1", totalEffort: 10, workItemCount: 1, utilizationPercentage: 20, changeFromPrevious: 0, direction: EffortForecastDirection.Increasing)
                ],
                trendByAreaPath:
                [
                    new EffortAreaPathTrend("TeamA", effortBySprint: [10], averageEffort: 10, standardDeviation: 0, direction: EffortForecastDirection.Stable, trendSlope: 0)
                ],
                overallTrend: EffortForecastDirection.Increasing,
                trendSlope: 1.5,
                forecasts:
                [
                    new EffortDistributionForecast("Sprint 2", forecastedEffort: 12, lowEstimate: 10, highEstimate: 14, confidenceLevel: 0.8)
                ]));

        var result = await _handler.Handle(new GetEffortDistributionTrendQuery(), CancellationToken.None);

        Assert.AreEqual(EffortTrendDirection.Increasing, result.OverallTrend);
        Assert.AreEqual(1.5d, result.TrendSlope, 0.001);
        Assert.HasCount(1, result.TrendBySprint);
        Assert.AreEqual("Sprint 1", result.TrendBySprint[0].IterationPath);
        Assert.AreEqual(EffortTrendDirection.Increasing, result.TrendBySprint[0].Direction);
        Assert.HasCount(1, result.TrendByAreaPath);
        Assert.AreEqual("TeamA", result.TrendByAreaPath[0].AreaPath);
        Assert.AreEqual(EffortTrendDirection.Stable, result.TrendByAreaPath[0].Direction);
        Assert.HasCount(1, result.Forecasts);
        Assert.AreEqual("Sprint 2", result.Forecasts[0].SprintName);
        Assert.AreEqual(12, result.Forecasts[0].ForecastedEffort);
        Assert.AreEqual(0.8d, result.Forecasts[0].ConfidenceLevel, 0.001);

        _mockRepository.Verify(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockProductRepository.VerifyAll();
        _mockMediator.VerifyAll();
        _mockEffortTrendForecastService.VerifyAll();
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

    private static WorkItemDto CreateWorkItem(int id, string areaPath, string iterationPath, int effort)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: "Task",
            Title: $"Work Item {id}",
            ParentTfsId: null,
            AreaPath: areaPath,
            IterationPath: iterationPath,
            State: "New",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
            Description: null,
            Tags: null);
    }
}
