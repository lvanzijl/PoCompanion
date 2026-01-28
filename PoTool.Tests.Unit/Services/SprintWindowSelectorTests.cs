using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Core.Metrics.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class SprintWindowSelectorTests
{
    private SprintWindowSelector _selector = null!;

    [TestInitialize]
    public void Initialize()
    {
        _selector = new SprintWindowSelector();
    }

    #region GetBacklogHealthSprints Tests

    [TestMethod]
    public void GetBacklogHealthSprints_StandardCase_ReturnsCurrentPlusTwoFuture()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Past2", new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 11, 14, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past1", new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Current", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future1", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future2", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future3", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetBacklogHealthSprints(sprints, today);

        // Assert
        Assert.HasCount(3, result, "Should return 3 sprints");
        Assert.AreEqual("Current", result[0].SprintName, "First should be current");
        Assert.AreEqual("Future1", result[1].SprintName, "Second should be first future");
        Assert.AreEqual("Future2", result[2].SprintName, "Third should be second future");
    }

    [TestMethod]
    public void GetBacklogHealthSprints_NoCurrentOnlyFuture_ReturnsEarliestFutureAsCurrentPlusTwoMore()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Past1", new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future1", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future2", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future3", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetBacklogHealthSprints(sprints, today);

        // Assert
        Assert.HasCount(3, result, "Should return 3 sprints");
        Assert.AreEqual("Future1", result[0].SprintName, "Should use earliest future as current");
        Assert.AreEqual("Future2", result[1].SprintName);
        Assert.AreEqual("Future3", result[2].SprintName);
    }

    [TestMethod]
    public void GetBacklogHealthSprints_NoCurrentOnlyPast_ReturnsLatestPastAsCurrentOnly()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Past1", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past2", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past3", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetBacklogHealthSprints(sprints, today);

        // Assert
        Assert.HasCount(1, result, "Should return only 1 sprint (latest past as current)");
        Assert.AreEqual("Past3", result[0].SprintName, "Should use latest past as current");
    }

    [TestMethod]
    public void GetBacklogHealthSprints_InsufficientFuture_ReturnsCurrentPlusAvailableFuture()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Current", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future1", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetBacklogHealthSprints(sprints, today);

        // Assert
        Assert.HasCount(2, result, "Should return 2 sprints (current + 1 future)");
        Assert.AreEqual("Current", result[0].SprintName);
        Assert.AreEqual("Future1", result[1].SprintName);
    }

    [TestMethod]
    public void GetBacklogHealthSprints_NoSprintsWithDates_ReturnsEmpty()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Sprint1", null, null),
            CreateSprint("Sprint2", null, null),
        };

        // Act
        var result = _selector.GetBacklogHealthSprints(sprints, today);

        // Assert
        Assert.HasCount(0, result, "Should return empty list when no sprints have dates");
    }

    #endregion

    #region GetIssueComparisonSprints Tests

    [TestMethod]
    public void GetIssueComparisonSprints_StandardCase_ReturnsThreePastCurrentTwoFuture()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Past4", new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 9, 30, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past3", new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 10, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past2", new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 11, 30, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past1", new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Current", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future1", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future2", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future3", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetIssueComparisonSprints(sprints, today);

        // Assert
        Assert.HasCount(6, result, "Should return 6 sprints");
        Assert.AreEqual("Past3", result[0].SprintName, "First should be 3rd most recent past (oldest of the 3)");
        Assert.AreEqual("Past2", result[1].SprintName, "Second should be 2nd most recent past");
        Assert.AreEqual("Past1", result[2].SprintName, "Third should be most recent past");
        Assert.AreEqual("Current", result[3].SprintName, "Fourth should be current");
        Assert.AreEqual("Future1", result[4].SprintName, "Fifth should be first future");
        Assert.AreEqual("Future2", result[5].SprintName, "Sixth should be second future");
    }

    [TestMethod]
    public void GetIssueComparisonSprints_InsufficientPast_ReturnsAvailablePastCurrentTwoFuture()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Past1", new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Current", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future1", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future2", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetIssueComparisonSprints(sprints, today);

        // Assert
        Assert.HasCount(4, result, "Should return 4 sprints (1 past + current + 2 future)");
        Assert.AreEqual("Past1", result[0].SprintName);
        Assert.AreEqual("Current", result[1].SprintName);
        Assert.AreEqual("Future1", result[2].SprintName);
        Assert.AreEqual("Future2", result[3].SprintName);
    }

    [TestMethod]
    public void GetIssueComparisonSprints_InsufficientFuture_ReturnsThreePastCurrentAvailableFuture()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Past3", new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 10, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past2", new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 11, 30, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past1", new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Current", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future1", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetIssueComparisonSprints(sprints, today);

        // Assert
        Assert.HasCount(5, result, "Should return 5 sprints (3 past + current + 1 future)");
        Assert.AreEqual("Past3", result[0].SprintName);
        Assert.AreEqual("Past2", result[1].SprintName);
        Assert.AreEqual("Past1", result[2].SprintName);
        Assert.AreEqual("Current", result[3].SprintName);
        Assert.AreEqual("Future1", result[4].SprintName);
    }

    [TestMethod]
    public void GetIssueComparisonSprints_NoCurrentOnlyFuture_ReturnsEarliestFutureAsCurrentPlusTwoMore()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Past3", new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 10, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past2", new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 11, 30, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past1", new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future1", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future2", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future3", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetIssueComparisonSprints(sprints, today);

        // Assert
        Assert.HasCount(6, result);
        Assert.AreEqual("Past3", result[0].SprintName);
        Assert.AreEqual("Past2", result[1].SprintName);
        Assert.AreEqual("Past1", result[2].SprintName);
        Assert.AreEqual("Future1", result[3].SprintName, "Should use earliest future as current");
        Assert.AreEqual("Future2", result[4].SprintName);
        Assert.AreEqual("Future3", result[5].SprintName);
    }

    [TestMethod]
    public void GetIssueComparisonSprints_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>();

        // Act
        var result = _selector.GetIssueComparisonSprints(sprints, today);

        // Assert
        Assert.HasCount(0, result, "Should return empty list");
    }

    #endregion

    private static SprintMetricsDto CreateSprint(string name, DateTimeOffset? start, DateTimeOffset? end)
    {
        return new SprintMetricsDto(
            IterationPath: $"\\Project\\{name}",
            SprintName: name,
            StartDate: start,
            EndDate: end,
            CompletedStoryPoints: 0,
            PlannedStoryPoints: 0,
            CompletedWorkItemCount: 0,
            TotalWorkItemCount: 0,
            CompletedPBIs: 0,
            CompletedBugs: 0,
            CompletedTasks: 0
        );
    }
}
