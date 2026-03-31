using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Shared.PullRequests;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class PullRequestMetricsServiceTests
{
    private PullRequestMetricsService _service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _service = new PullRequestMetricsService();
    }

    private PullRequestMetricsDto CreateMetrics(int id, double daysOpen, int iterations, int files, string status = "Active")
    {
        return new PullRequestMetricsDto
        {
            PullRequestId = id,
            Title = $"PR {id}",
            CreatedBy = $"User{id % 3}", // Cycle through 3 users
            Status = status,
            TotalTimeOpen = TimeSpan.FromDays(daysOpen),
            IterationCount = iterations,
            TotalFileCount = files,
            CommentCount = 5,
            CreatedDate = DateTime.UtcNow.AddDays(-daysOpen)
        };
    }

    [TestMethod]
    public void CalculateAverageTimeOpen_EmptyList_ReturnsZero()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>();

        // Act
        var result = _service.CalculateAverageTimeOpen(metrics);

        // Assert
        Assert.AreEqual("0d", result);
    }

    [TestMethod]
    public void CalculateAverageTimeOpen_WithData_ReturnsFormattedAverage()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetrics(1, 2.0, 1, 10),
            CreateMetrics(2, 4.0, 2, 15),
            CreateMetrics(3, 6.0, 3, 20)
        };

        // Act
        var result = _service.CalculateAverageTimeOpen(metrics);

        // Assert
        Assert.AreEqual("4.0d", result); // (2 + 4 + 6) / 3 = 4
    }

    [TestMethod]
    public void CalculateAverageIterations_EmptyList_ReturnsZero()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>();

        // Act
        var result = _service.CalculateAverageIterations(metrics);

        // Assert
        Assert.AreEqual("0", result);
    }

    [TestMethod]
    public void CalculateAverageIterations_WithData_ReturnsFormattedAverage()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetrics(1, 2.0, 1, 10),
            CreateMetrics(2, 4.0, 2, 15),
            CreateMetrics(3, 6.0, 3, 20)
        };

        // Act
        var result = _service.CalculateAverageIterations(metrics);

        // Assert
        Assert.AreEqual("2.0", result); // (1 + 2 + 3) / 3 = 2
    }

    [TestMethod]
    public void CalculateAverageFiles_EmptyList_ReturnsZero()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>();

        // Act
        var result = _service.CalculateAverageFiles(metrics);

        // Assert
        Assert.AreEqual("0", result);
    }

    [TestMethod]
    public void CalculateAverageFiles_WithData_ReturnsFormattedAverage()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetrics(1, 2.0, 1, 10),
            CreateMetrics(2, 4.0, 2, 15),
            CreateMetrics(3, 6.0, 3, 20)
        };

        // Act
        var result = _service.CalculateAverageFiles(metrics);

        // Assert
        Assert.AreEqual("15", result); // (10 + 15 + 20) / 3 = 15
    }

    [TestMethod]
    public void FilterByDateRange_NoDateRange_ReturnsAllMetrics()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetrics(1, 2.0, 1, 10),
            CreateMetrics(2, 4.0, 2, 15)
        };

        // Act
        var result = _service.FilterByDateRange(metrics, null, null).ToList();

        // Assert
        Assert.HasCount(2, result);
    }

    [TestMethod]
    public void FilterByDateRange_WithStartDate_FiltersCorrectly()
    {
        // Arrange
        var baseDate = DateTime.UtcNow;
        var metrics = new List<PullRequestMetricsDto>
        {
            new() { PullRequestId = 1, Title = "Old PR", CreatedDate = baseDate.AddDays(-10) },
            new() { PullRequestId = 2, Title = "Recent PR", CreatedDate = baseDate.AddDays(-2) }
        };
        var startDate = baseDate.AddDays(-5);

        // Act
        var result = _service.FilterByDateRange(metrics, startDate, null).ToList();

        // Assert
        Assert.HasCount(1, result);
        Assert.AreEqual(2, result[0].PullRequestId);
    }

    [TestMethod]
    public void FilterByDateRange_WithEndDate_FiltersCorrectly()
    {
        // Arrange
        var baseDate = DateTime.UtcNow;
        var metrics = new List<PullRequestMetricsDto>
        {
            new() { PullRequestId = 1, Title = "Old PR", CreatedDate = baseDate.AddDays(-10) },
            new() { PullRequestId = 2, Title = "Recent PR", CreatedDate = baseDate.AddDays(-2) }
        };
        var endDate = baseDate.AddDays(-5);

        // Act
        var result = _service.FilterByDateRange(metrics, null, endDate).ToList();

        // Assert
        Assert.HasCount(1, result);
        Assert.AreEqual(1, result[0].PullRequestId);
    }

    [TestMethod]
    public void GetStatusChartData_EmptyList_ReturnsNoDataPlaceholder()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>();

        // Act
        var (data, labels) = _service.GetStatusChartData(metrics);

        // Assert
        Assert.HasCount(1, data);
        Assert.AreEqual(0.0, data[0]);
        Assert.HasCount(1, labels);
        Assert.AreEqual("No data", labels[0]);
    }

    [TestMethod]
    public void GetStatusChartData_WithData_ReturnsGroupedByStatus()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetrics(1, 2.0, 1, 10, "Active"),
            CreateMetrics(2, 4.0, 2, 15, "Active"),
            CreateMetrics(3, 6.0, 3, 20, "Completed")
        };

        // Act
        var (data, labels) = _service.GetStatusChartData(metrics);

        // Assert
        Assert.HasCount(2, data);
        Assert.HasCount(2, labels);

