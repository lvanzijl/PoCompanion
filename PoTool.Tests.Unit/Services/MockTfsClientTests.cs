using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class MockTfsClientTests
{
    [TestMethod]
    public async Task GetWorkItemsByTypeAsync_WithProjectRootAreaPath_ReturnsGoals()
    {
        var client = new MockTfsClient(CreateMockDataFacade(), Mock.Of<ILogger<MockTfsClient>>());

        var results = (await client.GetWorkItemsByTypeAsync(WorkItemType.Goal, "Battleship Systems")).ToList();

        Assert.HasCount(10, results);
        Assert.IsTrue(results.All(item => item.Type.Equals(WorkItemType.Goal, StringComparison.OrdinalIgnoreCase)));
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
}
