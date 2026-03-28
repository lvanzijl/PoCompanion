using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.Pipelines;
using PoTool.Core.Contracts;
using PoTool.Core.Filters;
using PoTool.Core.Pipelines.Filters;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetPipelineMetricsQueryHandlerTests
{
    private Mock<IPipelineReadProvider> _mockProvider = null!;
    private GetPipelineMetricsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockProvider = new Mock<IPipelineReadProvider>();
        _handler = new GetPipelineMetricsQueryHandler(_mockProvider.Object);
    }

    [TestMethod]
    public async Task Handle_WithProductIds_UsesResolvedBranchScope()
    {
        // Arrange
        var query = new GetPipelineMetricsQuery(CreateFilter(
            productIds: [1, 2],
            repositories: ["TestRepo1", "TestRepo2"],
            pipelineIds: [101, 102],
            branchScope:
            [
                new PipelineBranchScope(101, "refs/heads/main"),
                new PipelineBranchScope(102, "refs/heads/release")
            ],
            rangeStartUtc: DateTimeOffset.UtcNow.AddDays(-30)));

        var pipelines = new List<PipelineDto>
        {
            new(101, "Pipeline1", PipelineType.Build, null, DateTimeOffset.UtcNow),
            new(102, "Pipeline2", PipelineType.Build, null, DateTimeOffset.UtcNow)
        };

        var runs = new List<PipelineRunDto>
        {
            new(1, 101, "Pipeline1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-1).AddHours(1),
                TimeSpan.FromHours(1), PipelineRunResult.Succeeded, PipelineRunTrigger.ContinuousIntegration, 
                null, "refs/heads/main", "user1", DateTimeOffset.UtcNow),
            new(2, 102, "Pipeline2", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-2).AddHours(1),
                TimeSpan.FromHours(1), PipelineRunResult.Failed, PipelineRunTrigger.ContinuousIntegration,
                null, "refs/heads/main", "user2", DateTimeOffset.UtcNow)
        };

        _mockProvider.Setup(p => p.GetRunsForPipelinesAsync(
            It.Is<IEnumerable<int>>(ids => ids.Contains(101) && ids.Contains(102)),
            null,
            It.IsAny<DateTimeOffset?>(),
            100,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        _mockProvider.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.ToList();
        Assert.HasCount(1, metrics);
        Assert.AreEqual(101, metrics[0].PipelineId);

        // Verify efficient runs fetching with filters was called
        _mockProvider.Verify(p => p.GetRunsForPipelinesAsync(
            It.IsAny<IEnumerable<int>>(),
            null,
            It.IsAny<DateTimeOffset?>(),
            100,
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify we did NOT call the old inefficient GetAllRunsAsync
        _mockProvider.Verify(p => p.GetAllRunsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_WithoutProductIds_FetchesAllPipelines()
    {
        // Arrange
        var query = new GetPipelineMetricsQuery(CreateFilter(
            productIds: null,
            repositories: ["TestRepo1"],
            pipelineIds: [101],
            branchScope: [new PipelineBranchScope(101, "refs/heads/main")]));

        var pipelines = new List<PipelineDto>
        {
            new(101, "Pipeline1", PipelineType.Build, null, DateTimeOffset.UtcNow)
        };

        var runs = new List<PipelineRunDto>
        {
            new(1, 101, "Pipeline1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-1).AddHours(1),
                TimeSpan.FromHours(1), PipelineRunResult.Succeeded, PipelineRunTrigger.ContinuousIntegration,
                null, "refs/heads/main", "user1", DateTimeOffset.UtcNow)
        };

        // Setup mocks
        _mockProvider.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        _mockProvider.Setup(p => p.GetRunsForPipelinesAsync(
            It.Is<IEnumerable<int>>(ids => ids.Contains(101)),
            null,
            It.IsAny<DateTimeOffset?>(),
            100,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.ToList();
        Assert.HasCount(1, metrics);
        
        // Verify efficient runs fetching with filters was used
        _mockProvider.Verify(p => p.GetRunsForPipelinesAsync(
            It.IsAny<IEnumerable<int>>(),
            null,
            It.IsAny<DateTimeOffset?>(),
            100,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithProductIdsButNoPipelines_ReturnsEmpty()
    {
        // Arrange
        var query = new GetPipelineMetricsQuery(CreateFilter(
            productIds: [999],
            repositories: ["Repo-X"],
            pipelineIds: Array.Empty<int>(),
            branchScope: Array.Empty<PipelineBranchScope>()));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.ToList();
        Assert.IsEmpty(metrics);
        
        // Verify we did not fetch runs since there are no pipelines
        _mockProvider.Verify(p => p.GetRunsForPipelinesAsync(
            It.IsAny<IEnumerable<int>>(),
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_UsesDefaultBranchScopeInsteadOfHardcodedMain()
    {
        var query = new GetPipelineMetricsQuery(CreateFilter(
            productIds: [1],
            repositories: ["TestRepo1"],
            pipelineIds: [101],
            branchScope: [new PipelineBranchScope(101, "refs/heads/release")]));

        var pipelines = new List<PipelineDto>
        {
            new(101, "Pipeline1", PipelineType.Build, null, DateTimeOffset.UtcNow)
        };

        var runs = new List<PipelineRunDto>
        {
            new(1, 101, "Pipeline1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-1).AddHours(1),
                TimeSpan.FromHours(1), PipelineRunResult.Succeeded, PipelineRunTrigger.ContinuousIntegration,
                null, "refs/heads/main", "user1", DateTimeOffset.UtcNow),
            new(2, 101, "Pipeline1", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-2).AddHours(1),
                TimeSpan.FromHours(1), PipelineRunResult.Failed, PipelineRunTrigger.ContinuousIntegration,
                null, "refs/heads/release", "user2", DateTimeOffset.UtcNow)
        };

        _mockProvider.Setup(p => p.GetRunsForPipelinesAsync(
                It.IsAny<IEnumerable<int>>(),
                null,
                It.IsAny<DateTimeOffset?>(),
                100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);
        _mockProvider.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        var result = (await _handler.Handle(query, CancellationToken.None)).Single();

        Assert.AreEqual(1, result.TotalRuns);
        Assert.AreEqual(1, result.FailedRuns);
    }

    [TestMethod]
    public async Task Handle_WithUnevenPipelineActivity_IncludesQuietPipelineMetrics()
    {
        var query = new GetPipelineMetricsQuery(CreateFilter(
            productIds: [1, 2],
            repositories: ["Repo-A", "Repo-B"],
            pipelineIds: [101, 202],
            branchScope:
            [
                new PipelineBranchScope(101, "refs/heads/main"),
                new PipelineBranchScope(202, "refs/heads/release")
            ]));

        _mockProvider.Setup(p => p.GetRunsForPipelinesAsync(
                It.Is<IEnumerable<int>>(ids => ids.Contains(101) && ids.Contains(202)),
                null,
                It.IsAny<DateTimeOffset?>(),
                100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PipelineRunDto(1, 101, "Busy", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-1).AddHours(1), TimeSpan.FromHours(1), PipelineRunResult.Succeeded, PipelineRunTrigger.ContinuousIntegration, null, "refs/heads/main", "user1", DateTimeOffset.UtcNow),
                new PipelineRunDto(2, 101, "Busy", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-2).AddHours(1), TimeSpan.FromHours(1), PipelineRunResult.Failed, PipelineRunTrigger.ContinuousIntegration, null, "refs/heads/main", "user2", DateTimeOffset.UtcNow),
                new PipelineRunDto(3, 202, "Quiet", DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-10).AddHours(1), TimeSpan.FromHours(1), PipelineRunResult.Succeeded, PipelineRunTrigger.ContinuousIntegration, null, "refs/heads/release", "user3", DateTimeOffset.UtcNow)
            ]);

        _mockProvider.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PipelineDto(101, "Busy", PipelineType.Build, null, DateTimeOffset.UtcNow),
                new PipelineDto(202, "Quiet", PipelineType.Build, null, DateTimeOffset.UtcNow)
            ]);

        var metrics = (await _handler.Handle(query, CancellationToken.None))
            .OrderBy(metric => metric.PipelineId)
            .ToList();

        Assert.HasCount(2, metrics);
        Assert.AreEqual(101, metrics[0].PipelineId);
        Assert.AreEqual(2, metrics[0].TotalRuns);
        Assert.AreEqual(202, metrics[1].PipelineId);
        Assert.AreEqual(1, metrics[1].TotalRuns);
        Assert.AreEqual(1, metrics[1].SuccessfulRuns);
    }

    private static PipelineEffectiveFilter CreateFilter(
        IReadOnlyList<int>? productIds,
        IReadOnlyList<string> repositories,
        IReadOnlyList<int> pipelineIds,
        IReadOnlyList<PipelineBranchScope> branchScope,
        DateTimeOffset? rangeStartUtc = null,
        DateTimeOffset? rangeEndUtc = null)
        => new(
            new PipelineFilterContext(
                productIds is { Count: > 0 }
                    ? FilterSelection<int>.Selected(productIds)
                    : FilterSelection<int>.All(),
                FilterSelection<int>.All(),
                FilterSelection<string>.Selected(repositories),
                rangeStartUtc.HasValue || rangeEndUtc.HasValue
                    ? FilterTimeSelection.DateRange(rangeStartUtc, rangeEndUtc)
                    : FilterTimeSelection.None()),
            repositories.ToArray(),
            pipelineIds.ToArray(),
            branchScope.ToArray(),
            rangeStartUtc,
            rangeEndUtc,
            null);
}
