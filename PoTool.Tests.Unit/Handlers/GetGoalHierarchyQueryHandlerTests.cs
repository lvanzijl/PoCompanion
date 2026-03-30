using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Configuration;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetGoalHierarchyQueryHandlerTests
{
    private Mock<IWorkItemQuery> _workItemQuery = null!;
    private Mock<ILogger<GetGoalHierarchyQueryHandler>> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _workItemQuery = new Mock<IWorkItemQuery>(MockBehavior.Strict);
        _logger = new Mock<ILogger<GetGoalHierarchyQueryHandler>>();
    }

    [TestMethod]
    public async Task Handle_InRealMode_UsesCachedGoalHierarchyQuery()
    {
        var expected = new[]
        {
            CreateWorkItem(100, WorkItemType.Goal, null),
            CreateWorkItem(101, WorkItemType.Epic, 100)
        };

        _workItemQuery
            .Setup(query => query.GetGoalHierarchyAsync(
                It.Is<IReadOnlyList<int>>(goalIds => goalIds.SequenceEqual(new[] { 100 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetGoalHierarchyQueryHandler(
            _workItemQuery.Object,
            new TfsRuntimeMode(useMockClient: false),
            _logger.Object);

        var result = (await handler.Handle(new GetGoalHierarchyQuery([100]), CancellationToken.None)).ToList();

        CollectionAssert.AreEquivalent(expected.Select(item => item.TfsId).ToArray(), result.Select(item => item.TfsId).ToArray());
        _workItemQuery.VerifyAll();
    }

    [TestMethod]
    public async Task Handle_InMockMode_UsesMockFacadeHierarchy()
    {
        var mockFacade = CreateMockDataFacade();
        var goalIds = mockFacade
            .GetMockHierarchy()
            .Where(item => item.Type.Equals(WorkItemType.Goal, StringComparison.OrdinalIgnoreCase))
            .Take(1)
            .Select(item => item.TfsId)
            .ToList();
        var expectedIds = mockFacade
            .GetMockHierarchyForGoals(goalIds)
            .Select(item => item.TfsId)
            .OrderBy(id => id)
            .ToArray();

        var handler = new GetGoalHierarchyQueryHandler(
            _workItemQuery.Object,
            new TfsRuntimeMode(useMockClient: true),
            _logger.Object,
            mockFacade);

        var result = (await handler.Handle(new GetGoalHierarchyQuery(goalIds), CancellationToken.None))
            .Select(item => item.TfsId)
            .OrderBy(id => id)
            .ToArray();

        CollectionAssert.AreEqual(expectedIds, result);
        _workItemQuery.Verify(
            query => query.GetGoalHierarchyAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Handle_InMockModeWithoutFacade_ThrowsInvalidOperationException()
    {
        var handler = new GetGoalHierarchyQueryHandler(
            _workItemQuery.Object,
            new TfsRuntimeMode(useMockClient: true),
            _logger.Object);

        try
        {
            await handler.Handle(new GetGoalHierarchyQuery([100]), CancellationToken.None);
            Assert.Fail("Expected InvalidOperationException to be thrown.");
        }
        catch (InvalidOperationException)
        {
            // Expected guardrail exception.
        }
    }

    private static BattleshipMockDataFacade CreateMockDataFacade()
    {
        var workItemGenerator = new BattleshipWorkItemGenerator();
        var dependencyGenerator = new BattleshipDependencyGenerator();
        var pullRequestGenerator = new BattleshipPullRequestGenerator();
        var pipelineGenerator = new BattleshipPipelineGenerator(Mock.Of<ILogger<BattleshipPipelineGenerator>>());
        var validator = new MockDataValidator();

        return new BattleshipMockDataFacade(
            workItemGenerator,
            dependencyGenerator,
            pullRequestGenerator,
            pipelineGenerator,
            validator,
            Mock.Of<ILogger<BattleshipMockDataFacade>>());
    }

    private static WorkItemDto CreateWorkItem(int tfsId, string type, int? parentTfsId) =>
        new(
            TfsId: tfsId,
            Type: type,
            Title: $"Item {tfsId}",
            ParentTfsId: parentTfsId,
            AreaPath: "Area",
            IterationPath: "Iteration",
            State: "New",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null);
}
