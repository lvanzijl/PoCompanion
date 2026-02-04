using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class BugInsightsCalculatorTests
{
    private BugInsightsCalculator _calculator = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _calculator = new BugInsightsCalculator();
    }

    private WorkItemDto CreateBug(
        int tfsId,
        string state,
        DateTimeOffset? createdDate = null,
        DateTimeOffset? closedDate = null,
        string? severity = null)
    {
        var created = createdDate ?? DateTimeOffset.UtcNow.AddDays(-30);
        var jsonPayload = severity != null
            ? $"{{\"Microsoft.VSTS.Common.Severity\":\"{severity}\"}}"
            : "{}";

        return new WorkItemDto
        {
            TfsId = tfsId,
            Type = "Bug",
            Title = $"Bug {tfsId}",
            ParentTfsId = null,
            AreaPath = "TestArea",
            IterationPath = "TestIteration",
            State = state,
            JsonPayload = jsonPayload,
            RetrievedAt = DateTimeOffset.UtcNow,
            Effort = null,
            Description = null,
            CreatedDate = created,
            ClosedDate = closedDate,
            Tags = null
        };
    }

    #region Total Open Bugs Tests

    [TestMethod]
    public void CalculateTotalOpenBugs_EmptyList_ReturnsZero()
    {
        // Arrange
        var bugs = new List<WorkItemDto>();

        // Act
        var result = _calculator.CalculateTotalOpenBugs(bugs);

        // Assert
        Assert.AreEqual(0.0, result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateTotalOpenBugs_AllOpen_ReturnsCorrectCount()
    {
        // Arrange
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "New"),
            CreateBug(2, "Active"),
            CreateBug(3, "In Progress")
        };

        // Act
        var result = _calculator.CalculateTotalOpenBugs(bugs);

        // Assert
        Assert.AreEqual(3.0, result.Median);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void CalculateTotalOpenBugs_MixedStates_ReturnsOnlyOpen()
    {
        // Arrange
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "New"),
            CreateBug(2, "Active"),
            CreateBug(3, "Closed"),
            CreateBug(4, "Done"),
            CreateBug(5, "In Progress")
        };

        // Act
        var result = _calculator.CalculateTotalOpenBugs(bugs);

        // Assert
        Assert.AreEqual(3.0, result.Median); // Only New, Active, In Progress
        Assert.AreEqual(5, result.Count); // Total bugs analyzed
    }

    [TestMethod]
    public void CalculateTotalOpenBugs_AllClosed_ReturnsZero()
    {
        // Arrange
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "Closed"),
            CreateBug(2, "Done"),
            CreateBug(3, "Removed")
        };

        // Act
        var result = _calculator.CalculateTotalOpenBugs(bugs);

        // Assert
        Assert.AreEqual(0.0, result.Median);
        Assert.AreEqual(3, result.Count);
    }

    #endregion

    #region Bugs Created Per Period Tests

    [TestMethod]
    public void CalculateBugsCreatedPerPeriod_EmptyList_ReturnsZero()
    {
        // Arrange
        var bugs = new List<WorkItemDto>();
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);

        // Act
        var result = _calculator.CalculateBugsCreatedPerPeriod(bugs, fromDate);

        // Assert
        Assert.AreEqual(0.0, result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateBugsCreatedPerPeriod_BugsWithinWindow_ReturnsCorrectCount()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "New", createdDate: DateTimeOffset.UtcNow.AddMonths(-5)),
            CreateBug(2, "Active", createdDate: DateTimeOffset.UtcNow.AddMonths(-4)),
            CreateBug(3, "Closed", createdDate: DateTimeOffset.UtcNow.AddMonths(-3))
        };

        // Act
        var result = _calculator.CalculateBugsCreatedPerPeriod(bugs, fromDate);

        // Assert
        Assert.AreEqual(3.0, result.Median);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void CalculateBugsCreatedPerPeriod_BugsOutsideWindow_ExcludesThem()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "New", createdDate: DateTimeOffset.UtcNow.AddMonths(-5)),
            CreateBug(2, "Active", createdDate: DateTimeOffset.UtcNow.AddMonths(-7)), // Outside window
            CreateBug(3, "Closed", createdDate: DateTimeOffset.UtcNow.AddMonths(-3))
        };

        // Act
        var result = _calculator.CalculateBugsCreatedPerPeriod(bugs, fromDate);

        // Assert
        Assert.AreEqual(2.0, result.Median); // Only bugs 1 and 3
        Assert.AreEqual(3, result.Count); // Total bugs analyzed
    }

    [TestMethod]
    public void CalculateBugsCreatedPerPeriod_CustomEndDate_RespectsIt()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        var toDate = DateTimeOffset.UtcNow.AddMonths(-2);
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "New", createdDate: DateTimeOffset.UtcNow.AddMonths(-5)),
            CreateBug(2, "Active", createdDate: DateTimeOffset.UtcNow.AddMonths(-1)), // After end date
            CreateBug(3, "Closed", createdDate: DateTimeOffset.UtcNow.AddMonths(-3))
        };

        // Act
        var result = _calculator.CalculateBugsCreatedPerPeriod(bugs, fromDate, toDate);

        // Assert
        Assert.AreEqual(2.0, result.Median); // Only bugs 1 and 3
        Assert.AreEqual(3, result.Count);
    }

    #endregion

    #region Bugs Resolved Per Period Tests

    [TestMethod]
    public void CalculateBugsResolvedPerPeriod_EmptyList_ReturnsZero()
    {
        // Arrange
        var bugs = new List<WorkItemDto>();
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);

        // Act
        var result = _calculator.CalculateBugsResolvedPerPeriod(bugs, fromDate);

        // Assert
        Assert.AreEqual(0.0, result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateBugsResolvedPerPeriod_BugsResolvedWithinWindow_ReturnsCorrectCount()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "Closed", 
                createdDate: DateTimeOffset.UtcNow.AddMonths(-7),
                closedDate: DateTimeOffset.UtcNow.AddMonths(-5)),
            CreateBug(2, "Done", 
                createdDate: DateTimeOffset.UtcNow.AddMonths(-6),
                closedDate: DateTimeOffset.UtcNow.AddMonths(-4)),
            CreateBug(3, "Active", createdDate: DateTimeOffset.UtcNow.AddMonths(-5)) // Not closed
        };

        // Act
        var result = _calculator.CalculateBugsResolvedPerPeriod(bugs, fromDate);

        // Assert
        Assert.AreEqual(2.0, result.Median); // Bugs 1 and 2
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void CalculateBugsResolvedPerPeriod_BugsResolvedOutsideWindow_ExcludesThem()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "Closed", 
                createdDate: DateTimeOffset.UtcNow.AddMonths(-8),
                closedDate: DateTimeOffset.UtcNow.AddMonths(-7)), // Closed before window
            CreateBug(2, "Done", 
                createdDate: DateTimeOffset.UtcNow.AddMonths(-6),
                closedDate: DateTimeOffset.UtcNow.AddMonths(-4))
        };

        // Act
        var result = _calculator.CalculateBugsResolvedPerPeriod(bugs, fromDate);

        // Assert
        Assert.AreEqual(1.0, result.Median); // Only bug 2
        Assert.AreEqual(2, result.Count);
    }

    #endregion

    #region Bug Resolution Time Tests

    [TestMethod]
    public void CalculateBugResolutionTime_EmptyList_ReturnsNull()
    {
        // Arrange
        var bugs = new List<WorkItemDto>();

        // Act
        var result = _calculator.CalculateBugResolutionTime(bugs);

        // Assert
        Assert.IsNull(result.Median);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateBugResolutionTime_SingleBug_ReturnsCorrectTime()
    {
        // Arrange
        var createdDate = DateTimeOffset.UtcNow.AddDays(-10);
        var closedDate = DateTimeOffset.UtcNow.AddDays(-5);
        var expectedHours = (closedDate - createdDate).TotalHours;

        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "Closed", createdDate: createdDate, closedDate: closedDate)
        };

        // Act
        var result = _calculator.CalculateBugResolutionTime(bugs);

        // Assert
        Assert.AreEqual(expectedHours, result.Median);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(100.0, result.Coverage); // 100% coverage
    }

    [TestMethod]
    public void CalculateBugResolutionTime_MultipleBugs_ReturnsMedianAndP75()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "Closed", 
                createdDate: now.AddHours(-48), 
                closedDate: now.AddHours(-24)), // 24 hours
            CreateBug(2, "Closed", 
                createdDate: now.AddHours(-72), 
                closedDate: now.AddHours(-24)), // 48 hours
            CreateBug(3, "Closed", 
                createdDate: now.AddHours(-96), 
                closedDate: now.AddHours(-24)), // 72 hours
            CreateBug(4, "Closed", 
                createdDate: now.AddHours(-120), 
                closedDate: now.AddHours(-24)) // 96 hours
        };

        // Act
        var result = _calculator.CalculateBugResolutionTime(bugs);

        // Assert
        Assert.AreEqual(60.0, result.Median); // Median of [24, 48, 72, 96] = (48+72)/2 = 60
        Assert.AreEqual(78.0, result.P75); // P75 = 72 + 0.25*(96-72) = 78
        Assert.AreEqual(4, result.Count);
    }

    [TestMethod]
    public void CalculateBugResolutionTime_BugsWithoutClosedDate_ExcludesThem()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "Closed", 
                createdDate: now.AddHours(-48), 
                closedDate: now.AddHours(-24)), // 24 hours - has complete data
            CreateBug(2, "Active", 
                createdDate: now.AddHours(-72)), // No closed date - not closed
            CreateBug(3, "Closed", 
                createdDate: now.AddHours(-96), 
                closedDate: now.AddHours(-24)) // 72 hours - has complete data
        };

        // Act
        var result = _calculator.CalculateBugResolutionTime(bugs);

        // Assert
        Assert.AreEqual(2, result.Count);
        // Coverage: 2 bugs closed in window, both have complete data = 100%
        Assert.AreEqual(100.0, result.Coverage!.Value, 0.01);
    }

    [TestMethod]
    public void CalculateBugResolutionTime_BugsResolvedOutsideWindow_ExcludesThem()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        var now = DateTimeOffset.UtcNow;
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "Closed", 
                createdDate: now.AddMonths(-8), 
                closedDate: now.AddMonths(-7)), // Closed before window
            CreateBug(2, "Closed", 
                createdDate: now.AddMonths(-6), 
                closedDate: now.AddMonths(-4)), // Closed in window
            CreateBug(3, "Closed", 
                createdDate: now.AddMonths(-5), 
                closedDate: now.AddMonths(-2)) // Closed in window
        };

        // Act
        var result = _calculator.CalculateBugResolutionTime(bugs, fromDate);

        // Assert
        Assert.AreEqual(2, result.Count); // Only bugs 2 and 3
        Assert.IsNotNull(result.Median);
        Assert.IsNotNull(result.P75);
    }

    [TestMethod]
    public void CalculateBugResolutionTime_NoResolvedBugsInWindow_ReturnsEmpty()
    {
        // Arrange
        var fromDate = DateTimeOffset.UtcNow.AddMonths(-6);
        var now = DateTimeOffset.UtcNow;
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "Active", createdDate: now.AddMonths(-5)), // Not closed
            CreateBug(2, "Closed", 
                createdDate: now.AddMonths(-8), 
                closedDate: now.AddMonths(-7)) // Closed before window
        };

        // Act
        var result = _calculator.CalculateBugResolutionTime(bugs, fromDate);

        // Assert
        Assert.AreEqual(0, result.Count);
        Assert.IsNull(result.Median);
        Assert.IsNull(result.P75);
        Assert.AreEqual(0.0, result.Coverage);
    }

    #endregion

    #region Bugs By Severity Distribution Tests

    [TestMethod]
    public void CalculateBugsBySeverityDistribution_EmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var bugs = new List<WorkItemDto>();

        // Act
        var result = _calculator.CalculateBugsBySeverityDistribution(bugs);

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void CalculateBugsBySeverityDistribution_SingleSeverity_ReturnsCorrectDistribution()
    {
        // Arrange
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "New", severity: "1 - Critical"),
            CreateBug(2, "Active", severity: "1 - Critical"),
            CreateBug(3, "Closed", severity: "1 - Critical")
        };

        // Act
        var result = _calculator.CalculateBugsBySeverityDistribution(bugs);

        // Assert
        Assert.HasCount(1, result);
        Assert.IsTrue(result.ContainsKey("1 - Critical"));
        Assert.AreEqual(3, result["1 - Critical"].Count);
        Assert.AreEqual(100.0, result["1 - Critical"].Percentage);
    }

    [TestMethod]
    public void CalculateBugsBySeverityDistribution_MultipleSeverities_ReturnsCorrectDistribution()
    {
        // Arrange
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "New", severity: "1 - Critical"),
            CreateBug(2, "Active", severity: "2 - High"),
            CreateBug(3, "Closed", severity: "2 - High"),
            CreateBug(4, "Active", severity: "3 - Medium"),
            CreateBug(5, "New", severity: "4 - Low")
        };

        // Act
        var result = _calculator.CalculateBugsBySeverityDistribution(bugs);

        // Assert
        Assert.HasCount(4, result);
        Assert.AreEqual(1, result["1 - Critical"].Count);
        Assert.AreEqual(20.0, result["1 - Critical"].Percentage);
        Assert.AreEqual(2, result["2 - High"].Count);
        Assert.AreEqual(40.0, result["2 - High"].Percentage);
        Assert.AreEqual(1, result["3 - Medium"].Count);
        Assert.AreEqual(20.0, result["3 - Medium"].Percentage);
        Assert.AreEqual(1, result["4 - Low"].Count);
        Assert.AreEqual(20.0, result["4 - Low"].Percentage);
    }

    [TestMethod]
    public void CalculateBugsBySeverityDistribution_MissingSeverity_CategorizedAsUnknown()
    {
        // Arrange
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "New", severity: "1 - Critical"),
            CreateBug(2, "Active", severity: null), // No severity
            CreateBug(3, "Closed") // No severity
        };

        // Act
        var result = _calculator.CalculateBugsBySeverityDistribution(bugs);

        // Assert
        Assert.HasCount(2, result);
        Assert.AreEqual(1, result["1 - Critical"].Count);
        Assert.AreEqual(33.33, result["1 - Critical"].Percentage, 0.01);
        Assert.AreEqual(2, result["Unknown"].Count);
        Assert.AreEqual(66.67, result["Unknown"].Percentage, 0.01);
    }

    [TestMethod]
    public void CalculateBugsBySeverityDistribution_OrdersByCriticalityFirst()
    {
        // Arrange
        var bugs = new List<WorkItemDto>
        {
            CreateBug(1, "New", severity: "4 - Low"),
            CreateBug(2, "Active", severity: "1 - Critical"),
            CreateBug(3, "Closed", severity: "3 - Medium"),
            CreateBug(4, "Active", severity: "2 - High")
        };

        // Act
        var result = _calculator.CalculateBugsBySeverityDistribution(bugs);

        // Assert
        var keys = result.Keys.ToList();
        Assert.AreEqual("1 - Critical", keys[0]);
        Assert.AreEqual("2 - High", keys[1]);
        Assert.AreEqual("3 - Medium", keys[2]);
        Assert.AreEqual("4 - Low", keys[3]);
    }

    #endregion
}
