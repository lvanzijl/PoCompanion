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
        Assert.IsTrue(result.Contains("# Work Items Summary Report"));
        Assert.IsTrue(result.Contains("**Total Items:** 1"));
        Assert.IsTrue(result.Contains("## Summary by Type"));
        Assert.IsTrue(result.Contains("**Epic:** 1"));
        Assert.IsTrue(result.Contains("## Summary by State"));
        Assert.IsTrue(result.Contains("**Active:** 1"));
        Assert.IsTrue(result.Contains("## Effort Summary"));
        Assert.IsTrue(result.Contains("**Total Effort:** 5"));
        Assert.IsTrue(result.Contains("## Detailed List"));
        Assert.IsTrue(result.Contains("123"));
        Assert.IsTrue(result.Contains("Test Item"));
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
        Assert.IsTrue(result.Contains("**Total Items:** 3"));
        Assert.IsTrue(result.Contains("**Epic:** 1"));
        Assert.IsTrue(result.Contains("**Feature:** 1"));
        Assert.IsTrue(result.Contains("**Task:** 1"));
        Assert.IsTrue(result.Contains("**Active:** 2"));
        Assert.IsTrue(result.Contains("**Done:** 1"));
        Assert.IsTrue(result.Contains("**Total Effort:** 10"));
        Assert.IsTrue(result.Contains("**Average Effort:**"));
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
        Assert.IsTrue(result.Contains("**Items with Effort:** 1 of 2"));
        Assert.IsTrue(result.Contains("**Total Effort:** 5"));
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
        Assert.IsTrue(result.Contains("## Summary by Area Path"));
        Assert.IsTrue(result.Contains("**Project\\\\Team A:** 2"));
        Assert.IsTrue(result.Contains("**Project\\\\Team B:** 1"));
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
        Assert.IsTrue(result.Contains("| ID | Type | State | Title | Effort |"));
        Assert.IsTrue(result.Contains("|---|---|---|---|---|"));
        Assert.IsTrue(result.Contains("| 123 | Epic | Active | Test Item | 5h |"));
    }
}
