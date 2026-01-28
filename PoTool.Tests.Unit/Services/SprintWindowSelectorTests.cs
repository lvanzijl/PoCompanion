using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Core.Metrics.Models;
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

    #region Placeholder Tests (GetBacklogHealthWindow)

    [TestMethod]
    public void GetBacklogHealthWindow_StandardCase_ReturnsExactly3Slots()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Current", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future1", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future2", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetBacklogHealthWindow(sprints, today);

        // Assert
        Assert.HasCount(3, result, "Should return exactly 3 slots");
        Assert.IsFalse(result[0].IsPlaceholder, "First should be real sprint");
        Assert.IsFalse(result[1].IsPlaceholder, "Second should be real sprint");
        Assert.IsFalse(result[2].IsPlaceholder, "Third should be real sprint");
        Assert.AreEqual("Current", result[0].DisplayName);
        Assert.AreEqual("Future1", result[1].DisplayName);
        Assert.AreEqual("Future2", result[2].DisplayName);
    }

    [TestMethod]
    public void GetBacklogHealthWindow_Missing1Future_ReturnsPlaceholder()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Current", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Future1", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetBacklogHealthWindow(sprints, today);

        // Assert
        Assert.HasCount(3, result, "Should return exactly 3 slots");
        Assert.IsFalse(result[0].IsPlaceholder, "First should be real sprint");
        Assert.IsFalse(result[1].IsPlaceholder, "Second should be real sprint");
        Assert.IsTrue(result[2].IsPlaceholder, "Third should be placeholder");
        Assert.AreEqual("[undefined]", result[2].DisplayName);
        Assert.AreEqual("newer sprints aren't available", result[2].PlaceholderMessage);
    }

    [TestMethod]
    public void GetBacklogHealthWindow_Missing2Future_ReturnsPlaceholders()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Current", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetBacklogHealthWindow(sprints, today);

        // Assert
        Assert.HasCount(3, result, "Should return exactly 3 slots");
        Assert.IsFalse(result[0].IsPlaceholder, "First should be real sprint");
        Assert.IsTrue(result[1].IsPlaceholder, "Second should be placeholder");
        Assert.IsTrue(result[2].IsPlaceholder, "Third should be placeholder");
        Assert.AreEqual("[undefined]", result[1].DisplayName);
        Assert.AreEqual("[undefined]", result[2].DisplayName);
    }

    [TestMethod]
    public void GetBacklogHealthWindow_NoSprints_Returns3Placeholders()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>();

        // Act
        var result = _selector.GetBacklogHealthWindow(sprints, today);

        // Assert
        Assert.HasCount(3, result, "Should return exactly 3 slots");
        Assert.IsTrue(result[0].IsPlaceholder, "All should be placeholders");
        Assert.IsTrue(result[1].IsPlaceholder, "All should be placeholders");
        Assert.IsTrue(result[2].IsPlaceholder, "All should be placeholders");
    }

    #endregion

    #region Placeholder Tests (GetIssueComparisonWindow)

    [TestMethod]
    public void GetIssueComparisonWindow_StandardCase_ReturnsExactly6Slots()
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
            CreateSprint("Future2", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetIssueComparisonWindow(sprints, today);

        // Assert
        Assert.HasCount(6, result, "Should return exactly 6 slots");
        for (int i = 0; i < 6; i++)
        {
            Assert.IsFalse(result[i].IsPlaceholder, $"Slot {i} should be real sprint");
        }
        Assert.AreEqual("Past3", result[0].DisplayName);
        Assert.AreEqual("Past2", result[1].DisplayName);
        Assert.AreEqual("Past1", result[2].DisplayName);
        Assert.AreEqual("Current", result[3].DisplayName);
        Assert.AreEqual("Future1", result[4].DisplayName);
        Assert.AreEqual("Future2", result[5].DisplayName);
    }

    [TestMethod]
    public void GetIssueComparisonWindow_Missing1Future_ReturnsPlaceholder()
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
        var result = _selector.GetIssueComparisonWindow(sprints, today);

        // Assert
        Assert.HasCount(6, result, "Should return exactly 6 slots");
        Assert.IsFalse(result[4].IsPlaceholder, "5th should be real sprint");
        Assert.IsTrue(result[5].IsPlaceholder, "6th should be placeholder");
        Assert.AreEqual("[undefined]", result[5].DisplayName);
        Assert.AreEqual("newer sprints aren't available", result[5].PlaceholderMessage);
    }

    [TestMethod]
    public void GetIssueComparisonWindow_Missing2Future_ReturnsPlaceholders()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var sprints = new List<SprintMetricsDto>
        {
            CreateSprint("Past3", new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 10, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past2", new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 11, 30, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Past1", new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)),
            CreateSprint("Current", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
        };

        // Act
        var result = _selector.GetIssueComparisonWindow(sprints, today);

        // Assert
        Assert.HasCount(6, result, "Should return exactly 6 slots");
        Assert.IsFalse(result[3].IsPlaceholder, "4th should be real sprint (current)");
        Assert.IsTrue(result[4].IsPlaceholder, "5th should be placeholder");
        Assert.IsTrue(result[5].IsPlaceholder, "6th should be placeholder");
        Assert.AreEqual("[undefined]", result[4].DisplayName);
        Assert.AreEqual("[undefined]", result[5].DisplayName);
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
