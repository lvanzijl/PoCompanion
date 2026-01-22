using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Tests for LivePullRequestReadProvider to verify product scope filtering and time window logic.
/// </summary>
[TestClass]
public class LivePullRequestReadProviderTests
{
    private static readonly DateTimeOffset FixedTestTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    
    private Mock<ITfsClient> _mockTfsClient = null!;
    private Mock<IRepositoryConfigRepository> _mockRepositoryConfigRepository = null!;
    private Mock<ILogger<LivePullRequestReadProvider>> _mockLogger = null!;
    private LivePullRequestReadProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTfsClient = new Mock<ITfsClient>();
        _mockRepositoryConfigRepository = new Mock<IRepositoryConfigRepository>();
        _mockLogger = new Mock<ILogger<LivePullRequestReadProvider>>();
        _provider = new LivePullRequestReadProvider(
            _mockTfsClient.Object,
            _mockRepositoryConfigRepository.Object,
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
            CreatePullRequest(1, "PR 1", "Repo1", productId: 1),
            CreatePullRequest(2, "PR 2", "Repo2", productId: 2),
            CreatePullRequest(3, "PR 3", "Repo3", productId: 3)
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
        
        // Verify GetAllAsync path is taken (calls GetPullRequestsAsync with null repository)
        _mockTfsClient.Verify(c => c.GetPullRequestsAsync(
            null, 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithEmptyProductIds_ReturnsAllPullRequests()
    {
        // Arrange
        var allPrs = new List<PullRequestDto>
        {
            CreatePullRequest(1, "PR 1", "Repo1", productId: 1),
            CreatePullRequest(2, "PR 2", "Repo2", productId: 2),
            CreatePullRequest(3, "PR 3", "Repo3", productId: 3)
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
        
        // Verify GetAllAsync path is taken (calls GetPullRequestsAsync with null repository)
        _mockTfsClient.Verify(c => c.GetPullRequestsAsync(
            null, 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithSingleProductId_FetchesOnlyConfiguredRepositories()
    {
        // Arrange
        var productId = 1;
        var repo1Prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, "PR 1", "Repo1", productId: null),
            CreatePullRequest(3, "PR 3", "Repo1", productId: null)
        };
        var repo2Prs = new List<PullRequestDto>
        {
            CreatePullRequest(5, "PR 5", "Repo2", productId: null)
        };
        
        // Setup repository configuration for product 1
        var reposByProduct = new Dictionary<int, List<RepositoryDto>>
        {
            { productId, new List<RepositoryDto>
                {
                    new RepositoryDto(1, productId, "Repo1", FixedTestTime),
                    new RepositoryDto(2, productId, "Repo2", FixedTestTime)
                }
            }
        };
        
        _mockRepositoryConfigRepository.Setup(r => r.GetRepositoriesByProductIdsAsync(
            It.Is<List<int>>(ids => ids.Contains(productId)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(reposByProduct);
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            "Repo1",
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(repo1Prs);
            
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            "Repo2",
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(repo2Prs);

        // Act
        var result = await _provider.GetByProductIdsAsync(new List<int> { productId });

        // Assert
        var resultList = result.ToList();
        Assert.HasCount(3, resultList, "Should return PRs from both configured repositories");
        Assert.IsTrue(resultList.All(pr => pr.ProductId == productId), "All PRs should have ProductId set to 1");
        Assert.IsTrue(resultList.Any(pr => pr.Id == 1), "Should include PR 1 from Repo1");
        Assert.IsTrue(resultList.Any(pr => pr.Id == 3), "Should include PR 3 from Repo1");
        Assert.IsTrue(resultList.Any(pr => pr.Id == 5), "Should include PR 5 from Repo2");
        
        // Verify TFS was called only for configured repositories
        _mockTfsClient.Verify(c => c.GetPullRequestsAsync(
            "Repo1", 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
        _mockTfsClient.Verify(c => c.GetPullRequestsAsync(
            "Repo2", 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
        // Verify it did NOT call GetPullRequestsAsync with null (which would fetch all repos)
        _mockTfsClient.Verify(c => c.GetPullRequestsAsync(
            null, 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithMultipleProductIds_FetchesOnlyConfiguredRepositories()
    {
        // Arrange
        var product1Repo1Prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, "PR 1", "Product1Repo1", productId: null)
        };
        var product3Repo1Prs = new List<PullRequestDto>
        {
            CreatePullRequest(3, "PR 3", "Product3Repo1", productId: null)
        };
        
        // Setup repository configurations
        var reposByProduct = new Dictionary<int, List<RepositoryDto>>
        {
            { 1, new List<RepositoryDto>
                {
                    new RepositoryDto(1, 1, "Product1Repo1", FixedTestTime)
                }
            },
            { 3, new List<RepositoryDto>
                {
                    new RepositoryDto(3, 3, "Product3Repo1", FixedTestTime)
                }
            }
        };
        
        _mockRepositoryConfigRepository.Setup(r => r.GetRepositoriesByProductIdsAsync(
            It.Is<List<int>>(ids => ids.Contains(1) && ids.Contains(3)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(reposByProduct);
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            "Product1Repo1",
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(product1Repo1Prs);
            
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            "Product3Repo1",
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(product3Repo1Prs);

        // Act
        var result = await _provider.GetByProductIdsAsync(new List<int> { 1, 3 });

        // Assert
        var resultList = result.ToList();
        Assert.HasCount(2, resultList, "Should return PRs from both products' repositories");
        Assert.IsTrue(resultList.Any(pr => pr.Id == 1 && pr.ProductId == 1), "Should include PR 1 with ProductId 1");
        Assert.IsTrue(resultList.Any(pr => pr.Id == 3 && pr.ProductId == 3), "Should include PR 3 with ProductId 3");
        Assert.IsFalse(resultList.Any(pr => pr.ProductId == 2), "Should not include PRs from product 2");
        Assert.IsFalse(resultList.Any(pr => pr.ProductId == 4), "Should not include PRs from product 4");
        
        // Verify TFS was called only for configured repositories
        _mockTfsClient.Verify(c => c.GetPullRequestsAsync(
            "Product1Repo1", 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
        _mockTfsClient.Verify(c => c.GetPullRequestsAsync(
            "Product3Repo1", 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithEmptyRepositoryList_ReturnsEmptyResult()
    {
        // Arrange
        var productId = 5;
        
        // Setup: product has no configured repositories
        var reposByProduct = new Dictionary<int, List<RepositoryDto>>();
        
        _mockRepositoryConfigRepository.Setup(r => r.GetRepositoriesByProductIdsAsync(
            It.Is<List<int>>(ids => ids.Contains(productId)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(reposByProduct);

        // Act
        var result = await _provider.GetByProductIdsAsync(new List<int> { productId });

        // Assert
        var resultList = result.ToList();
        Assert.HasCount(0, resultList, "Should return empty result when product has no repositories");
        
        // Verify TFS was never called (no repositories to fetch from)
        _mockTfsClient.Verify(c => c.GetPullRequestsAsync(
            It.IsAny<string>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_WithFromDate_PassesFromDateToTfsClient()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        var productId = 1;
        DateTimeOffset? capturedFromDate = null;
        
        var reposByProduct = new Dictionary<int, List<RepositoryDto>>
        {
            { productId, new List<RepositoryDto>
                {
                    new RepositoryDto(1, productId, "TestRepo", FixedTestTime)
                }
            }
        };
        
        _mockRepositoryConfigRepository.Setup(r => r.GetRepositoriesByProductIdsAsync(
            It.Is<List<int>>(ids => ids.Contains(productId)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(reposByProduct);
        
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
        await _provider.GetByProductIdsAsync(new List<int> { productId }, fromDate);

        // Assert
        Assert.AreEqual(fromDate, capturedFromDate, "Should pass fromDate to TFS client");
    }

    [TestMethod]
    public async Task GetByProductIdsAsync_CombinesProductFilterAndTimeFilter()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        var productId = 1;
        var repoPrs = new List<PullRequestDto>
        {
            CreatePullRequest(1, "PR 1", "TestRepo", productId: null),
            CreatePullRequest(3, "PR 3", "TestRepo", productId: null)
        };
        
        var reposByProduct = new Dictionary<int, List<RepositoryDto>>
        {
            { productId, new List<RepositoryDto>
                {
                    new RepositoryDto(1, productId, "TestRepo", FixedTestTime)
                }
            }
        };
        
        _mockRepositoryConfigRepository.Setup(r => r.GetRepositoriesByProductIdsAsync(
            It.Is<List<int>>(ids => ids.Contains(productId)),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(reposByProduct);
        
        _mockTfsClient.Setup(c => c.GetPullRequestsAsync(
            "TestRepo",
            fromDate,
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoPrs);

        // Act
        var result = await _provider.GetByProductIdsAsync(new List<int> { productId }, fromDate);

        // Assert
        var resultList = result.ToList();
        Assert.HasCount(2, resultList, "Should return PRs for product 1 from configured repository");
        Assert.IsTrue(resultList.All(pr => pr.ProductId == productId), "All PRs should belong to product 1");
        
        // Verify the call was made with the correct repository and fromDate
        _mockTfsClient.Verify(c => c.GetPullRequestsAsync(
            "TestRepo",
            fromDate,
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static PullRequestDto CreatePullRequest(
        int id,
        string title,
        string repositoryName,
        int? productId = null)
    {
        return new PullRequestDto(
            Id: id,
            RepositoryName: repositoryName,
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
