using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Blazor;

/// <summary>
/// Unit tests for PullRequestInsightsCalculator service
/// </summary>
[TestClass]
public class PullRequestInsightsCalculatorTests
{
    private PullRequestInsightsCalculator _calculator = null!;

    [TestInitialize]
    public void Setup()
    {
        _calculator = new PullRequestInsightsCalculator();
    }

    #region Lead Time to Merge Tests

    [TestMethod]
    public void CalculateLeadTimeToMerge_WithCompletedPRs_ReturnsCorrectMetrics()
    {
        // Arrange
        var prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, createdDate: DateTime.UtcNow.AddDays(-5), completedDate: DateTime.UtcNow, status: "Completed"),
            CreatePullRequest(2, createdDate: DateTime.UtcNow.AddDays(-3), completedDate: DateTime.UtcNow, status: "Completed"),
            CreatePullRequest(3, createdDate: DateTime.UtcNow.AddDays(-7), completedDate: DateTime.UtcNow, status: "Completed"),
        };
        // Metrics not needed for this test

        // Act
        var result = _calculator.CalculateLeadTimeToMerge(prs);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.IsNotNull(result.Median);
        Assert.IsNotNull(result.P75);
        Assert.AreEqual(5 * 24, result.Median.Value, 1.0); // Median should be around 5 days = 120 hours
        Assert.IsNull(result.Coverage); // Coverage not applicable for lead time
    }

    [TestMethod]
    public void CalculateLeadTimeToMerge_WithNonCompletedPRs_IgnoresThem()
    {
        // Arrange
        var prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, createdDate: DateTime.UtcNow.AddDays(-5), completedDate: DateTime.UtcNow, status: "Completed"),
            CreatePullRequest(2, createdDate: DateTime.UtcNow.AddDays(-3), completedDate: null, status: "Active"),
            CreatePullRequest(3, createdDate: DateTime.UtcNow.AddDays(-7), completedDate: DateTime.UtcNow, status: "Abandoned"),
        };
        // Metrics not needed for this test

        // Act
        var result = _calculator.CalculateLeadTimeToMerge(prs);

        // Assert
        Assert.AreEqual(1, result.Count); // Only 1 completed PR
    }

    [TestMethod]
    public void CalculateLeadTimeToMerge_WithNoPRs_ReturnsEmptyResult()
    {
        // Arrange
        var prs = new List<PullRequestDto>();
        // Metrics not needed for this test

        // Act
        var result = _calculator.CalculateLeadTimeToMerge(prs);

        // Assert
        Assert.AreEqual(0, result.Count);
        Assert.IsNull(result.Median);
        Assert.IsNull(result.P75);
    }

    #endregion

    #region Time to First Review Tests

    [TestMethod]
    public void CalculateTimeToFirstReview_WithComments_ReturnsCorrectMetrics()
    {
        // Arrange
        var prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, createdDate: DateTime.UtcNow.AddDays(-5)),
            CreatePullRequest(2, createdDate: DateTime.UtcNow.AddDays(-3)),
        };
        var prComments = new Dictionary<int, List<PullRequestCommentDto>>
        {
            { 1, new List<PullRequestCommentDto> 
                { 
                    CreateComment(1, 1, DateTime.UtcNow.AddDays(-4)) // 1 day after PR created
                } 
            },
            { 2, new List<PullRequestCommentDto> 
                { 
                    CreateComment(2, 2, DateTime.UtcNow.AddDays(-2)) // 1 day after PR created
                } 
            },
        };
        // Metrics not needed for this test

        // Act
        var result = _calculator.CalculateTimeToFirstReview(prs, prComments);

        // Assert
        Assert.AreEqual(2, result.Count);
        Assert.IsNotNull(result.Median);
        Assert.IsNotNull(result.P75);
        Assert.IsNotNull(result.Coverage);
        Assert.AreEqual(100.0, result.Coverage.Value, 0.1); // 100% coverage
    }

    [TestMethod]
    public void CalculateTimeToFirstReview_WithSomePRsWithoutComments_CalculatesCoverageCorrectly()
    {
        // Arrange
        var prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, createdDate: DateTime.UtcNow.AddDays(-5)),
            CreatePullRequest(2, createdDate: DateTime.UtcNow.AddDays(-3)),
            CreatePullRequest(3, createdDate: DateTime.UtcNow.AddDays(-2)),
        };
        var prComments = new Dictionary<int, List<PullRequestCommentDto>>
        {
            { 1, new List<PullRequestCommentDto> 
                { 
                    CreateComment(1, 1, DateTime.UtcNow.AddDays(-4))
                } 
            },
            // PR 2 and 3 have no comments
        };
        // Metrics not needed for this test

        // Act
        var result = _calculator.CalculateTimeToFirstReview(prs, prComments);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.IsNotNull(result.Coverage);
        Assert.AreEqual(33.3, result.Coverage.Value, 0.5); // 1 out of 3 = 33%
    }

    [TestMethod]
    public void CalculateTimeToFirstReview_WithNoPRsWithComments_ReturnsZeroCoverage()
    {
        // Arrange
        var prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, createdDate: DateTime.UtcNow.AddDays(-5)),
        };
        var prComments = new Dictionary<int, List<PullRequestCommentDto>>();
        // Metrics not needed for this test

        // Act
        var result = _calculator.CalculateTimeToFirstReview(prs, prComments);

        // Assert
        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(0.0, result.Coverage);
    }

    #endregion

    #region Review Duration Tests

    [TestMethod]
    public void CalculateReviewDuration_WithMultipleComments_ReturnsCorrectDuration()
    {
        // Arrange
        var prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, createdDate: DateTime.UtcNow.AddDays(-5)),
        };
        var prComments = new Dictionary<int, List<PullRequestCommentDto>>
        {
            { 1, new List<PullRequestCommentDto> 
                { 
                    CreateComment(1, 1, DateTime.UtcNow.AddDays(-4)), // First review
                    CreateComment(2, 1, DateTime.UtcNow.AddDays(-3)), // Second review
                    CreateComment(3, 1, DateTime.UtcNow.AddDays(-2)), // Last review
                } 
            },
        };
        // Metrics not needed for this test

        // Act
        var result = _calculator.CalculateReviewDuration(prs, prComments);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.IsNotNull(result.Median);
        Assert.AreEqual(2 * 24, result.Median.Value, 1.0); // 2 days duration = 48 hours
    }

    [TestMethod]
    public void CalculateReviewDuration_WithLessThanTwoComments_IgnoresPR()
    {
        // Arrange
        var prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, createdDate: DateTime.UtcNow.AddDays(-5)),
            CreatePullRequest(2, createdDate: DateTime.UtcNow.AddDays(-3)),
        };
        var prComments = new Dictionary<int, List<PullRequestCommentDto>>
        {
            { 1, new List<PullRequestCommentDto> 
                { 
                    CreateComment(1, 1, DateTime.UtcNow.AddDays(-4)), // Only 1 comment
                } 
            },
        };
        // Metrics not needed for this test

        // Act
        var result = _calculator.CalculateReviewDuration(prs, prComments);

        // Assert
        Assert.AreEqual(0, result.Count); // PR with < 2 comments ignored
    }

    #endregion

    #region PR Size Tests

    [TestMethod]
    public void CalculatePRSize_WithMetrics_ReturnsLinesAndFilesChanged()
    {
        // Arrange
        var prs = new List<PullRequestDto>();
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetric(1, linesAdded: 100, linesDeleted: 50, fileCount: 5),
            CreateMetric(2, linesAdded: 200, linesDeleted: 30, fileCount: 10),
            CreateMetric(3, linesAdded: 50, linesDeleted: 20, fileCount: 3),
        };

        // Act
        var (linesResult, filesResult) = _calculator.CalculatePRSize(metrics);

        // Assert
        Assert.AreEqual(3, linesResult.Count);
        Assert.IsNotNull(linesResult.Median);
        Assert.AreEqual(150, linesResult.Median.Value, 1.0); // Median lines: 70, 150, 230 -> 150
        
        Assert.AreEqual(3, filesResult.Count);
        Assert.IsNotNull(filesResult.Median);
        Assert.AreEqual(5, filesResult.Median.Value, 1.0); // Median files: 3, 5, 10 -> 5
    }

    [TestMethod]
    public void CalculatePRSize_WithNoMetrics_ReturnsEmptyResults()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>();

        // Act
        var (linesResult, filesResult) = _calculator.CalculatePRSize(metrics);

        // Assert
        Assert.AreEqual(0, linesResult.Count);
        Assert.AreEqual(0, filesResult.Count);
    }

    #endregion

    #region Rework Rate Tests

    [TestMethod]
    public void CalculateReworkRate_WithPostReviewCommits_ReturnsCorrectMetrics()
    {
        // Arrange
        var prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, createdDate: DateTime.UtcNow.AddDays(-5)),
        };
        var prComments = new Dictionary<int, List<PullRequestCommentDto>>
        {
            { 1, new List<PullRequestCommentDto> 
                { 
                    CreateComment(1, 1, DateTime.UtcNow.AddDays(-4)), // First review 1 day after PR
                } 
            },
        };
        var prIterations = new Dictionary<int, List<PullRequestIterationDto>>
        {
            { 1, new List<PullRequestIterationDto> 
                { 
                    CreateIteration(1, 1, DateTime.UtcNow.AddDays(-5), commitCount: 2), // Before review
                    CreateIteration(2, 1, DateTime.UtcNow.AddDays(-3), commitCount: 3), // After review
                    CreateIteration(3, 1, DateTime.UtcNow.AddDays(-2), commitCount: 1), // After review
                } 
            },
        };
        // Metrics not needed for this test

        // Act
        var result = _calculator.CalculateReworkRate(prs, prComments, prIterations);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.IsNotNull(result.Median);
        Assert.AreEqual(4, result.Median.Value, 0.1); // 3 + 1 = 4 commits after review
    }

    [TestMethod]
    public void CalculateReworkRate_WithPRsWithoutReview_CalculatesCoverageCorrectly()
    {
        // Arrange
        var prs = new List<PullRequestDto>
        {
            CreatePullRequest(1, createdDate: DateTime.UtcNow.AddDays(-5)),
            CreatePullRequest(2, createdDate: DateTime.UtcNow.AddDays(-3)),
        };
        var prComments = new Dictionary<int, List<PullRequestCommentDto>>
        {
            { 1, new List<PullRequestCommentDto> 
                { 
                    CreateComment(1, 1, DateTime.UtcNow.AddDays(-4)),
                } 
            },
            // PR 2 has no comments/review
        };
        var prIterations = new Dictionary<int, List<PullRequestIterationDto>>
        {
            { 1, new List<PullRequestIterationDto> 
                { 
                    CreateIteration(1, 1, DateTime.UtcNow.AddDays(-5), commitCount: 1),
                    CreateIteration(2, 1, DateTime.UtcNow.AddDays(-3), commitCount: 2),
                } 
            },
        };
        // Metrics not needed for this test

        // Act
        var result = _calculator.CalculateReworkRate(prs, prComments, prIterations);

        // Assert
        Assert.AreEqual(1, result.Count);
        Assert.IsNotNull(result.Coverage);
        Assert.AreEqual(50.0, result.Coverage.Value, 0.1); // 1 out of 2 = 50%
    }

    #endregion

    #region Helper Methods

    private PullRequestDto CreatePullRequest(
        int id,
        DateTime? createdDate = null,
        DateTime? completedDate = null,
        string status = "Active")
    {
        return new PullRequestDto
        {
            Id = id,
            RepositoryName = "TestRepo",
            Title = $"PR {id}",
            CreatedBy = "testuser",
            CreatedDate = createdDate ?? DateTime.UtcNow,
            CompletedDate = completedDate,
            Status = status,
            IterationPath = "Test",
            SourceBranch = "feature",
            TargetBranch = "main",
            RetrievedAt = DateTime.UtcNow,
            ProductId = 1
        };
    }

    private PullRequestCommentDto CreateComment(int id, int prId, DateTime createdDate)
    {
        return new PullRequestCommentDto
        {
            Id = id,
            PullRequestId = prId,
            ThreadId = id,
            Author = "reviewer",
            Content = "Test comment",
            CreatedDate = createdDate,
            UpdatedDate = null,
            IsResolved = false,
            ResolvedDate = null,
            ResolvedBy = null
        };
    }

    private PullRequestIterationDto CreateIteration(
        int iterationNumber,
        int prId,
        DateTime createdDate,
        int commitCount)
    {
        return new PullRequestIterationDto
        {
            PullRequestId = prId,
            IterationNumber = iterationNumber,
            CreatedDate = createdDate,
            UpdatedDate = createdDate.AddHours(1),
            CommitCount = commitCount,
            ChangeCount = commitCount * 5 // Arbitrary
        };
    }

    private PullRequestMetricsDto CreateMetric(
        int prId,
        int linesAdded = 0,
        int linesDeleted = 0,
        int fileCount = 0)
    {
        return new PullRequestMetricsDto
        {
            PullRequestId = prId,
            Title = $"PR {prId}",
            CreatedBy = "testuser",
            CreatedDate = DateTime.UtcNow,
            CompletedDate = null,
            Status = "Active",
            IterationPath = "Test",
            TotalTimeOpen = TimeSpan.FromDays(1),
            EffectiveWorkTime = TimeSpan.FromHours(8),
            IterationCount = 1,
            CommentCount = 0,
            UnresolvedCommentCount = 0,
            TotalFileCount = fileCount,
            TotalLinesAdded = linesAdded,
            TotalLinesDeleted = linesDeleted,
            AverageLinesPerFile = fileCount > 0 ? (double)(linesAdded + linesDeleted) / fileCount : 0
        };
    }

    #endregion
}
