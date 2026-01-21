using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Tests for LivePullRequestReadProvider to verify product scope filtering and time window logic.
/// </summary>
[TestClass]
public class LivePullRequestReadProviderTests
{
    private Mock<ITfsClient> _mockTfsClient = null!;
    private Mock<ILogger<LivePullRequestReadProvider>> _mockLogger = null!;
    private LivePullRequestReadProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTfsClient = new Mock<ITfsClient>();
        _mockLogger = new Mock<ILogger<LivePullRequestReadProvider>>();
        _provider = new LivePullRequestReadProvider(
            _mockTfsClient.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task GetAllAsync_WithFromDate_PassesFromDateToTfsClient()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        DateTimeOffset? capturedFromDate = null;
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .Callback<string?, DateTimeOffset?, DateTimeOffset?, CancellationToken>((_, from, _, _) =>
            {
                capturedFromDate = from;
            })
            .ReturnsAsync(new List<PullRequestDto>());

        // Act
        await _provider.GetAllAsync(fromDate);

        // Assert
        Assert.AreEqual(fromDate, capturedFromDate, "Should pass fromDate to TFS client");
    }

    [TestMethod]
    public async Task GetAllAsync_WithoutFromDate_PassesNullToTfsClient()
    {
        // Arrange
        DateTimeOffset? capturedFromDate = DateTimeOffset.UtcNow; // Initialize with dummy value
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .Callback<string?, DateTimeOffset?, DateTimeOffset?, CancellationToken>((_, from, _, _) =>
            {
                capturedFromDate = from;
            })
            .ReturnsAsync(new List<PullRequestDto>());

        // Act
        await _provider.GetAllAsync(null);

        // Assert
        Assert.IsNull(capturedFromDate, "Should pass null fromDate to TFS client");
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithNullProductIds_ReturnsAllPullRequests()
    {
        // Arrange
        var allPrs = new List<PullRequestDto>
        {
            CreatePullRequest(1, "PR 1", productId: 1),
            CreatePullRequest(2, "PR 2", productId: 2),
            CreatePullRequest(3, "PR 3", productId: 3)
        };
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPrs);

        // Act
        var result = await _provider.GetByProductIdsAsync(null);

        // Assert
        Assert.HasCount(3, result, "Should return all PRs when productIds is null");
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithEmptyProductIds_ReturnsAllPullRequests()
    {
        // Arrange
        var allPrs = new List<PullRequestDto>
        {
            CreatePullRequest(1, "PR 1", productId: 1),
            CreatePullRequest(2, "PR 2", productId: 2),
            CreatePullRequest(3, "PR 3", productId: 3)
        };
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPrs);

        // Act
        var result = await _provider.GetByProductIdsAsync(new List<int>());

        // Assert
        Assert.HasCount(3, result, "Should return all PRs when productIds is empty");
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithSingleProductId_FiltersCorrectly()
    {
        // Arrange
        var allPrs = new List<PullRequestDto>
        {
            CreatePullRequest(1, "PR 1", productId: 1),
            CreatePullRequest(2, "PR 2", productId: 2),
            CreatePullRequest(3, "PR 3", productId: 1),
            CreatePullRequest(4, "PR 4", productId: 3)
        };
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPrs);

        // Act
        var result = await _provider.GetByProductIdsAsync(new List<int> { 1 });

        // Assert
        var resultList = result.ToList();
        Assert.HasCount(2, resultList, "Should return only PRs for product 1");
        Assert.IsTrue(resultList.All(pr => pr.ProductId == 1), "All PRs should belong to product 1");
        Assert.IsTrue(resultList.Any(pr => pr.Id == 1), "Should include PR 1");
        Assert.IsTrue(resultList.Any(pr => pr.Id == 3), "Should include PR 3");
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithMultipleProductIds_FiltersCorrectly()
    {
        // Arrange
        var allPrs = new List<PullRequestDto>
        {
            CreatePullRequest(1, "PR 1", productId: 1),
            CreatePullRequest(2, "PR 2", productId: 2),
            CreatePullRequest(3, "PR 3", productId: 3),
            CreatePullRequest(4, "PR 4", productId: 4)
        };
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPrs);

        // Act
        var result = await _provider.GetByProductIdsAsync(new List<int> { 1, 3 });

        // Assert
        var resultList = result.ToList();
        Assert.HasCount(2, resultList, "Should return only PRs for products 1 and 3");
        Assert.IsTrue(resultList.Any(pr => pr.Id == 1 && pr.ProductId == 1), "Should include PR 1");
        Assert.IsTrue(resultList.Any(pr => pr.Id == 3 && pr.ProductId == 3), "Should include PR 3");
        Assert.IsFalse(resultList.Any(pr => pr.ProductId == 2), "Should not include PRs from product 2");
        Assert.IsFalse(resultList.Any(pr => pr.ProductId == 4), "Should not include PRs from product 4");
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithPRsWithoutProductId_ExcludesThemFromFilteredResults()
    {
        // Arrange
        var allPrs = new List<PullRequestDto>
        {
            CreatePullRequest(1, "PR 1", productId: 1),
            CreatePullRequest(2, "PR 2", productId: null), // No product ID
            CreatePullRequest(3, "PR 3", productId: 2)
        };
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPrs);

        // Act
        var result = await _provider.GetByProductIdsAsync(new List<int> { 1, 2 });

        // Assert
        var resultList = result.ToList();
        Assert.HasCount(2, resultList, "Should return only PRs with matching product IDs");
        Assert.IsFalse(resultList.Any(pr => pr.Id == 2), "Should not include PR without product ID");
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithFromDate_PassesFromDateToTfsClient()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        DateTimeOffset? capturedFromDate = null;
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .Callback<string?, DateTimeOffset?, DateTimeOffset?, CancellationToken>((_, from, _, _) =>
            {
                capturedFromDate = from;
            })
            .ReturnsAsync(new List<PullRequestDto>());

        // Act
        await _provider.GetByProductIdsAsync(new List<int> { 1 }, fromDate);

        // Assert
        Assert.AreEqual(fromDate, capturedFromDate, "Should pass fromDate to TFS client");
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_CombinesProductFilterAndTimeFilter()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        var allPrs = new List<PullRequestDto>
        {
            CreatePullRequest(1, "PR 1", productId: 1),
            CreatePullRequest(2, "PR 2", productId: 2),
            CreatePullRequest(3, "PR 3", productId: 1)
        };
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            It.IsAny<string>(),
            fromDate,
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPrs);

        // Act
        var result = await _provider.GetByProductIdsAsync(new List<int> { 1 }, fromDate);

        // Assert
        var resultList = result.ToList();
        Assert.HasCount(2, resultList, "Should return only PRs for product 1 after fromDate");
        Assert.IsTrue(resultList.All(pr => pr.ProductId == 1), "All PRs should belong to product 1");
    }

    private static PullRequestDto CreatePullRequest(
        int id,
        string title,
        int? productId = null)
    {
        return new PullRequestDto(
            Id: id,
            RepositoryName: "TestRepo",
            Title: title,
            CreatedBy: "TestUser",
            CreatedDate: DateTimeOffset.UtcNow.AddDays(-7),
            CompletedDate: DateTimeOffset.UtcNow,
            Status: "Completed",
            IterationPath: "TestIteration",
            SourceBranch: "feature/test",
            TargetBranch: "main",
            RetrievedAt: DateTimeOffset.UtcNow,
            ProductId: productId
        );
    }
}
