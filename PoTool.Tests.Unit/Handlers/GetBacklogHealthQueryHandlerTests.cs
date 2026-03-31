using Mediator;
using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Handlers.Metrics;
using PoTool.Api.Services;
using PoTool.Core.BacklogQuality;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.BacklogQuality.Models;
using PoTool.Core.Domain.BacklogQuality.Rules;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.Metrics;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetBacklogHealthQueryHandlerTests
{
    private Mock<IWorkItemReadProvider> _mockProvider = null!;
    private Mock<IProductRepository> _mockProductRepository = null!;
    private Mock<IMediator> _mockMediator = null!;
    private Mock<IBacklogQualityAnalysisService> _mockBacklogQualityAnalysisService = null!;
    private Mock<ILogger<GetBacklogHealthQueryHandler>> _mockLogger = null!;
    private GetBacklogHealthQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockProvider = new Mock<IWorkItemReadProvider>();
        _mockProductRepository = new Mock<IProductRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockBacklogQualityAnalysisService = new Mock<IBacklogQualityAnalysisService>();
        _mockLogger = new Mock<ILogger<GetBacklogHealthQueryHandler>>();

        // Setup default mock behaviors
        _mockProductRepository.Setup(r => r.GetAllProductsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductDto>());
        _mockMediator.Setup(m => m.Send(It.IsAny<GetWorkItemsByRootIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        _mockBacklogQualityAnalysisService.Setup(service => service.AnalyzeAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAnalysis());

        _handler = new GetBacklogHealthQueryHandler(
            new SprintScopedWorkItemLoader(
                _mockProvider.Object,
                _mockProductRepository.Object,
                _mockMediator.Object),
            _mockBacklogQualityAnalysisService.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoWorkItems_ReturnsNull()
    {
        // Arrange
        _mockProvider.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task Handle_WithValidWorkItems_CalculatesCorrectMetrics()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "In Progress", "Sprint 1", null),
            CreateWorkItem(3, "Bug", "New", "Sprint 1", null),
            CreateWorkItem(4, "Task", "Done", "Sprint 1", 3)
        };

        _mockProvider.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Sprint 1", result.IterationPath);
        Assert.AreEqual("Sprint 1", result.SprintName);
        Assert.AreEqual(4, result.TotalWorkItems);
        Assert.AreEqual(2, result.WorkItemsWithoutEffort); // Items 2 and 3
        Assert.AreEqual(1, result.WorkItemsInProgressWithoutEffort); // Item 2
    }

    [TestMethod]
    public async Task Handle_WithBlockedItems_CountsCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Blocked", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "On Hold", "Sprint 1", 8),
            CreateWorkItem(3, "Bug", "In Progress", "Sprint 1", 3)
        };

        _mockProvider.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.BlockedItems); // Items with "Blocked" or "On Hold"
    }

    [TestMethod]
    public async Task Handle_WithValidationIssues_GroupsByType()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "In Progress", "Sprint 1", null),
            CreateWorkItem(2, "PBI", "Done", "Sprint 1", 5)
        };

        _mockProvider.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockBacklogQualityAnalysisService.Setup(service => service.AnalyzeAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAnalysis(refinementNeededIds: [1]));

        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        CollectionAssert.AreEquivalent(
            new[] { "Refinement Needed" },
            result.ValidationIssues.Select(issue => issue.ValidationType).ToArray());
    }

    [TestMethod]
    public async Task Handle_WithParentProgressIssues_CountsCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "Task", "Done", "Sprint 1", 3)
        };

        _mockProvider.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        _mockBacklogQualityAnalysisService.Setup(service => service.AnalyzeAsync(It.IsAny<IEnumerable<WorkItemDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAnalysis(integrityIds: [1, 2]));

        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.ParentProgressIssues); // Both structural integrity issues
    }

    [TestMethod]
    public async Task Handle_WithAllItemsHavingEffort_ReturnsZeroWithoutEffort()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "In Progress", "Sprint 1", 8),
            CreateWorkItem(3, "Bug", "New", "Sprint 1", 3)
        };

        _mockProvider.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.WorkItemsWithoutEffort);
        Assert.AreEqual(0, result.WorkItemsInProgressWithoutEffort);
    }

    [TestMethod]
    public async Task Handle_WithCaseInsensitiveIterationPath_MatchesCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "SPRINT 1", 5),
            CreateWorkItem(2, "PBI", "Done", "Sprint 1", 8)
        };

        _mockProvider.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var query = new GetBacklogHealthQuery("sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalWorkItems);
    }

    [TestMethod]
    public async Task Handle_WithComplexIterationPath_ExtractsCorrectSprintName()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Project\\Team A\\2024\\Sprint 5", 5)
        };

        _mockProvider.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var query = new GetBacklogHealthQuery("Project\\Team A\\2024\\Sprint 5");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Sprint 5", result.SprintName);
        Assert.AreEqual("Project\\Team A\\2024\\Sprint 5", result.IterationPath);
    }

    [TestMethod]
    public async Task Handle_WithZeroEffortItems_CountsAsHavingEffort()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 0),
            CreateWorkItem(2, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(3, "Bug", "New", "Sprint 1", null)
        };

        _mockProvider.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var query = new GetBacklogHealthQuery("Sprint 1");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.WorkItemsWithoutEffort); // Only item 3
    }

    [TestMethod]
    public async Task Handle_UsesBacklogQualityAnalysisServiceForIterationItems()
    {
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "PBI", "Done", "Sprint 1", 5),
            CreateWorkItem(2, "PBI", "Done", "Sprint 2", 8)
        };

        _mockProvider.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        await _handler.Handle(new GetBacklogHealthQuery("Sprint 1"), CancellationToken.None);

        _mockBacklogQualityAnalysisService.Verify(
            service => service.AnalyzeAsync(
                It.Is<IEnumerable<WorkItemDto>>(items => items.Select(item => item.TfsId).SequenceEqual(new[] { 1 })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static WorkItemDto CreateWorkItem(
        int id,
        string type,
        string state,
        string iterationPath,
        int? effort)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Work Item {id}",
            ParentTfsId: null,
            AreaPath: "TestArea",
            IterationPath: iterationPath,
            State: state,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
                    Description: null,
                    Tags: null
        );
    }

    private static BacklogQualityAnalysisResult CreateAnalysis(
        IEnumerable<int>? integrityIds = null,
        IEnumerable<int>? refinementBlockerIds = null,
        IEnumerable<int>? refinementNeededIds = null,
        IEnumerable<int>? missingEffortIds = null)
    {
        var integrityFindings = (integrityIds ?? [])
            .Select(id => new BacklogIntegrityFinding(
                CreateMetadata("SI-1", RuleFamily.StructuralIntegrity, "ParentChildMismatch", RuleFindingClass.StructuralWarning),
                id,
                "Epic",
                $"Integrity issue for {id}",
                Array.Empty<int>()))
            .ToArray();
        var findings = (refinementBlockerIds ?? [])
            .Select(id => CreateFinding(id, "RR-1", RuleFamily.RefinementReadiness, "MissingDescription"))
            .Concat((refinementNeededIds ?? [])
                .Select(id => CreateFinding(id, "RC-3", RuleFamily.ImplementationReadiness, "MissingChildren")))
            .Concat((missingEffortIds ?? [])
                .Select(id => CreateFinding(id, "RC-2", RuleFamily.ImplementationReadiness, "MissingEffort")))
            .ToArray();

        return new BacklogQualityAnalysisResult(
            new BacklogValidationResult(
                integrityFindings,
                findings,
                Array.Empty<RefinementReadinessState>(),
                Array.Empty<ImplementationReadinessState>()),
            Array.Empty<BacklogReadinessScore>());
    }

    private static PoTool.Core.Domain.BacklogQuality.Models.ValidationRuleResult CreateFinding(
        int workItemId,
        string ruleId,
        RuleFamily family,
        string semanticTag)
    {
        return new PoTool.Core.Domain.BacklogQuality.Models.ValidationRuleResult(
            CreateMetadata(
                ruleId,
                family,
                semanticTag,
                family == RuleFamily.RefinementReadiness ? RuleFindingClass.RefinementBlocker : RuleFindingClass.ImplementationBlocker),
            workItemId,
            "PBI",
            $"Finding {ruleId}");
    }

    private static RuleMetadata CreateMetadata(
        string ruleId,
        RuleFamily family,
        string semanticTag,
        RuleFindingClass findingClass)
    {
        return new RuleMetadata(
            ruleId,
            family,
            semanticTag,
            $"Rule {ruleId}",
            RuleResponsibleParty.ProductOwner,
            findingClass,
            ["Epic", "Feature", "PBI", "Task", "Bug"]);
    }
}