#pragma warning disable MSTEST0037
        Assert.IsTrue(data.Contains(2.0)); // 2 Active

#pragma warning disable MSTEST0037
        Assert.IsTrue(data.Contains(1.0)); // 1 Completed
    }

    [TestMethod]
    public void GetTimeOpenChartData_ReturnsTop10OrderedByTimeOpen()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>();
        for (int i = 1; i <= 15; i++)
        {
            metrics.Add(CreateMetrics(i, i, 1, 10));
        }

        // Act
        var (data, labels) = _service.GetTimeOpenChartData(metrics);

        // Assert
        Assert.HasCount(10, data);
        Assert.HasCount(10, labels);
        Assert.AreEqual(15.0, data[0]); // Highest time open first
        Assert.AreEqual(6.0, data[9]); // 10th highest
    }

    [TestMethod]
    public void GetTimeOpenChartData_TruncatesLongTitles()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            new()
            {
                PullRequestId = 1,
                Title = "This is a very long pull request title that needs truncation",
                TotalTimeOpen = TimeSpan.FromDays(5),
                IterationCount = 1,
                TotalFileCount = 10,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "User1",
                Status = "Active",
                CommentCount = 5
            }
        };

        // Act
        var (_, labels) = _service.GetTimeOpenChartData(metrics);

        // Assert
        Assert.HasCount(1, labels);
        Assert.IsTrue(labels[0].EndsWith("..."), $"Label should end with '...', but was: {labels[0]}");
        Assert.IsLessThanOrEqualTo(labels[0].Length, 30);
    }

    [TestMethod]
    public void GetPRsByUserChartData_GroupsByUser()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>
        {
            CreateMetrics(1, 2.0, 1, 10), // User0
            CreateMetrics(2, 4.0, 2, 15), // User1
            CreateMetrics(3, 6.0, 3, 20), // User2
            CreateMetrics(4, 8.0, 4, 25), // User0
        };

        // Act
        var (data, labels) = _service.GetPRsByUserChartData(metrics);

        // Assert
        Assert.HasCount(3, data); // 3 unique users
        Assert.HasCount(3, labels);

#pragma warning disable MSTEST0037
        Assert.IsTrue(data.Contains(2.0)); // User0 has 2 PRs
    }

    [TestMethod]
    public void CalculateMetricsHashCode_EmptyList_ReturnsZero()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>();

        // Act
        var hash = _service.CalculateMetricsHashCode(metrics);

        // Assert
        Assert.AreEqual(0, hash);
    }

    [TestMethod]
    public void CalculateMetricsHashCode_SameData_ReturnsSameHash()
    {
        // Arrange
        var metrics1 = new List<PullRequestMetricsDto>
        {
            CreateMetrics(1, 2.0, 1, 10),
            CreateMetrics(2, 4.0, 2, 15)
        };
        var metrics2 = new List<PullRequestMetricsDto>
        {
            CreateMetrics(1, 2.0, 1, 10),
            CreateMetrics(2, 4.0, 2, 15)
        };

        // Act
        var hash1 = _service.CalculateMetricsHashCode(metrics1);
        var hash2 = _service.CalculateMetricsHashCode(metrics2);

        // Assert
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void CalculateMetricsHashCode_DifferentData_ReturnsDifferentHash()
    {
        // Arrange
        var metrics1 = new List<PullRequestMetricsDto>
        {
            CreateMetrics(1, 2.0, 1, 10)
        };
        var metrics2 = new List<PullRequestMetricsDto>
        {
            CreateMetrics(2, 4.0, 2, 15)
        };

        // Act
        var hash1 = _service.CalculateMetricsHashCode(metrics1);
        var hash2 = _service.CalculateMetricsHashCode(metrics2);

        // Assert
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void GetTimeOpenChartData_EmptyList_ReturnsEmptyArrays()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>();

        // Act
        var (data, labels) = _service.GetTimeOpenChartData(metrics);

        // Assert
        Assert.HasCount(0, data);
        Assert.HasCount(0, labels);
    }

    [TestMethod]
    public void GetPRsByUserChartData_EmptyList_ReturnsEmptyArrays()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>();

        // Act
        var (data, labels) = _service.GetPRsByUserChartData(metrics);

        // Assert
        Assert.HasCount(0, data);
        Assert.HasCount(0, labels);
    }

    [TestMethod]
    public void GetStatusChartData_EmptyList_ReturnsNoDataIndicator()
    {
        // Arrange
        var metrics = new List<PullRequestMetricsDto>();

        // Act
        var (data, labels) = _service.GetStatusChartData(metrics);

        // Assert
        Assert.HasCount(1, data);
        Assert.AreEqual(0.0, data[0]);
        Assert.AreEqual("No data", labels[0]);
    }
}
