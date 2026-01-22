using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.PullRequests;
using PoTool.Api.Services;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetPullRequestMetricsQueryHandlerTests
{
    private Mock<IPullRequestReadProvider> _mockProvider = null!;
    private Mock<ILogger<GetPullRequestMetricsQueryHandler>> _mockLogger = null!;
    private GetPullRequestMetricsQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockProvider = new Mock<IPullRequestReadProvider>();
        _mockLogger = new Mock<ILogger<GetPullRequestMetricsQueryHandler>>();
        _handler = new GetPullRequestMetricsQueryHandler(
            _mockProvider.Object,
            _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoPullRequests_ReturnsEmptyList()
    {
        // Arrange
        _mockProvider.Setup(r => r.GetByProductIdsAsync(It.IsAny<List<int>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto>());
        var query = new GetPullRequestMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task Handle_WithSinglePullRequest_CalculatesMetricsCorrectly()
    {
        // Arrange
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

        _mockProvider.Setup(r => r.GetByProductIdsAsync(It.IsAny<List<int>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto> { pr });
        _mockProvider.Setup(r => r.GetIterationsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(iterations);
        _mockProvider.Setup(r => r.GetCommentsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(comments);
        _mockProvider.Setup(r => r.GetFileChangesAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileChanges);

        var query = new GetPullRequestMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
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
        // Arrange
        var createdDate = DateTimeOffset.UtcNow.AddDays(-3);
        var pr = CreatePullRequest(1, "Open PR", "TestUser", createdDate, null, "Active");

        _mockProvider.Setup(r => r.GetByProductIdsAsync(It.IsAny<List<int>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto> { pr });
        _mockProvider.Setup(r => r.GetIterationsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestIterationDto>());
        _mockProvider.Setup(r => r.GetCommentsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestCommentDto>());
        _mockProvider.Setup(r => r.GetFileChangesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestFileChangeDto>());

        var query = new GetPullRequestMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.Single();
        // Use a more lenient check to avoid flaky timing issues (allow slight variations due to test execution time)
        // MSTest signature: IsGreaterThanOrEqualTo(lowerBound, value) checks if "lowerBound >= value"
        // We want to check if 2.99 <= metrics.TotalTimeOpen.TotalDays, so swap parameters
        Assert.IsGreaterThanOrEqualTo(2.99, metrics.TotalTimeOpen.TotalDays, 
            $"TotalTimeOpen should be approximately 3 days or more, but was {metrics.TotalTimeOpen.TotalDays}");
        Assert.IsNull(metrics.CompletedDate);
    }

    [TestMethod]
    public async Task Handle_WithNoIterations_ReturnsNullEffectiveWorkTime()
    {
        // Arrange
        var createdDate = DateTimeOffset.UtcNow.AddDays(-5);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "Simple PR", "TestUser", createdDate, completedDate, "Completed");

        _mockProvider.Setup(r => r.GetByProductIdsAsync(It.IsAny<List<int>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto> { pr });
        _mockProvider.Setup(r => r.GetIterationsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestIterationDto>());
        _mockProvider.Setup(r => r.GetCommentsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestCommentDto>());
        _mockProvider.Setup(r => r.GetFileChangesAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestFileChangeDto>());

        var query = new GetPullRequestMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.Single();
        Assert.IsNull(metrics.EffectiveWorkTime);
    }

    [TestMethod]
    public async Task Handle_WithNoFileChanges_ReturnsZeroFileMetrics()
    {
        // Arrange
        var createdDate = DateTimeOffset.UtcNow.AddDays(-2);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "No Files PR", "TestUser", createdDate, completedDate, "Completed");

        _mockProvider.Setup(r => r.GetByProductIdsAsync(It.IsAny<List<int>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto> { pr });
        _mockProvider.Setup(r => r.GetIterationsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestIterationDto>());
        _mockProvider.Setup(r => r.GetCommentsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestCommentDto>());
        _mockProvider.Setup(r => r.GetFileChangesAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestFileChangeDto>());

        var query = new GetPullRequestMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
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
        // Arrange
        var createdDate = DateTimeOffset.UtcNow.AddDays(-1);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "Multi-edit PR", "TestUser", createdDate, completedDate, "Completed");

        var fileChanges = new List<PullRequestFileChangeDto>
        {
            CreateFileChange(1, 1, "File1.cs", 10, 5, 0),
            CreateFileChange(1, 2, "File1.cs", 20, 10, 5), // Same file, different iteration
            CreateFileChange(1, 1, "File2.cs", 30, 0, 0)
        };

        _mockProvider.Setup(r => r.GetByProductIdsAsync(It.IsAny<List<int>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto> { pr });
        _mockProvider.Setup(r => r.GetIterationsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestIterationDto>());
        _mockProvider.Setup(r => r.GetCommentsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestCommentDto>());
        _mockProvider.Setup(r => r.GetFileChangesAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileChanges);

        var query = new GetPullRequestMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.Single();
        Assert.AreEqual(2, metrics.TotalFileCount); // File1.cs and File2.cs
        Assert.AreEqual(60, metrics.TotalLinesAdded); // 10 + 20 + 30
        Assert.AreEqual(15, metrics.TotalLinesDeleted); // 5 + 10 + 0
    }

    [TestMethod]
    public async Task Handle_WithAllCommentsResolved_ReturnsZeroUnresolved()
    {
        // Arrange
        var createdDate = DateTimeOffset.UtcNow.AddDays(-2);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "Resolved PR", "TestUser", createdDate, completedDate, "Completed");

        var comments = new List<PullRequestCommentDto>
        {
            CreateComment(1, 1, "Author1", createdDate.AddHours(1), true),
            CreateComment(2, 1, "Author2", createdDate.AddHours(2), true),
            CreateComment(3, 1, "Author3", createdDate.AddHours(3), true)
        };

        _mockProvider.Setup(r => r.GetByProductIdsAsync(It.IsAny<List<int>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto> { pr });
        _mockProvider.Setup(r => r.GetIterationsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestIterationDto>());
        _mockProvider.Setup(r => r.GetCommentsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(comments);
        _mockProvider.Setup(r => r.GetFileChangesAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestFileChangeDto>());

        var query = new GetPullRequestMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.Single();
        Assert.AreEqual(3, metrics.CommentCount);
        Assert.AreEqual(0, metrics.UnresolvedCommentCount);
    }

    [TestMethod]
    public async Task Handle_WithMultiplePullRequests_ReturnsAllMetrics()
    {
        // Arrange
        var createdDate1 = DateTimeOffset.UtcNow.AddDays(-5);
        var createdDate2 = DateTimeOffset.UtcNow.AddDays(-3);
        var pr1 = CreatePullRequest(1, "PR 1", "User1", createdDate1, DateTimeOffset.UtcNow, "Completed");
        var pr2 = CreatePullRequest(2, "PR 2", "User2", createdDate2, null, "Active");

        _mockProvider.Setup(r => r.GetByProductIdsAsync(It.IsAny<List<int>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto> { pr1, pr2 });
        _mockProvider.Setup(r => r.GetIterationsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestIterationDto>());
        _mockProvider.Setup(r => r.GetCommentsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestCommentDto>());
        _mockProvider.Setup(r => r.GetFileChangesAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestFileChangeDto>());

        var query = new GetPullRequestMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Count());
        Assert.IsTrue(result.Any(m => m.PullRequestId == 1));
        Assert.IsTrue(result.Any(m => m.PullRequestId == 2));
    }

    [TestMethod]
    public async Task Handle_CalculatesAverageLinesPerFileCorrectly()
    {
        // Arrange
        var createdDate = DateTimeOffset.UtcNow.AddDays(-1);
        var completedDate = DateTimeOffset.UtcNow;
        var pr = CreatePullRequest(1, "Avg Lines PR", "TestUser", createdDate, completedDate, "Completed");

        var fileChanges = new List<PullRequestFileChangeDto>
        {
            CreateFileChange(1, 1, "File1.cs", 100, 50, 0),  // 100 added + 50 deleted = 150 lines changed
            CreateFileChange(1, 1, "File2.cs", 50, 25, 0),   // 50 added + 25 deleted = 75 lines changed
            CreateFileChange(1, 1, "File3.cs", 25, 0, 0)     // 25 added + 0 deleted = 25 lines changed
        };

        _mockProvider.Setup(r => r.GetByProductIdsAsync(It.IsAny<List<int>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto> { pr });
        _mockProvider.Setup(r => r.GetIterationsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestIterationDto>());
        _mockProvider.Setup(r => r.GetCommentsAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestCommentDto>());
        _mockProvider.Setup(r => r.GetFileChangesAsync(1, "TestRepo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileChanges);

        var query = new GetPullRequestMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        var metrics = result.Single();
        Assert.AreEqual(3, metrics.TotalFileCount);
        Assert.AreEqual(175, metrics.TotalLinesAdded);
        Assert.AreEqual(75, metrics.TotalLinesDeleted);
        // Average lines changed per file = (150 + 75 + 25) / 3 = 83.33
        Assert.AreEqual(83.33, metrics.AverageLinesPerFile, 0.01);
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
            RetrievedAt: DateTimeOffset.UtcNow
        );
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
            ChangeCount: 1
        );
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
            ResolvedBy: isResolved ? "Reviewer" : null
        );
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
            LinesModified: linesModified
        );
    }

    [TestMethod]
    public async Task Handle_Enforces6MonthTimeWindow()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var cutoffDate = now.AddMonths(-6);
        DateTimeOffset? capturedFromDate = null;
        
        // Setup mock to capture the fromDate parameter
        _mockProvider.Setup(r => r.GetByProductIdsAsync(
            It.IsAny<List<int>>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()))
            .Callback<List<int>, DateTimeOffset?, CancellationToken>((_, fromDate, _) => 
            {
                capturedFromDate = fromDate;
            })
            .ReturnsAsync(new List<PullRequestDto>());

        var query = new GetPullRequestMetricsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(capturedFromDate, "FromDate should be set");
        Assert.IsTrue(capturedFromDate.Value >= cutoffDate.AddMinutes(-1), 
            "FromDate should be approximately 6 months ago");
        Assert.IsTrue(capturedFromDate.Value <= now, 
            "FromDate should not be in the future");
    }

    [TestMethod]
    public async Task Handle_WithExplicitFromDate_UsesProvidedDate()
    {
        // Arrange
        var explicitFromDate = DateTimeOffset.UtcNow.AddMonths(-3);
        DateTimeOffset? capturedFromDate = null;
        
        // Setup mock to capture the fromDate parameter
        _mockProvider.Setup(r => r.GetByProductIdsAsync(
            It.IsAny<List<int>>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()))
            .Callback<List<int>, DateTimeOffset?, CancellationToken>((_, fromDate, _) => 
            {
                capturedFromDate = fromDate;
            })
            .ReturnsAsync(new List<PullRequestDto>());

        var query = new GetPullRequestMetricsQuery(null, explicitFromDate);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(capturedFromDate, "FromDate should be set");
        Assert.AreEqual(explicitFromDate, capturedFromDate, 
            "Should use the explicitly provided FromDate");
    }

    [TestMethod]
    public async Task Handle_WithProductIds_PassesProductIdsToProvider()
    {
        // Arrange
        var productIds = new List<int> { 1, 2, 3 };
        List<int>? capturedProductIds = null;
        
        // Setup mock to capture the productIds parameter
        _mockProvider.Setup(r => r.GetByProductIdsAsync(
            It.IsAny<List<int>>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()))
            .Callback<List<int>, DateTimeOffset?, CancellationToken>((ids, _, _) => 
            {
                capturedProductIds = ids;
            })
            .ReturnsAsync(new List<PullRequestDto>());

        var query = new GetPullRequestMetricsQuery(productIds);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(capturedProductIds, "ProductIds should be passed to provider");
        CollectionAssert.AreEqual(productIds, capturedProductIds, 
            "Should pass the product IDs to the provider");
    }

    [TestMethod]
    public async Task Handle_WithNullProductIds_PassesNullToProvider()
    {
        // Arrange
        List<int>? capturedProductIds = new List<int> { -1 }; // Initialize with dummy value
        
        // Setup mock to capture the productIds parameter
        _mockProvider.Setup(r => r.GetByProductIdsAsync(
            It.IsAny<List<int>>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()))
            .Callback<List<int>, DateTimeOffset?, CancellationToken>((ids, _, _) => 
            {
                capturedProductIds = ids;
            })
            .ReturnsAsync(new List<PullRequestDto>());

        var query = new GetPullRequestMetricsQuery(null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNull(capturedProductIds, 
            "ProductIds should be null when not specified (all products)");
    }

    [TestMethod]
    public async Task Handle_WithSingleProductId_PassesSingleIdToProvider()
    {
        // Arrange
        var productIds = new List<int> { 42 };
        List<int>? capturedProductIds = null;
        
        // Setup mock to capture the productIds parameter
        _mockProvider.Setup(r => r.GetByProductIdsAsync(
            It.IsAny<List<int>>(), 
            It.IsAny<DateTimeOffset?>(), 
            It.IsAny<CancellationToken>()))
            .Callback<List<int>, DateTimeOffset?, CancellationToken>((ids, _, _) => 
            {
                capturedProductIds = ids;
            })
            .ReturnsAsync(new List<PullRequestDto>());

        var query = new GetPullRequestMetricsQuery(productIds);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(capturedProductIds, "ProductIds should be passed to provider");
        Assert.HasCount(1, capturedProductIds, "Should have single product ID");
        Assert.AreEqual(42, capturedProductIds[0], "Should pass correct product ID");
    }
}
