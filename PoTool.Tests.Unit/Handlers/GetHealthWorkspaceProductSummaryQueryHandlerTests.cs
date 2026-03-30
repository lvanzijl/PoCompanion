using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Health;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetHealthWorkspaceProductSummaryQueryHandlerTests
{
    private Mock<IWorkItemQuery> _workItemQuery = null!;
    private Mock<IWorkItemStateClassificationService> _stateClassificationService = null!;
    private Mock<ILogger<GetHealthWorkspaceProductSummaryQueryHandler>> _logger = null!;
    private BacklogStateComputationService _computationService = null!;
    private GetHealthWorkspaceProductSummaryQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _workItemQuery = new Mock<IWorkItemQuery>();
        _stateClassificationService = new Mock<IWorkItemStateClassificationService>();
        _logger = new Mock<ILogger<GetHealthWorkspaceProductSummaryQueryHandler>>();
        _computationService = new BacklogStateComputationService();

        _stateClassificationService
            .Setup(service => service.GetClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetStateClassificationsResponse
            {
                ProjectName = "Test",
                IsDefault = false,
                Classifications =
                [
                    new WorkItemStateClassificationDto
                    {
                        WorkItemType = WorkItemType.Epic,
                        StateName = "Done",
                        Classification = StateClassification.Done
                    },
                    new WorkItemStateClassificationDto
                    {
                        WorkItemType = WorkItemType.Feature,
                        StateName = "Done",
                        Classification = StateClassification.Done
                    },
                    new WorkItemStateClassificationDto
                    {
                        WorkItemType = WorkItemType.Pbi,
                        StateName = "Done",
                        Classification = StateClassification.Done
                    },
                    new WorkItemStateClassificationDto
                    {
                        WorkItemType = WorkItemType.Pbi,
                        StateName = "Removed",
                        Classification = StateClassification.Removed
                    }
                ]
            });

        _handler = new GetHealthWorkspaceProductSummaryQueryHandler(
            _workItemQuery.Object,
            _computationService,
            _stateClassificationService.Object,
            _logger.Object);
    }

    [TestMethod]
    public async Task Handle_WithReadyAndPendingEpics_ReturnsDashboardSummary()
    {
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1000, WorkItemType.Epic, "Ready epic", state: "Active", description: "ready epic"),
            CreateWorkItem(1001, WorkItemType.Feature, "Ready feature", state: "Active", parentId: 1000, description: "feature"),
            CreateWorkItem(1002, WorkItemType.Pbi, "Ready pbi", state: "Active", parentId: 1001, description: "pbi", effort: 8),
            CreateWorkItem(1003, WorkItemType.Epic, "Pending epic", state: "Active", description: "pending epic"),
            CreateWorkItem(1004, WorkItemType.Feature, "Pending but ready feature", state: "Active", parentId: 1003, description: "feature"),
            CreateWorkItem(1005, WorkItemType.Pbi, "Pending ready pbi", state: "Active", parentId: 1004, description: "pbi", effort: 5),
            CreateWorkItem(1006, WorkItemType.Feature, "Incomplete feature", state: "Active", parentId: 1003, description: "feature"),
            CreateWorkItem(1007, WorkItemType.Pbi, "Missing effort", state: "Active", parentId: 1006, description: "pbi", effort: null),
            CreateWorkItem(1008, WorkItemType.Pbi, "Done pbi", state: "Done", parentId: 1001, description: "done pbi", effort: 13),
            CreateWorkItem(1009, WorkItemType.Pbi, "Removed pbi", state: "Removed", parentId: 1004, description: "removed pbi", effort: 21)
        };

        _workItemQuery
            .Setup(provider => provider.GetProductBacklogAnalyticsSourceAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductBacklogAnalyticsSource(1, items));

        var result = await _handler.Handle(new GetHealthWorkspaceProductSummaryQuery(1), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.ProductId);
        Assert.AreEqual(8, result.ReadyEffort);
        Assert.AreEqual(1, result.FeaturesReadyInPendingEpics);
        Assert.HasCount(2, result.TopEpics);
        Assert.AreEqual("Ready epic", result.TopEpics[0].Title);
        Assert.AreEqual(100, result.TopEpics[0].Score);
        Assert.AreEqual(8, result.TopEpics[0].Effort);
        Assert.AreEqual("Pending epic", result.TopEpics[1].Title);
        Assert.AreEqual(88, result.TopEpics[1].Score);
        Assert.AreEqual(5, result.TopEpics[1].Effort);
    }

    [TestMethod]
    public async Task Handle_WithUnknownProduct_ReturnsNull()
    {
        _workItemQuery
            .Setup(provider => provider.GetProductBacklogAnalyticsSourceAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductBacklogAnalyticsSource?)null);

        var result = await _handler.Handle(new GetHealthWorkspaceProductSummaryQuery(99), CancellationToken.None);

        Assert.IsNull(result);
        _workItemQuery.Verify(
            provider => provider.GetProductBacklogAnalyticsSourceAsync(99, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static WorkItemDto CreateWorkItem(
        int tfsId,
        string type,
        string title,
        string state,
        int? parentId = null,
        string? description = null,
        int? effort = null) =>
        new(
            TfsId: tfsId,
            Type: type,
            Title: title,
            ParentTfsId: parentId,
            AreaPath: "\\Test",
            IterationPath: "\\Sprint 1",
            State: state,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
            Description: description,
            CreatedDate: DateTimeOffset.UtcNow,
            ClosedDate: null,
            Severity: null,
            Tags: string.Empty,
            IsBlocked: null,
            Relations: null,
            ChangedDate: DateTimeOffset.UtcNow,
            BusinessValue: null,
            BacklogPriority: null);
}
