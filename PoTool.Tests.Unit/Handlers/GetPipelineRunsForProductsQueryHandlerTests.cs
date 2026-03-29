using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Pipelines;
using PoTool.Core.Contracts;
using PoTool.Core.Filters;
using PoTool.Core.Pipelines.Filters;
using PoTool.Core.Pipelines.Queries;
using PoTool.Shared.Pipelines;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetPipelineRunsForProductsQueryHandlerTests
{
    [TestMethod]
    public async Task Handle_WithUnevenPipelineActivityAndBranchScope_ReturnsCompleteScopedRuns()
    {
        var provider = new Mock<IPipelineReadProvider>();
        var handler = new GetPipelineRunsForProductsQueryHandler(provider.Object);

        var filter = new PipelineEffectiveFilter(
            new PipelineFilterContext(
                FilterSelection<int>.Selected([1, 2]),
                FilterSelection<int>.All(),
                FilterSelection<string>.Selected(["Repo-A", "Repo-B"]),
                FilterTimeSelection.DateRange(
                    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero))),
            RepositoryScope: ["Repo-A", "Repo-B"],
            PipelineIds: [101, 202],
            BranchScope:
            [
                new PipelineBranchScope(101, "refs/heads/main"),
                new PipelineBranchScope(202, "refs/heads/release")
            ],
            RangeStartUtc: new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            RangeEndUtc: new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero),
            SprintId: null);

        provider.Setup(p => p.GetRunsForPipelinesAsync(
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 101, 202 })),
                null,
                filter.RangeStartUtc,
                filter.RangeEndUtc,
                It.Is<IReadOnlyList<PipelineBranchScope>>(scopes =>
                    scopes.Count == 2
                    && scopes.Any(scope => scope.PipelineId == 101 && scope.DefaultBranch == "refs/heads/main")
                    && scopes.Any(scope => scope.PipelineId == 202 && scope.DefaultBranch == "refs/heads/release")),
                100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PipelineRunDto(1, 101, "Busy", new DateTimeOffset(2026, 3, 20, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), PipelineRunResult.Succeeded, PipelineRunTrigger.ContinuousIntegration, null, "refs/heads/main", "user", DateTimeOffset.UtcNow),
                new PipelineRunDto(2, 101, "Busy", new DateTimeOffset(2026, 3, 19, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 19, 10, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), PipelineRunResult.Failed, PipelineRunTrigger.ContinuousIntegration, null, "refs/heads/main", "user", DateTimeOffset.UtcNow),
                new PipelineRunDto(3, 101, "Busy", new DateTimeOffset(2026, 3, 18, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 18, 10, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), PipelineRunResult.Succeeded, PipelineRunTrigger.ContinuousIntegration, null, "refs/heads/release", "user", DateTimeOffset.UtcNow),
                new PipelineRunDto(4, 202, "Quiet", new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1), PipelineRunResult.Succeeded, PipelineRunTrigger.ContinuousIntegration, null, "refs/heads/release", "user", DateTimeOffset.UtcNow)
            ]);

        var runs = (await handler.Handle(new GetPipelineRunsForProductsQuery(filter), CancellationToken.None)).ToList();

        Assert.HasCount(3, runs);
        Assert.AreEqual(2, runs.Count(run => run.PipelineId == 101));
        Assert.AreEqual(1, runs.Count(run => run.PipelineId == 202));
        Assert.IsFalse(runs.Any(run => run.PipelineId == 101 && run.Branch == "refs/heads/release"));
    }
}
