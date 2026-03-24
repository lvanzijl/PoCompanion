using Microsoft.Extensions.Logging;
using Moq;
using PoTool.Api.Services;
using PoTool.Api.Services.BuildQuality;
using PoTool.Api.Services.MockData;
using PoTool.Shared.BuildQuality;
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

    [TestMethod]
    public async Task GetBuildQualityFactsForIncidentResponseControl_ReturnsLinkedMixedScenario()
    {
        var client = new MockTfsClient(CreateMockDataFacade(), Mock.Of<ILogger<MockTfsClient>>());
        var repositoryName = "Battleship-Incident-Backend";

        var definitions = (await client.GetPipelineDefinitionsForRepositoryAsync(repositoryName)).ToList();
        var runs = (await client.GetPipelineRunsAsync(
                definitions.Select(definition => definition.PipelineDefinitionId),
                branchName: "refs/heads/main",
                top: 100))
            .Where(run => run.RunId >= 910001)
            .OrderBy(run => run.RunId)
            .ToList();

        var buildIds = runs.Select(run => run.RunId).ToArray();
        var testRuns = (await client.GetTestRunsByBuildIdsAsync(buildIds))
            .OrderBy(testRun => testRun.BuildId)
            .ToList();
        var coverage = (await client.GetCoverageByBuildIdsAsync(buildIds))
            .OrderBy(coverageRow => coverageRow.BuildId)
            .ToList();

        Assert.HasCount(5, runs);
        CollectionAssert.AreEqual(
            new[] { 910001, 910002, 910003, 910004, 910005 },
            buildIds);
        Assert.IsTrue(runs.Any(run => run.Result == PipelineRunResult.Succeeded));
        Assert.IsTrue(runs.Any(run => run.Result == PipelineRunResult.Failed));

        CollectionAssert.AreEqual(
            new[] { 910001, 910002, 910003 },
            testRuns.Select(testRun => testRun.BuildId).ToArray());
        CollectionAssert.AreEqual(
            new[] { 910001, 910002, 910004 },
            coverage.Select(coverageRow => coverageRow.BuildId).ToArray());

        var buildWithTestsNoCoverage = 910003;
        var buildWithCoverageNoTests = 910004;
        var buildWithNeither = 910005;

        Assert.IsTrue(testRuns.Any(testRun => testRun.BuildId == buildWithTestsNoCoverage));
        Assert.IsFalse(coverage.Any(coverageRow => coverageRow.BuildId == buildWithTestsNoCoverage));
        Assert.IsTrue(coverage.Any(coverageRow => coverageRow.BuildId == buildWithCoverageNoTests));
        Assert.IsFalse(testRuns.Any(testRun => testRun.BuildId == buildWithCoverageNoTests));
        Assert.IsFalse(testRuns.Any(testRun => testRun.BuildId == buildWithNeither));
        Assert.IsFalse(coverage.Any(coverageRow => coverageRow.BuildId == buildWithNeither));

        var provider = new BuildQualityProvider();
        var result = provider.Compute(
            runs.Select(run => new BuildQualityBuildFact(run.RunId, run.Result.ToString())),
            testRuns.Select(testRun => new BuildQualityTestRunFact(testRun.BuildId, testRun.TotalTests, testRun.PassedTests, testRun.NotApplicableTests)),
            coverage.Select(coverageRow => new BuildQualityCoverageFact(coverageRow.BuildId, coverageRow.CoveredLines, coverageRow.TotalLines)));

        Assert.IsNotNull(result.Metrics.SuccessRate, "Success rate should be known when builds exist.");
        Assert.IsNotNull(result.Metrics.TestPassRate, "Test pass rate should be known when test runs exist.");
        Assert.IsNotNull(result.Metrics.Coverage, "Coverage should be known when coverage rows exist.");
        Assert.IsGreaterThan(0d, result.Metrics.SuccessRate!.Value);
        Assert.IsGreaterThan(0d, result.Metrics.TestPassRate!.Value);
        Assert.IsGreaterThan(0d, result.Metrics.Coverage!.Value);
        Assert.IsFalse(result.Evidence.SuccessRateUnknown);
        Assert.IsFalse(result.Evidence.TestPassRateUnknown);
        Assert.IsFalse(result.Evidence.CoverageUnknown);
        Assert.IsGreaterThan(0, result.Metrics.Confidence);
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
