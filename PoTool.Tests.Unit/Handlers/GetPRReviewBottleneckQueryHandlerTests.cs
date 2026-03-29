using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.PullRequests;
using PoTool.Api.Services;
using PoTool.Core.PullRequests.Queries;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetPRReviewBottleneckQueryHandlerTests
{
    private Mock<IPullRequestQueryStore> _mockQueryStore = null!;
    private GetPRReviewBottleneckQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockQueryStore = new Mock<IPullRequestQueryStore>();
        _handler = new GetPRReviewBottleneckQueryHandler(
            _mockQueryStore.Object,
            Mock.Of<ILogger<GetPRReviewBottleneckQueryHandler>>());
    }

    [TestMethod]
    public async Task Handle_UsesQueryStoreToLoadRecentPullRequests()
    {
        DateTime? capturedCutoffUtc = null;
        int? capturedMax = null;
        _mockQueryStore.Setup(store => store.GetReviewBottleneckPullRequestsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<DateTime, int, CancellationToken>((cutoffUtc, maxPullRequests, _) =>
            {
                capturedCutoffUtc = cutoffUtc;
                capturedMax = maxPullRequests;
            })
            .ReturnsAsync(
            [
                CreatePullRequest(1, "alice", DateTimeOffset.UtcNow.AddHours(-6), DateTimeOffset.UtcNow.AddHours(-2), "completed"),
                CreatePullRequest(2, "bob", DateTimeOffset.UtcNow.AddHours(-3), null, "active")
            ]);

        var result = await _handler.Handle(new GetPRReviewBottleneckQuery(25, 7), CancellationToken.None);

        Assert.IsNotNull(capturedCutoffUtc);
        Assert.AreEqual(25, capturedMax);
        Assert.HasCount(2, result.ReviewerPerformances);
        Assert.HasCount(1, result.PRsWaitingLongest);
        Assert.AreEqual("bob", result.PRsWaitingLongest[0].Author);
        _mockQueryStore.Verify(
            store => store.GetReviewBottleneckPullRequestsAsync(It.IsAny<DateTime>(), 25, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Handle_WhenStoreReturnsNoPullRequests_ReturnsEmptySummary()
    {
        _mockQueryStore.Setup(store => store.GetReviewBottleneckPullRequestsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestDto>());

        var result = await _handler.Handle(new GetPRReviewBottleneckQuery(10, 30), CancellationToken.None);

        Assert.IsEmpty(result.ReviewerPerformances);
        Assert.IsEmpty(result.PRsWaitingLongest);
        Assert.AreEqual(0d, result.Summary.AverageTimeToCompleteReviewsHours);
        Assert.AreEqual("None", result.Summary.BottleneckReviewer);
    }

    private static PullRequestDto CreatePullRequest(
        int id,
        string author,
        DateTimeOffset createdDate,
        DateTimeOffset? completedDate,
        string status)
    {
        return new PullRequestDto(
            id,
            "Repo-A",
            $"PR-{id}",
            author,
            createdDate,
            completedDate,
            status,
            "Sprint 1",
            "refs/heads/feature",
            "refs/heads/main",
            DateTimeOffset.UtcNow,
            null);
    }
}
