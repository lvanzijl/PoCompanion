using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services.Sync;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Tests for PullRequestSyncStage covering edge cases in multi-PR comment upsert.
/// </summary>
[TestClass]
public class PullRequestSyncStageTests
{
    /// <summary>
    /// Regression test for: "An item with the same key has already been added. Key: 1"
    /// TFS comment Ids are scoped per PR (every PR starts at Id=1), so when syncing
    /// multiple PRs the aggregated comment list contains duplicate Id values.
    /// UpsertCommentsAsync must use the composite (PullRequestId, Id) key.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_WithMultiplePrsHavingDuplicateCommentIds_SucceedsAndSavesAllComments()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"PullRequestSyncStageTests_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        var now = DateTimeOffset.UtcNow;

        // Two PRs, each with a comment having TFS Id = 1 (PR-scoped, not globally unique)
        var pr1 = new PullRequestDto(100, "TestRepo", "PR 100", "User1", now, null, "completed", "Sprint/1", "feature/100", "main", now);
        var pr2 = new PullRequestDto(101, "TestRepo", "PR 101", "User2", now, null, "completed", "Sprint/1", "feature/101", "main", now);

        var pr1Comments = new List<PullRequestCommentDto>
        {
            new(Id: 1, PullRequestId: 100, ThreadId: 1, Author: "User1", Content: "Comment on PR 100",
                CreatedDate: now, UpdatedDate: null, IsResolved: false, ResolvedDate: null, ResolvedBy: null)
        };

        var pr2Comments = new List<PullRequestCommentDto>
        {
            new(Id: 1, PullRequestId: 101, ThreadId: 1, Author: "User2", Content: "Comment on PR 101",
                CreatedDate: now, UpdatedDate: null, IsResolved: false, ResolvedDate: null, ResolvedBy: null)
        };

        var tfsClient = new Mock<ITfsClient>();
        tfsClient
            .Setup(c => c.GetPullRequestsAsync("TestRepo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pr1, pr2 });
        tfsClient
            .Setup(c => c.GetPullRequestIterationsAsync(100, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestIterationDto>());
        tfsClient
            .Setup(c => c.GetPullRequestIterationsAsync(101, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestIterationDto>());
        tfsClient
            .Setup(c => c.GetPullRequestCommentsAsync(100, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pr1Comments);
        tfsClient
            .Setup(c => c.GetPullRequestCommentsAsync(101, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pr2Comments);

        var logger = new Mock<ILogger<PullRequestSyncStage>>();
        var stage = new PullRequestSyncStage(tfsClient.Object, dbContext, logger.Object);

        var context = new SyncContext
        {
            ProductOwnerId = 1,
            RootWorkItemIds = Array.Empty<int>(),
            RepositoryNames = ["TestRepo"]
        };

        // Act - before fix, this threw: ArgumentException "An item with the same key has already been added. Key: 1"
        var result = await stage.ExecuteAsync(context, _ => { }, CancellationToken.None);

        // Assert - stage must succeed
        Assert.IsTrue(result.Success, $"Sync should succeed but failed with: {result.ErrorMessage}");

        var savedComments = await dbContext.PullRequestComments.ToListAsync();
        Assert.HasCount(2, savedComments, "Both comments must be saved despite sharing the same TFS comment Id");

        var pr100Comment = savedComments.Single(c => c.PullRequestId == 100);
        Assert.AreEqual(1, pr100Comment.Id);
        Assert.AreEqual("Comment on PR 100", pr100Comment.Content);

        var pr101Comment = savedComments.Single(c => c.PullRequestId == 101);
        Assert.AreEqual(1, pr101Comment.Id);
        Assert.AreEqual("Comment on PR 101", pr101Comment.Content);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenCommentFetchFails_StillPersistsWorkItemLinks()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"PullRequestSyncStageTests_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        var now = DateTimeOffset.UtcNow;
        var pr = new PullRequestDto(100, "TestRepo", "PR 100", "User1", now, null, "completed", "Sprint/1", "feature/100", "main", now);

        var tfsClient = new Mock<ITfsClient>();
        tfsClient
            .Setup(c => c.GetPullRequestsAsync("TestRepo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pr });
        tfsClient
            .Setup(c => c.GetPullRequestIterationsAsync(100, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestIterationDto>());
        tfsClient
            .Setup(c => c.GetPullRequestCommentsAsync(100, "TestRepo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("comments failed"));
        tfsClient
            .Setup(c => c.GetPullRequestWorkItemLinksAsync(100, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 300 });

        var logger = new Mock<ILogger<PullRequestSyncStage>>();
        var stage = new PullRequestSyncStage(tfsClient.Object, dbContext, logger.Object);

        var context = new SyncContext
        {
            ProductOwnerId = 1,
            RootWorkItemIds = Array.Empty<int>(),
            RepositoryNames = ["TestRepo"]
        };

        var result = await stage.ExecuteAsync(context, _ => { }, CancellationToken.None);

        Assert.IsTrue(result.Success, $"Sync should succeed but failed with: {result.ErrorMessage}");

        var savedLinks = await dbContext.PullRequestWorkItemLinks
            .Select(link => new { link.PullRequestId, link.WorkItemId })
            .ToListAsync();

        Assert.HasCount(1, savedLinks);
        Assert.AreEqual(100, savedLinks[0].PullRequestId);
        Assert.AreEqual(300, savedLinks[0].WorkItemId);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenNoNewPrs_BackfillsExistingNullProductIdsFromRepositoryMapping()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"PullRequestSyncStageTests_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new PoToolDbContext(options);

        dbContext.Products.Add(new ProductEntity
        {
            Id = 42,
            Name = "Product 42",
            ProductOwnerId = 1
        });
        dbContext.Repositories.Add(new RepositoryEntity
        {
            Id = 7,
            ProductId = 42,
            Name = "TestRepo",
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.PullRequests.Add(new PullRequestEntity
        {
            InternalId = 1,
            Id = 900,
            RepositoryName = "TestRepo",
            Title = "Existing PR",
            CreatedBy = "User1",
            CreatedDate = DateTimeOffset.UtcNow.AddDays(-3),
            CreatedDateUtc = DateTime.UtcNow.AddDays(-3),
            CompletedDate = null,
            Status = "active",
            IterationPath = "Sprint/1",
            SourceBranch = "feature/existing",
            TargetBranch = "main",
            RetrievedAt = DateTimeOffset.UtcNow.AddDays(-3),
            ProductId = null
        });
        await dbContext.SaveChangesAsync();

        var tfsClient = new Mock<ITfsClient>();
        tfsClient
            .Setup(c => c.GetPullRequestsAsync("TestRepo", It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestDto>());

        var logger = new Mock<ILogger<PullRequestSyncStage>>();
        var stage = new PullRequestSyncStage(tfsClient.Object, dbContext, logger.Object);

        var context = new SyncContext
        {
            ProductOwnerId = 1,
            RootWorkItemIds = Array.Empty<int>(),
            RepositoryNames = ["TestRepo"]
        };

        var result = await stage.ExecuteAsync(context, _ => { }, CancellationToken.None);

        Assert.IsTrue(result.Success, $"Sync should succeed but failed with: {result.ErrorMessage}");

        var persistedPr = await dbContext.PullRequests.SingleAsync(pr => pr.Id == 900);
        Assert.AreEqual(42, persistedPr.ProductId);
    }
}
