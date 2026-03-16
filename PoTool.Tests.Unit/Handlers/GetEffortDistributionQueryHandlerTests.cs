using Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.EffortPlanning;
using PoTool.Core.Metrics.Queries;
using PoTool.Shared.Metrics;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetEffortDistributionQueryHandlerTests
{
    private Mock<IWorkItemRepository> _repository = null!;
    private Mock<IProductRepository> _productRepository = null!;
    private Mock<IMediator> _mediator = null!;
    private Mock<IEffortDistributionService> _effortDistributionService = null!;
    private GetEffortDistributionQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _repository = new Mock<IWorkItemRepository>(MockBehavior.Strict);
        _productRepository = new Mock<IProductRepository>(MockBehavior.Strict);
        _mediator = new Mock<IMediator>(MockBehavior.Strict);
        _effortDistributionService = new Mock<IEffortDistributionService>(MockBehavior.Strict);

        _productRepository
            .Setup(repository => repository.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _handler = new GetEffortDistributionQueryHandler(
            _repository.Object,
            _productRepository.Object,
            _mediator.Object,
            _effortDistributionService.Object,
            Mock.Of<ILogger<GetEffortDistributionQueryHandler>>());
    }

    [TestMethod]
    public async Task Handle_DelegatesFilteredWorkItemsToCdcService_AndMapsCanonicalResult()
    {
        List<WorkItemDto> workItems =
        [
            CreateWorkItem(1, "Project\\Team A", "Sprint 1", 5),
            CreateWorkItem(2, "Project\\Team A\\Sub", "Sprint 1", 8),
            CreateWorkItem(3, "Project\\Team B", "Sprint 2", 13)
        ];

        _repository
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _effortDistributionService
            .Setup(service => service.Analyze(
                It.Is<IReadOnlyList<EffortPlanningWorkItem>>(items =>
                    items.Count == 2
                    && items.All(item => item.AreaPath.StartsWith("Project\\Team A", StringComparison.OrdinalIgnoreCase))
                    && items.Sum(item => item.Effort ?? 0) == 13),
                5,
                50))
            .Returns(new EffortDistributionResult(
                EffortByArea:
                [
                    new EffortAreaDistributionResult("Project\\Team A", 13, 2, 6.5d)
                ],
                EffortByIteration:
                [
                    new EffortIterationDistributionResult("Sprint 1", "Sprint 1", 13, 2, 50, 26d)
                ],
                HeatMapData:
                [
                    new EffortHeatMapCellResult("Project\\Team A", "Sprint 1", 13, 2, CapacityStatus.Underutilized)
                ],
                TotalEffort: 13));

        var result = await _handler.Handle(
            new GetEffortDistributionQuery(AreaPathFilter: "Project\\Team A", MaxIterations: 5, DefaultCapacityPerIteration: 50),
            CancellationToken.None);

        Assert.AreEqual(13, result.TotalEffort);
        Assert.HasCount(1, result.EffortByArea);
        Assert.AreEqual("Project\\Team A", result.EffortByArea[0].AreaPath);
        Assert.AreEqual(6.5d, result.EffortByArea[0].AverageEffortPerItem, 0.001d);
        Assert.HasCount(1, result.EffortByIteration);
        Assert.AreEqual(26d, result.EffortByIteration[0].UtilizationPercentage, 0.001d);
        Assert.HasCount(1, result.HeatMapData);
        Assert.AreEqual(CapacityStatus.Underutilized, result.HeatMapData[0].Status);

        _effortDistributionService.VerifyAll();
    }

    [TestMethod]
    public async Task Handle_WithNoEligibleEffortItems_StillMapsEmptyCanonicalResult()
    {
        _repository
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>
            {
                CreateWorkItem(1, "Area", "Sprint 1", null),
                CreateWorkItem(2, "Area", "Sprint 1", 0)
            });
        _effortDistributionService
            .Setup(service => service.Analyze(
                It.Is<IReadOnlyList<EffortPlanningWorkItem>>(items => items.Count == 0),
                10,
                null))
            .Returns(new EffortDistributionResult([], [], [], 0));

        var result = await _handler.Handle(new GetEffortDistributionQuery(), CancellationToken.None);

        Assert.AreEqual(0, result.TotalEffort);
        Assert.IsEmpty(result.EffortByArea);
        Assert.IsEmpty(result.EffortByIteration);
        Assert.IsEmpty(result.HeatMapData);

        _effortDistributionService.VerifyAll();
    }

    private static WorkItemDto CreateWorkItem(int id, string areaPath, string iterationPath, int? effort)
    {
        return new WorkItemDto(
            id,
            "Task",
            $"Work item {id}",
            ParentTfsId: null,
            areaPath,
            iterationPath,
            "New",
            DateTimeOffset.UtcNow,
            effort,
            Description: null,
            Tags: null);
    }
}
