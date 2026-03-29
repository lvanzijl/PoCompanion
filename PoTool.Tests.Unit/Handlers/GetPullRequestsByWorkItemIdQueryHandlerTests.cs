using Microsoft.EntityFrameworkCore;

using PoTool.Api.Handlers.PullRequests;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.PullRequests.Queries;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public sealed class GetPullRequestsByWorkItemIdQueryHandlerTests
{
    [TestMethod]
    public async Task Handle_ReturnsOnlyPullRequestsLinkedToRequestedWorkItem()
    {
        await using var context = CreateContext();
        context.PullRequests.AddRange(
            new PullRequestEntity
            {
                Id = 101,
                RepositoryName = "Battleship-Incident-Backend",
                Title = "PR 101",
                CreatedBy = "alice@example.com",
                CreatedDate = new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero),
                CreatedDateUtc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
                Status = "completed",
                IterationPath = "\\Battleship Systems\\2026\\Q1\\Sprint 4",
                SourceBranch = "refs/heads/feature/pr-101",
                TargetBranch = "refs/heads/main",
                RetrievedAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero)
            },
            new PullRequestEntity
            {
                Id = 102,
                RepositoryName = "Battleship-CrewSafety-UI",
                Title = "PR 102",
                CreatedBy = "bob@example.com",
                CreatedDate = new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero),
                CreatedDateUtc = new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc),
                Status = "completed",
                IterationPath = "\\Battleship Systems\\2026\\Q1\\Sprint 4",
                SourceBranch = "refs/heads/feature/pr-102",
                TargetBranch = "refs/heads/main",
                RetrievedAt = new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero)
            });

        context.PullRequestWorkItemLinks.AddRange(
            new PullRequestWorkItemLinkEntity { PullRequestId = 101, WorkItemId = 5001 },
            new PullRequestWorkItemLinkEntity { PullRequestId = 102, WorkItemId = 5002 },
            new PullRequestWorkItemLinkEntity { PullRequestId = 102, WorkItemId = 5001 });

        await context.SaveChangesAsync();

        var handler = new GetPullRequestsByWorkItemIdQueryHandler(new EfPullRequestQueryStore(context));

        var result = (await handler.Handle(new GetPullRequestsByWorkItemIdQuery(5001), CancellationToken.None)).ToList();

        Assert.HasCount(2, result);
        Assert.AreEqual(102, result[0].Id, "Newest linked PR should be returned first.");
        Assert.AreEqual(101, result[1].Id);
        Assert.IsTrue(result.All(pr => pr.Id is 101 or 102));
    }

    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase($"GetPullRequestsByWorkItemIdQueryHandlerTests-{Guid.NewGuid()}")
            .Options;

        return new PoToolDbContext(options);
    }
}
