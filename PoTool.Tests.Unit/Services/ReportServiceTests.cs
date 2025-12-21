using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class ReportServiceTests
{
    private ReportService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new ReportService();
    }

    [TestMethod]
    public void GenerateSummaryReport_EmptyCollection_ReturnsNoItemsMessage()
    {
        // Arrange
        var workItems = Array.Empty<WorkItemDto>();

        // Act
        var result = _service.GenerateSummaryReport(workItems);

        // Assert
        Assert.AreEqual("No work items selected.", result);
    }

    [TestMethod]
    public void GenerateSummaryReport_NullCollection_ReturnsNoItemsMessage()
    {
        // Act
        var result = _service.GenerateSummaryReport(null!);

        // Assert
        Assert.AreEqual("No work items selected.", result);
    }

    [TestMethod]
    public void GenerateSummaryReport_SingleItem_ReturnsCorrectReport()
    {
        // Arrange
        var workItems = new[]
        {
            new WorkItemDto
            {
                TfsId = 123,
                Title = "Test Item",
                Type = "Epic",
                State = "Active",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                ParentTfsId = null,
                Effort = 5,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            }
        };

        // Act
        var result = _service.GenerateSummaryReport(workItems);

        // Assert
        Assert.Contains("# Work Items Summary Report", result);
        Assert.Contains("**Total Items:** 1", result);
        Assert.Contains("## Summary by Type", result);
        Assert.Contains("**Epic:** 1", result);
        Assert.Contains("## Summary by State", result);
        Assert.Contains("**Active:** 1", result);
        Assert.Contains("## Effort Summary", result);
        Assert.Contains("**Total Effort:** 5", result);
        Assert.Contains("## Detailed List", result);
        Assert.Contains("123", result);
        Assert.Contains("Test Item", result);
    }

    [TestMethod]
    public void GenerateSummaryReport_MultipleItems_CalculatesCorrectSummaries()
    {
        // Arrange
        var workItems = new[]
        {
            new WorkItemDto
            {
                TfsId = 123,
                Title = "Epic 1",
                Type = "Epic",
                State = "Active",
                AreaPath = "Project\\Team A",
                IterationPath = "Sprint 1",
                ParentTfsId = null,
                Effort = 5,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            },
            new WorkItemDto
            {
                TfsId = 456,
                Title = "Feature 1",
                Type = "Feature",
                State = "Active",
                AreaPath = "Project\\Team A",
                IterationPath = "Sprint 1",
                ParentTfsId = 123,
                Effort = 3,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            },
            new WorkItemDto
            {
                TfsId = 789,
                Title = "Task 1",
                Type = "Task",
                State = "Done",
                AreaPath = "Project\\Team B",
                IterationPath = "Sprint 2",
                ParentTfsId = 456,
                Effort = 2,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            }
        };

        // Act
        var result = _service.GenerateSummaryReport(workItems);

        // Assert
        Assert.Contains("**Total Items:** 3", result);
        Assert.Contains("**Epic:** 1", result);
        Assert.Contains("**Feature:** 1", result);
        Assert.Contains("**Task:** 1", result);
        Assert.Contains("**Active:** 2", result);
        Assert.Contains("**Done:** 1", result);
        Assert.Contains("**Total Effort:** 10", result);
        Assert.Contains("**Average Effort:**", result);
    }

    [TestMethod]
    public void GenerateSummaryReport_ItemsWithoutEffort_HandlesCorrectly()
    {
        // Arrange
        var workItems = new[]
        {
            new WorkItemDto
            {
                TfsId = 123,
                Title = "Item with effort",
                Type = "Epic",
                State = "Active",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                ParentTfsId = null,
                Effort = 5,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            },
            new WorkItemDto
            {
                TfsId = 456,
                Title = "Item without effort",
                Type = "Feature",
                State = "Active",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                ParentTfsId = 123,
                Effort = null,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            }
        };

        // Act
        var result = _service.GenerateSummaryReport(workItems);

        // Assert
        Assert.Contains("**Items with Effort:** 1 of 2", result);
        Assert.Contains("**Total Effort:** 5", result);
    }

    [TestMethod]
    public void GenerateSummaryReport_GroupsByAreaPath_ShowsTop10()
    {
        // Arrange
        var workItems = new[]
        {
            new WorkItemDto
            {
                TfsId = 123,
                Title = "Item 1",
                Type = "Epic",
                State = "Active",
                AreaPath = "Project\\Team A",
                IterationPath = "Sprint 1",
                ParentTfsId = null,
                Effort = 5,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            },
            new WorkItemDto
            {
                TfsId = 456,
                Title = "Item 2",
                Type = "Feature",
                State = "Active",
                AreaPath = "Project\\Team A",
                IterationPath = "Sprint 1",
                ParentTfsId = 123,
                Effort = 3,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            },
            new WorkItemDto
            {
                TfsId = 789,
                Title = "Item 3",
                Type = "Task",
                State = "Done",
                AreaPath = "Project\\Team B",
                IterationPath = "Sprint 2",
                ParentTfsId = 456,
                Effort = 2,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            }
        };

        // Act
        var result = _service.GenerateSummaryReport(workItems);

        // Assert
        Assert.Contains("## Summary by Area Path", result);
        Assert.Contains("**Project\\\\Team A:** 2", result);
        Assert.Contains("**Project\\\\Team B:** 1", result);
    }

    [TestMethod]
    public void GenerateSummaryReport_ContainsMarkdownTable_WithAllItems()
    {
        // Arrange
        var workItems = new[]
        {
            new WorkItemDto
            {
                TfsId = 123,
                Title = "Test Item",
                Type = "Epic",
                State = "Active",
                AreaPath = "Project\\Team",
                IterationPath = "Sprint 1",
                ParentTfsId = null,
                Effort = 5,
                RetrievedAt = DateTimeOffset.UtcNow,
                JsonPayload = "{}"
            }
        };

        // Act
        var result = _service.GenerateSummaryReport(workItems);

        // Assert
        Assert.Contains("| ID | Type | State | Title | Effort |", result);
        Assert.Contains("|---|---|---|---|---|", result);
        Assert.Contains("| 123 | Epic | Active | Test Item | 5h |", result);
    }
}
