using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.PullRequests;
using PoTool.Api.Services;
using PoTool.Core.Filters;
using PoTool.Core.PullRequests.Filters;
using PoTool.Core.PullRequests.Queries;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetPullRequestMetricsQueryHandlerTests
{
    private Mock<IPullRequestQueryStore> _mockQueryStore = null!;
    private Mock<ILogger<GetPullRequestMetricsQueryHandler>> _mockLogger = null!;
    private GetPullRequestMetricsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockQueryStore = new Mock<IPullRequestQueryStore>();
        _mockLogger = new Mock<ILogger<GetPullRequestMetricsQueryHandler>>();
        _handler = new GetPullRequestMetricsQueryHandler(
            _mockQueryStore.Object,
            _mockLogger.Object);
    }

    private static GetPullRequestMetricsQuery CreateQuery(
        IReadOnlyList<string>? repositoryScope = null,
        DateTimeOffset? rangeStartUtc = null,
        DateTimeOffset? rangeEndUtc = null)
        => new(new PullRequestEffectiveFilter(
            new PullRequestFilterContext(
                FilterSelection<int>.All(),
                FilterSelection<int>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                rangeStartUtc.HasValue || rangeEndUtc.HasValue
                    ? FilterTimeSelection.DateRange(rangeStartUtc, rangeEndUtc)
                    : FilterTimeSelection.None()),
            repositoryScope ?? ["TestRepo"],
            rangeStartUtc,
            rangeEndUtc,
            null,
            Array.Empty<int>()));

    private void SetupMetricsData(
        IEnumerable<PullRequestDto>? pullRequests = null,
        IEnumerable<PullRequestIterationDto>? iterations = null,
        IEnumerable<PullRequestCommentDto>? comments = null,
        IEnumerable<PullRequestFileChangeDto>? fileChanges = null)
    {
        _mockQueryStore.Setup(r => r.GetMetricsDataAsync(
                It.IsAny<PullRequestEffectiveFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestMetricsQueryData(
                (pullRequests ?? Array.Empty<PullRequestDto>()).ToList(),
                GroupByPullRequestId(iterations ?? Array.Empty<PullRequestIterationDto>(), iteration => iteration.PullRequestId),
                GroupByPullRequestId(comments ?? Array.Empty<PullRequestCommentDto>(), comment => comment.PullRequestId),
                GroupByPullRequestId(fileChanges ?? Array.Empty<PullRequestFileChangeDto>(), fileChange => fileChange.PullRequestId)));
    }

    [TestMethod]
    public async Task Handle_WithNoPullRequests_ReturnsEmptyList()
    {
        SetupMetricsData();

        var result = await _handler.Handle(CreateQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_WithSinglePullRequest_CalculatesMetricsCorrectly()
    {
        var createdDate = DateTimeOffset.UtcNow.AddDays(-7);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "Test PR", "TestUser", createdDate, completedDate, "Completed");

        var iterations = new List<PullRequestIterationDto>
        {
            CreateIteration(1, 1, createdDate, createdDate.AddHours(2)),
            CreateIteration(1, 2, createdDate.AddDays(1), createdDate.AddDays(1).AddHours(1))
        };

        var comments = new List<PullRequestCommentDto>
        {
            CreateComment(1, 1, "Author1", createdDate.AddHours(1), false),
            CreateComment(2, 1, "Author2", createdDate.AddHours(3), true)
        };

        var fileChanges = new List<PullRequestFileChangeDto>
        {
            CreateFileChange(1, 1, "File1.cs", 50, 10, 5),
            CreateFileChange(1, 1, "File2.cs", 30, 20, 10)
        };

        SetupMetricsData([pr], iterations, comments, fileChanges);

        var result = await _handler.Handle(CreateQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        var metrics = result.Single();
        Assert.AreEqual(1, metrics.PullRequestId);
        Assert.AreEqual("Test PR", metrics.Title);
        Assert.AreEqual(2, metrics.IterationCount);
        Assert.AreEqual(2, metrics.CommentCount);
        Assert.AreEqual(1, metrics.UnresolvedCommentCount);
        Assert.AreEqual(2, metrics.TotalFileCount);
        Assert.AreEqual(80, metrics.TotalLinesAdded);
        Assert.AreEqual(30, metrics.TotalLinesDeleted);
    }

    [TestMethod]
    public async Task Handle_WithOpenPullRequest_UsesCurrentTimeForCalculation()
    {
        var createdDate = DateTimeOffset.UtcNow.AddDays(-3);
        var pr = CreatePullRequest(1, "Open PR", "TestUser", createdDate, null, "Active");

        SetupMetricsData([pr]);

        var result = await _handler.Handle(CreateQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        var metrics = result.Single();
        Assert.IsGreaterThanOrEqualTo(2.99, metrics.TotalTimeOpen.TotalDays,
            $"TotalTimeOpen should be approximately 3 days or more, but was {metrics.TotalTimeOpen.TotalDays}");
        Assert.IsNull(metrics.CompletedDate);
    }

    [TestMethod]
    public async Task Handle_WithNoIterations_ReturnsNullEffectiveWorkTime()
    {
        var createdDate = DateTimeOffset.UtcNow.AddDays(-5);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "Simple PR", "TestUser", createdDate, completedDate, "Completed");

        SetupMetricsData([pr]);

        var result = await _handler.Handle(CreateQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsNull(result.Single().EffectiveWorkTime);
    }

    [TestMethod]
    public async Task Handle_WithNoFileChanges_ReturnsZeroFileMetrics()
    {
        var createdDate = DateTimeOffset.UtcNow.AddDays(-2);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "No Files PR", "TestUser", createdDate, completedDate, "Completed");

        SetupMetricsData([pr]);

        var result = await _handler.Handle(CreateQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        var metrics = result.Single();
        Assert.AreEqual(0, metrics.TotalFileCount);
        Assert.AreEqual(0, metrics.TotalLinesAdded);
        Assert.AreEqual(0, metrics.TotalLinesDeleted);
        Assert.AreEqual(0, metrics.AverageLinesPerFile);
    }

    [TestMethod]
    public async Task Handle_WithMultipleChangesToSameFile_CountsFileOnce()
    {
        var createdDate = DateTimeOffset.UtcNow.AddDays(-1);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "Multi-edit PR", "TestUser", createdDate, completedDate, "Completed");

        var fileChanges = new List<PullRequestFileChangeDto>
        {
            CreateFileChange(1, 1, "File1.cs", 10, 5, 0),
            CreateFileChange(1, 2, "File1.cs", 20, 10, 5),
            CreateFileChange(1, 1, "File2.cs", 30, 0, 0)
        };

        SetupMetricsData([pr], fileChanges: fileChanges);

        var result = await _handler.Handle(CreateQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        var metrics = result.Single();
        Assert.AreEqual(2, metrics.TotalFileCount);
        Assert.AreEqual(60, metrics.TotalLinesAdded);
        Assert.AreEqual(15, metrics.TotalLinesDeleted);
    }

    [TestMethod]
    public async Task Handle_WithAllCommentsResolved_ReturnsZeroUnresolved()
    {
        var createdDate = DateTimeOffset.UtcNow.AddDays(-2);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "Resolved PR", "TestUser", createdDate, completedDate, "Completed");

        var comments = new List<PullRequestCommentDto>
        {
            CreateComment(1, 1, "Author1", createdDate.AddHours(1), true),
            CreateComment(2, 1, "Author2", createdDate.AddHours(2), true),
            CreateComment(3, 1, "Author3", createdDate.AddHours(3), true)
        };

        SetupMetricsData([pr], comments: comments);

        var result = await _handler.Handle(CreateQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        var metrics = result.Single();
        Assert.AreEqual(3, metrics.CommentCount);
        Assert.AreEqual(0, metrics.UnresolvedCommentCount);
    }

    [TestMethod]
    public async Task Handle_WithMultiplePullRequests_ReturnsAllMetrics()
    {
        var createdDate1 = DateTimeOffset.UtcNow.AddDays(-5);
        var createdDate2 = DateTimeOffset.UtcNow.AddDays(-3);
        var pr1 = CreatePullRequest(1, "PR 1", "User1", createdDate1, DateTimeOffset.UtcNow, "Completed");
        var pr2 = CreatePullRequest(2, "PR 2", "User2", createdDate2, null, "Active");

        SetupMetricsData([pr1, pr2]);

        var result = await _handler.Handle(CreateQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.Any(m => m.PullRequestId == 1));
        Assert.IsTrue(result.Any(m => m.PullRequestId == 2));
    }

    [TestMethod]
    public async Task Handle_CalculatesAverageLinesPerFileCorrectly()
    {
        var createdDate = DateTimeOffset.UtcNow.AddDays(-1);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "Avg Lines PR", "TestUser", createdDate, completedDate, "Completed");

        var fileChanges = new List<PullRequestFileChangeDto>
        {
            CreateFileChange(1, 1, "File1.cs", 100, 50, 0),
            CreateFileChange(1, 1, "File2.cs", 50, 25, 0),
            CreateFileChange(1, 1, "File3.cs", 25, 0, 0)
        };

        SetupMetricsData([pr], fileChanges: fileChanges);

        var result = await _handler.Handle(CreateQuery(), CancellationToken.None);

        Assert.IsNotNull(result);
        var metrics = result.Single();
        Assert.AreEqual(3, metrics.TotalFileCount);
        Assert.AreEqual(175, metrics.TotalLinesAdded);
        Assert.AreEqual(75, metrics.TotalLinesDeleted);
        Assert.AreEqual(83.33, metrics.AverageLinesPerFile, 0.01);
    }

    [TestMethod]
    public async Task Handle_UsesQueryStoreOnce_ForAnalyticalReadComposition()
    {
        PullRequestEffectiveFilter? capturedFilter = null;
        var createdDate = DateTimeOffset.UtcNow.AddDays(-5);
        var pr1 = CreatePullRequest(1, "PR 1", "User1", createdDate, DateTimeOffset.UtcNow, "Completed");
        var pr2 = CreatePullRequest(2, "PR 2", "User2", createdDate.AddDays(1), null, "Active");

        _mockQueryStore.Setup(r => r.GetMetricsDataAsync(
                It.IsAny<PullRequestEffectiveFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<PullRequestEffectiveFilter, CancellationToken>((filter, _) => capturedFilter = filter)
            .ReturnsAsync(new PullRequestMetricsQueryData(
                [pr1, pr2],
                GroupByPullRequestId(
                    [
                        CreateIteration(1, 1, createdDate, createdDate.AddHours(2)),
                        CreateIteration(2, 1, createdDate.AddDays(1), createdDate.AddDays(1).AddHours(1))
                    ],
                    iteration => iteration.PullRequestId),
                GroupByPullRequestId(
                    [
                        CreateComment(1, 1, "Author1", createdDate.AddHours(1), false),
                        CreateComment(2, 2, "Author2", createdDate.AddDays(1).AddHours(1), true)
                    ],
                    comment => comment.PullRequestId),
                GroupByPullRequestId(
                    [
                        CreateFileChange(1, 1, "File1.cs", 10, 2, 0),
                        CreateFileChange(2, 1, "File2.cs", 20, 4, 0)
                    ],
                    fileChange => fileChange.PullRequestId)));

        var result = (await _handler.Handle(CreateQuery(), CancellationToken.None)).ToList();

        Assert.HasCount(2, result);
        Assert.IsNotNull(capturedFilter);
        CollectionAssert.AreEqual(new[] { "TestRepo" }, capturedFilter.RepositoryScope.ToArray());
        _mockQueryStore.Verify(
            r => r.GetMetricsDataAsync(It.IsAny<PullRequestEffectiveFilter>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Handle_WithoutExplicitDateRange_PassesNullTimeBoundsToQueryStore()
    {
        PullRequestEffectiveFilter? capturedFilter = null;
        _mockQueryStore.Setup(r => r.GetMetricsDataAsync(
                It.IsAny<PullRequestEffectiveFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<PullRequestEffectiveFilter, CancellationToken>((filter, _) => capturedFilter = filter)
            .ReturnsAsync(new PullRequestMetricsQueryData([], GroupByPullRequestId(Array.Empty<PullRequestIterationDto>(), x => x.PullRequestId), GroupByPullRequestId(Array.Empty<PullRequestCommentDto>(), x => x.PullRequestId), GroupByPullRequestId(Array.Empty<PullRequestFileChangeDto>(), x => x.PullRequestId)));

        _ = await _handler.Handle(CreateQuery(), CancellationToken.None);

        Assert.IsNotNull(capturedFilter);
        Assert.IsNull(capturedFilter.RangeStartUtc);
        Assert.IsNull(capturedFilter.RangeEndUtc);
    }

    [TestMethod]
    public async Task Handle_WithExplicitFromDate_UsesProvidedDate()
    {
        PullRequestEffectiveFilter? capturedFilter = null;
        var explicitFromDate = DateTimeOffset.UtcNow.AddMonths(-3);

        _mockQueryStore.Setup(r => r.GetMetricsDataAsync(
                It.IsAny<PullRequestEffectiveFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<PullRequestEffectiveFilter, CancellationToken>((filter, _) => capturedFilter = filter)
            .ReturnsAsync(new PullRequestMetricsQueryData([], GroupByPullRequestId(Array.Empty<PullRequestIterationDto>(), x => x.PullRequestId), GroupByPullRequestId(Array.Empty<PullRequestCommentDto>(), x => x.PullRequestId), GroupByPullRequestId(Array.Empty<PullRequestFileChangeDto>(), x => x.PullRequestId)));

        _ = await _handler.Handle(CreateQuery(rangeStartUtc: explicitFromDate), CancellationToken.None);

        Assert.IsNotNull(capturedFilter);
        Assert.AreEqual(explicitFromDate, capturedFilter.RangeStartUtc);
    }

    [TestMethod]
    public async Task Handle_PassesRepositoryScopeToQueryStore()
    {
        PullRequestEffectiveFilter? capturedFilter = null;
        var repositories = new[] { "Repo-A", "Repo-B", "Repo-C" };

        _mockQueryStore.Setup(r => r.GetMetricsDataAsync(
                It.IsAny<PullRequestEffectiveFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<PullRequestEffectiveFilter, CancellationToken>((filter, _) => capturedFilter = filter)
            .ReturnsAsync(new PullRequestMetricsQueryData([], GroupByPullRequestId(Array.Empty<PullRequestIterationDto>(), x => x.PullRequestId), GroupByPullRequestId(Array.Empty<PullRequestCommentDto>(), x => x.PullRequestId), GroupByPullRequestId(Array.Empty<PullRequestFileChangeDto>(), x => x.PullRequestId)));

        _ = await _handler.Handle(CreateQuery(repositoryScope: repositories), CancellationToken.None);

        Assert.IsNotNull(capturedFilter);
        CollectionAssert.AreEqual(repositories, capturedFilter.RepositoryScope.ToArray());
    }

    [TestMethod]
    public async Task Handle_WithOpenEndedEffectiveRange_PassesNullUpperBound()
    {
        PullRequestEffectiveFilter? capturedFilter = null;

        _mockQueryStore.Setup(r => r.GetMetricsDataAsync(
                It.IsAny<PullRequestEffectiveFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<PullRequestEffectiveFilter, CancellationToken>((filter, _) => capturedFilter = filter)
            .ReturnsAsync(new PullRequestMetricsQueryData([], GroupByPullRequestId(Array.Empty<PullRequestIterationDto>(), x => x.PullRequestId), GroupByPullRequestId(Array.Empty<PullRequestCommentDto>(), x => x.PullRequestId), GroupByPullRequestId(Array.Empty<PullRequestFileChangeDto>(), x => x.PullRequestId)));

        _ = await _handler.Handle(CreateQuery(rangeStartUtc: DateTimeOffset.UtcNow.AddDays(-14)), CancellationToken.None);

        Assert.IsNotNull(capturedFilter);
        Assert.IsNull(capturedFilter.RangeEndUtc);
    }

    [TestMethod]
    public async Task Handle_WithClosedEffectiveRange_PassesUpperBoundToQueryStore()
    {
        PullRequestEffectiveFilter? capturedFilter = null;
        var explicitToDate = DateTimeOffset.UtcNow.AddDays(-1);

        _mockQueryStore.Setup(r => r.GetMetricsDataAsync(
                It.IsAny<PullRequestEffectiveFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<PullRequestEffectiveFilter, CancellationToken>((filter, _) => capturedFilter = filter)
            .ReturnsAsync(new PullRequestMetricsQueryData([], GroupByPullRequestId(Array.Empty<PullRequestIterationDto>(), x => x.PullRequestId), GroupByPullRequestId(Array.Empty<PullRequestCommentDto>(), x => x.PullRequestId), GroupByPullRequestId(Array.Empty<PullRequestFileChangeDto>(), x => x.PullRequestId)));

        _ = await _handler.Handle(CreateQuery(
            rangeStartUtc: DateTimeOffset.UtcNow.AddDays(-7),
            rangeEndUtc: explicitToDate), CancellationToken.None);

        Assert.IsNotNull(capturedFilter);
        Assert.AreEqual(explicitToDate, capturedFilter.RangeEndUtc);
    }

    private static PullRequestDto CreatePullRequest(
        int id,
        string title,
        string createdBy,
        DateTimeOffset createdDate,
        DateTimeOffset? completedDate,
        string status)
    {
        return new PullRequestDto(
            Id: id,
            RepositoryName: "TestRepo",
            Title: title,
            CreatedBy: createdBy,
            CreatedDate: createdDate,
            CompletedDate: completedDate,
            Status: status,
            IterationPath: "TestIteration",
            SourceBranch: "feature/test",
            TargetBranch: "main",
            RetrievedAt: DateTimeOffset.UtcNow);
    }

    private static PullRequestIterationDto CreateIteration(
        int prId,
        int iterationNumber,
        DateTimeOffset createdDate,
        DateTimeOffset updatedDate)
    {
        return new PullRequestIterationDto(
            PullRequestId: prId,
            IterationNumber: iterationNumber,
            CreatedDate: createdDate,
            UpdatedDate: updatedDate,
            CommitCount: 1,
            ChangeCount: 1);
    }

    private static PullRequestCommentDto CreateComment(
        int id,
        int prId,
        string author,
        DateTimeOffset createdDate,
        bool isResolved)
    {
        return new PullRequestCommentDto(
            Id: id,
            PullRequestId: prId,
            ThreadId: id,
            Author: author,
            Content: $"Comment {id}",
            CreatedDate: createdDate,
            UpdatedDate: null,
            IsResolved: isResolved,
            ResolvedDate: isResolved ? createdDate.AddHours(1) : null,
            ResolvedBy: isResolved ? "Reviewer" : null);
    }

    private static PullRequestFileChangeDto CreateFileChange(
        int prId,
        int iterationId,
        string filePath,
        int linesAdded,
        int linesDeleted,
        int linesModified)
    {
        return new PullRequestFileChangeDto(
            PullRequestId: prId,
            IterationId: iterationId,
            FilePath: filePath,
            ChangeType: "Edit",
            LinesAdded: linesAdded,
            LinesDeleted: linesDeleted,
            LinesModified: linesModified);
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<TItem>> GroupByPullRequestId<TItem>(
        IEnumerable<TItem> items,
        Func<TItem, int> getPullRequestId)
    {
        return items
            .GroupBy(getPullRequestId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TItem>)group.ToList());
    }
}
