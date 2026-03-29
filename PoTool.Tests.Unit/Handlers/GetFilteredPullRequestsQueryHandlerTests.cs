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
public sealed class GetFilteredPullRequestsQueryHandlerTests
{
    private Mock<IPullRequestQueryStore> _mockQueryStore = null!;
    private GetFilteredPullRequestsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockQueryStore = new Mock<IPullRequestQueryStore>();
        _handler = new GetFilteredPullRequestsQueryHandler(
            _mockQueryStore.Object,
            Mock.Of<ILogger<GetFilteredPullRequestsQueryHandler>>());
    }

    [TestMethod]
    public async Task Handle_UsesQueryStoreScope_ThenAppliesLocalSelections()
    {
        PullRequestEffectiveFilter? capturedFilter = null;
        var filter = CreateFilter(
            createdBy: "alice",
            status: "completed",
            rangeStartUtc: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            rangeEndUtc: new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero));

        _mockQueryStore.Setup(store => store.GetScopedPullRequestsAsync(
                It.IsAny<PullRequestEffectiveFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<PullRequestEffectiveFilter, CancellationToken>((effectiveFilter, _) => capturedFilter = effectiveFilter)
            .ReturnsAsync(
            [
                CreatePullRequest(1, "alice", "completed"),
                CreatePullRequest(2, "bob", "completed"),
                CreatePullRequest(3, "alice", "active")
            ]);

        var result = (await _handler.Handle(new GetFilteredPullRequestsQuery(filter), CancellationToken.None)).ToList();

        Assert.IsNotNull(capturedFilter);
        CollectionAssert.AreEqual(new[] { "Repo-A", "Repo-B" }, capturedFilter.RepositoryScope.ToArray());
        Assert.HasCount(1, result);
        Assert.AreEqual(1, result[0].Id);
        _mockQueryStore.Verify(
            store => store.GetScopedPullRequestsAsync(It.IsAny<PullRequestEffectiveFilter>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Handle_WhenQueryStoreReturnsEmptyList_ReturnsEmptyList()
    {
        _mockQueryStore.Setup(store => store.GetScopedPullRequestsAsync(
                It.IsAny<PullRequestEffectiveFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestDto>());

        var result = await _handler.Handle(new GetFilteredPullRequestsQuery(CreateFilter()), CancellationToken.None);

        Assert.IsFalse(result.Any());
    }

    private static PullRequestEffectiveFilter CreateFilter(
        string? createdBy = null,
        string? status = null,
        DateTimeOffset? rangeStartUtc = null,
        DateTimeOffset? rangeEndUtc = null)
    {
        return new PullRequestEffectiveFilter(
            new PullRequestFilterContext(
                FilterSelection<int>.All(),
                FilterSelection<int>.All(),
                FilterSelection<string>.All(),
                FilterSelection<string>.All(),
                string.IsNullOrWhiteSpace(createdBy) ? FilterSelection<string>.All() : FilterSelection<string>.Selected([createdBy]),
                string.IsNullOrWhiteSpace(status) ? FilterSelection<string>.All() : FilterSelection<string>.Selected([status]),
                rangeStartUtc.HasValue || rangeEndUtc.HasValue
                    ? FilterTimeSelection.DateRange(rangeStartUtc, rangeEndUtc)
                    : FilterTimeSelection.None()),
            ["Repo-A", "Repo-B"],
            rangeStartUtc,
            rangeEndUtc,
            null,
            Array.Empty<int>());
    }

    private static PullRequestDto CreatePullRequest(int id, string createdBy, string status)
    {
        return new PullRequestDto(
            id,
            "Repo-A",
            $"PR-{id}",
            createdBy,
            new DateTimeOffset(2026, 2, id, 12, 0, 0, TimeSpan.Zero),
            status == "completed" ? new DateTimeOffset(2026, 2, id + 1, 12, 0, 0, TimeSpan.Zero) : null,
            status,
            "Sprint 1",
            "refs/heads/feature",
            "refs/heads/main",
            DateTimeOffset.UtcNow,
            null);
    }
}
