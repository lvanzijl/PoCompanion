using Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.EffortPlanning;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.Settings.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetEffortEstimationSuggestionsQueryHandlerTests
{
    private Mock<IWorkItemRepository> _repository = null!;
    private Mock<IProductRepository> _productRepository = null!;
    private Mock<IMediator> _mediator = null!;
    private Mock<IWorkItemStateClassificationService> _stateClassificationService = null!;
    private Mock<IEffortEstimationSuggestionService> _effortEstimationSuggestionService = null!;
    private GetEffortEstimationSuggestionsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _repository = new Mock<IWorkItemRepository>(MockBehavior.Strict);
        _productRepository = new Mock<IProductRepository>(MockBehavior.Strict);
        _mediator = new Mock<IMediator>(MockBehavior.Strict);
        _stateClassificationService = new Mock<IWorkItemStateClassificationService>(MockBehavior.Strict);
        _effortEstimationSuggestionService = new Mock<IEffortEstimationSuggestionService>(MockBehavior.Strict);

        _productRepository
            .Setup(repository => repository.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mediator
            .Setup(mediator => mediator.Send(It.IsAny<GetEffortEstimationSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EffortEstimationSettingsDto.Default);

        _handler = new GetEffortEstimationSuggestionsQueryHandler(
            _repository.Object,
            _productRepository.Object,
            _mediator.Object,
            _stateClassificationService.Object,
            _effortEstimationSuggestionService.Object,
            Mock.Of<ILogger<GetEffortEstimationSuggestionsQueryHandler>>());
    }

    [TestMethod]
    public async Task Handle_FiltersCandidatesAndHistoricalData_ThenMapsSuggestionResult()
    {
        var now = DateTimeOffset.UtcNow;
        List<WorkItemDto> workItems =
        [
            CreateWorkItem(1, "Task", "Done", "Team\\A", "Sprint 1", 3, now.AddDays(-3)),
            CreateWorkItem(2, "Task", "Closed", "Team\\A", "Sprint 1", 5, now.AddDays(-2)),
            CreateWorkItem(3, "Task", "New", "Team\\A", "Sprint 2", null, now.AddDays(-1)),
            CreateWorkItem(4, "Task", "In Progress", "Team\\A", "Sprint 2", null, now)
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
        _effortEstimationSuggestionService
            .Setup(service => service.GenerateSuggestion(
                It.Is<EffortPlanningWorkItem>(item => item.WorkItemId == 4 && item.State == "In Progress"),
                It.Is<IReadOnlyList<EffortPlanningWorkItem>>(history =>
                    history.Count == 2
                    && history.All(item => item.Effort.HasValue && item.Effort.Value > 0)),
                It.Is<EffortEstimationSettingsDto>(settings => settings.DefaultEffortTask == 3)))
            .Returns(new EffortEstimationSuggestionResult(
                WorkItemId: 4,
                WorkItemTitle: "Work item 4",
                WorkItemType: "Task",
                CurrentEffort: null,
                SuggestedEffort: 4,
                Confidence: 0.65d,
                HistoricalMatchCount: 2,
                HistoricalEffortMin: 3,
                HistoricalEffortMax: 5,
                SimilarWorkItems:
                [
                    new EffortHistoricalExampleResult(2, "Work item 2", 5, "Closed", 0.9d)
                ]));

        var result = await _handler.Handle(new GetEffortEstimationSuggestionsQuery(), CancellationToken.None);

        Assert.HasCount(1, result);
        Assert.AreEqual(4, result[0].WorkItemId);
        Assert.AreEqual(4, result[0].SuggestedEffort);
        Assert.AreEqual(0.65d, result[0].Confidence, 0.001d);
        Assert.AreEqual("Task items typically range from 3-5 points, median 4 (based on 2 completed items)", result[0].Rationale);
        Assert.HasCount(1, result[0].SimilarWorkItems);
        Assert.AreEqual(2, result[0].SimilarWorkItems[0].WorkItemId);

        _effortEstimationSuggestionService.VerifyAll();
    }

    [TestMethod]
    public async Task Handle_MapsConfiguredDefaultSuggestionToFallbackRationale()
    {
        var now = DateTimeOffset.UtcNow;
        List<WorkItemDto> workItems =
        [
            CreateWorkItem(1, "Epic", "In Progress", "Team\\A", "Sprint 2", null, now)
        ];

        _repository
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _effortEstimationSuggestionService
            .Setup(service => service.GenerateSuggestion(
                It.Is<EffortPlanningWorkItem>(item => item.WorkItemId == 1 && item.WorkItemType == "Epic"),
                It.Is<IReadOnlyList<EffortPlanningWorkItem>>(history => history.Count == 0),
                It.Is<EffortEstimationSettingsDto>(settings => settings.DefaultEffortEpic == EffortEstimationSettingsDto.Default.DefaultEffortEpic)))
            .Returns(new EffortEstimationSuggestionResult(
                WorkItemId: 1,
                WorkItemTitle: "Work item 1",
                WorkItemType: "Epic",
                CurrentEffort: null,
                SuggestedEffort: EffortEstimationSettingsDto.Default.DefaultEffortEpic,
                Confidence: 0.3d,
                HistoricalMatchCount: 0,
                HistoricalEffortMin: EffortEstimationSettingsDto.Default.DefaultEffortEpic,
                HistoricalEffortMax: EffortEstimationSettingsDto.Default.DefaultEffortEpic,
                SimilarWorkItems: []));

        var result = await _handler.Handle(new GetEffortEstimationSuggestionsQuery(), CancellationToken.None);

        Assert.HasCount(1, result);
        Assert.AreEqual("No historical data available. Using configured default Epic estimate.", result[0].Rationale);

        _effortEstimationSuggestionService.VerifyAll();
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
