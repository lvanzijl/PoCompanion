using Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.EffortPlanning;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetEffortEstimationQualityQueryHandlerTests
{
    private Mock<IWorkItemRepository> _repository = null!;
    private Mock<IProductRepository> _productRepository = null!;
    private Mock<IMediator> _mediator = null!;
    private Mock<IWorkItemStateClassificationService> _stateClassificationService = null!;
    private Mock<IEffortEstimationQualityService> _effortEstimationQualityService = null!;
    private GetEffortEstimationQualityQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _repository = new Mock<IWorkItemRepository>(MockBehavior.Strict);
        _productRepository = new Mock<IProductRepository>(MockBehavior.Strict);
        _mediator = new Mock<IMediator>(MockBehavior.Strict);
        _stateClassificationService = new Mock<IWorkItemStateClassificationService>(MockBehavior.Strict);
        _effortEstimationQualityService = new Mock<IEffortEstimationQualityService>(MockBehavior.Strict);

        _productRepository
            .Setup(repository => repository.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _handler = new GetEffortEstimationQualityQueryHandler(
            _repository.Object,
            _productRepository.Object,
            _mediator.Object,
            _stateClassificationService.Object,
            _effortEstimationQualityService.Object,
            Mock.Of<ILogger<GetEffortEstimationQualityQueryHandler>>());
    }

    [TestMethod]
    public async Task Handle_FiltersCompletedEstimatedItems_AndMapsServiceResult()
    {
        var completedAt = DateTimeOffset.UtcNow;
        List<WorkItemDto> workItems =
        [
            CreateWorkItem(1, "Task", "Done", "Team\\A", "Sprint 1", 5, completedAt.AddDays(-2)),
            CreateWorkItem(2, "Task", "Closed", "Team\\A", "Sprint 1", 8, completedAt.AddDays(-1)),
            CreateWorkItem(3, "Task", "Active", "Team\\A", "Sprint 1", 13, completedAt),
            CreateWorkItem(4, "Task", "Done", "Team\\B", "Sprint 2", null, completedAt)
        ];

        _repository
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _stateClassificationService
            .Setup(service => service.IsDoneStateAsync(It.IsAny<string>(), "Done", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _stateClassificationService
            .Setup(service => service.IsDoneStateAsync(It.IsAny<string>(), "Closed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _stateClassificationService
            .Setup(service => service.IsDoneStateAsync(It.IsAny<string>(), "Active", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _effortEstimationQualityService
            .Setup(service => service.Analyze(
                It.Is<IReadOnlyList<EffortPlanningWorkItem>>(items =>
                    items.Count == 2
                    && items.All(item => item.AreaPath == "Team\\A")
                    && items.All(item => item.Effort.HasValue && item.Effort.Value > 0)),
                3))
            .Returns(new EffortEstimationQualityResult(
                AverageEstimationAccuracy: 0.75d,
                TotalCompletedWorkItems: 2,
                WorkItemsWithEstimates: 2,
                QualityByType:
                [
                    new EffortTypeQualityResult("Task", 2, 0.75d, 5, 8, 7)
                ],
                TrendOverTime:
                [
                    new EffortQualityTrendResult("Sprint 1", completedAt.AddDays(-2), completedAt.AddDays(-1), 0.75d, 2)
                ]));

        var result = await _handler.Handle(new GetEffortEstimationQualityQuery("Team\\A", 3), CancellationToken.None);

        Assert.AreEqual(0.75d, result.AverageEstimationAccuracy, 0.001d);
        Assert.AreEqual(2, result.TotalCompletedWorkItems);
        Assert.AreEqual(2, result.WorkItemsWithEstimates);
        Assert.HasCount(1, result.QualityByType);
        Assert.AreEqual("Task", result.QualityByType[0].WorkItemType);
        Assert.AreEqual(7, result.QualityByType[0].AverageEffort);
        Assert.HasCount(1, result.TrendOverTime);
        Assert.AreEqual("Sprint 1", result.TrendOverTime[0].Period);

        _effortEstimationQualityService.VerifyAll();
    }

    private static WorkItemDto CreateWorkItem(
        int id,
        string type,
        string state,
        string areaPath,
        string iterationPath,
        int? effort,
        DateTimeOffset retrievedAt)
    {
        return new WorkItemDto(
            id,
            type,
            $"Work item {id}",
            ParentTfsId: null,
            areaPath,
            iterationPath,
            state,
            retrievedAt,
            effort,
            Description: null,
            Tags: null);
    }
}
