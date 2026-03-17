using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Shared.Pipelines;
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

    [TestMethod]
    public async Task GetPipelineDefinitionsForRepositoryAsync_ReturnsDefinitionsWithMatchingRuns()
    {
        var client = new MockTfsClient(CreateMockDataFacade(), Mock.Of<ILogger<MockTfsClient>>());
        var repositoryName = "Battleship-Incident-Backend";

        var definitions = (await client.GetPipelineDefinitionsForRepositoryAsync(repositoryName)).ToList();
        var pipelineIds = definitions.Select(definition => definition.PipelineDefinitionId).ToList();
        var runs = (await client.GetPipelineRunsAsync(pipelineIds, top: 5)).ToList();

        Assert.IsGreaterThan(0, definitions.Count, "Expected mock repositories to expose pipeline definitions.");
        Assert.IsTrue(definitions.All(definition => definition.RepoName == repositoryName),
            "Definitions should stay scoped to the requested repository.");
        Assert.IsNotEmpty(runs, "Expected discovered pipeline definitions to resolve to mock pipeline runs.");
        Assert.IsTrue(runs.All(run => pipelineIds.Contains(run.PipelineId)),
            "Pipeline runs should belong to the discovered definitions.");
        Assert.IsTrue(runs.Any(run => run.Trigger == PipelineRunTrigger.PullRequest),
            "Expected at least one PR-triggered run for analytics validation.");
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
