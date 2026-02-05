using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Services;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Tests for SprintTrendProjectionService activity detection logic.
/// Validates the IsQualifyingActivity method which determines if a state
/// change counts as "work" for sprint trend metrics.
/// </summary>
[TestClass]
public class SprintTrendActivityDetectionTests
{
    private SprintTrendProjectionService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        // Create minimal service for testing the activity detection logic
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockLogger = new Mock<ILogger<SprintTrendProjectionService>>();
        
        _service = new SprintTrendProjectionService(
            mockScopeFactory.Object,
            mockLogger.Object);
    }

    #region Task Activity Tests

    [TestMethod]
    [Description("Task: Any state change should count as activity")]
    public void IsQualifyingActivity_Task_NewToInProgress_ReturnsTrue()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Task",
            StateClassification.New,
            StateClassification.InProgress);

        // Assert
        Assert.IsTrue(result, "Task New→InProgress should count as activity");
    }

    [TestMethod]
    [Description("Task: InProgress to Done should count as activity")]
    public void IsQualifyingActivity_Task_InProgressToDone_ReturnsTrue()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Task",
            StateClassification.InProgress,
            StateClassification.Done);

        // Assert
        Assert.IsTrue(result, "Task InProgress→Done should count as activity");
    }

    [TestMethod]
    [Description("Task: Done to Removed should count as activity")]
    public void IsQualifyingActivity_Task_DoneToRemoved_ReturnsTrue()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Task",
            StateClassification.Done,
            StateClassification.Removed);

        // Assert
        Assert.IsTrue(result, "Task Done→Removed should count as activity");
    }

    [TestMethod]
    [Description("Task: Same state should not count as activity")]
    public void IsQualifyingActivity_Task_SameState_ReturnsFalse()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Task",
            StateClassification.InProgress,
            StateClassification.InProgress);

        // Assert
        Assert.IsFalse(result, "Task same state should not count as activity");
    }

    #endregion

    #region PBI Activity Tests

    [TestMethod]
    [Description("PBI: New→InProgress (commit) should NOT count as activity per spec")]
    public void IsQualifyingActivity_Pbi_NewToInProgress_ReturnsFalse()
    {
        // Arrange & Act - "Product Backlog Item" full name
        var result = _service.IsQualifyingActivity(
            "Product Backlog Item",
            StateClassification.New,
            StateClassification.InProgress);

        // Assert
        Assert.IsFalse(result, "PBI New→InProgress (commit) should NOT count as activity");
    }

    [TestMethod]
    [Description("PBI (short name): New→InProgress should NOT count as activity")]
    public void IsQualifyingActivity_PbiShort_NewToInProgress_ReturnsFalse()
    {
        // Arrange & Act - "PBI" short name
        var result = _service.IsQualifyingActivity(
            "PBI",
            StateClassification.New,
            StateClassification.InProgress);

        // Assert
        Assert.IsFalse(result, "PBI (short) New→InProgress should NOT count as activity");
    }

    [TestMethod]
    [Description("PBI: InProgress→Done should count as activity")]
    public void IsQualifyingActivity_Pbi_InProgressToDone_ReturnsTrue()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Product Backlog Item",
            StateClassification.InProgress,
            StateClassification.Done);

        // Assert
        Assert.IsTrue(result, "PBI InProgress→Done should count as activity");
    }

    [TestMethod]
    [Description("PBI: New→Done should NOT count as activity (skipped InProgress)")]
    public void IsQualifyingActivity_Pbi_NewToDone_ReturnsFalse()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Product Backlog Item",
            StateClassification.New,
            StateClassification.Done);

        // Assert - Direct New→Done doesn't match either qualifying transition
        Assert.IsFalse(result, "PBI New→Done should NOT count as activity");
    }

    [TestMethod]
    [Description("PBI: Done→InProgress (reopened) should NOT count as PBI activity")]
    public void IsQualifyingActivity_Pbi_DoneToInProgress_ReturnsFalse()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Product Backlog Item",
            StateClassification.Done,
            StateClassification.InProgress);

        // Assert - PBI reopening is not considered activity per spec
        Assert.IsFalse(result, "PBI Done→InProgress should NOT count as activity");
    }

    #endregion

    #region Bug Activity Tests

    [TestMethod]
    [Description("Bug: InProgress→Done should count as activity")]
    public void IsQualifyingActivity_Bug_InProgressToDone_ReturnsTrue()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Bug",
            StateClassification.InProgress,
            StateClassification.Done);

        // Assert
        Assert.IsTrue(result, "Bug InProgress→Done should count as activity");
    }

    [TestMethod]
    [Description("Bug: Done→InProgress (reopened) should count as activity")]
    public void IsQualifyingActivity_Bug_DoneToInProgress_ReturnsTrue()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Bug",
            StateClassification.Done,
            StateClassification.InProgress);

        // Assert
        Assert.IsTrue(result, "Bug Done→InProgress (reopened) should count as activity");
    }

    [TestMethod]
    [Description("Bug: New→InProgress should NOT count as activity")]
    public void IsQualifyingActivity_Bug_NewToInProgress_ReturnsFalse()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Bug",
            StateClassification.New,
            StateClassification.InProgress);

        // Assert
        Assert.IsFalse(result, "Bug New→InProgress should NOT count as activity");
    }

    [TestMethod]
    [Description("Bug: New→Done should NOT count as activity (skipped InProgress)")]
    public void IsQualifyingActivity_Bug_NewToDone_ReturnsFalse()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Bug",
            StateClassification.New,
            StateClassification.Done);

        // Assert
        Assert.IsFalse(result, "Bug New→Done should NOT count as activity");
    }

    #endregion

    #region Edge Case Tests

    [TestMethod]
    [Description("Feature: No state changes should count as activity")]
    public void IsQualifyingActivity_Feature_AnyChange_ReturnsFalse()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Feature",
            StateClassification.New,
            StateClassification.Done);

        // Assert
        Assert.IsFalse(result, "Feature state changes should NOT count as activity");
    }

    [TestMethod]
    [Description("Epic: No state changes should count as activity")]
    public void IsQualifyingActivity_Epic_AnyChange_ReturnsFalse()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Epic",
            StateClassification.InProgress,
            StateClassification.Done);

        // Assert
        Assert.IsFalse(result, "Epic state changes should NOT count as activity");
    }

    [TestMethod]
    [Description("Null old classification should not count as activity")]
    public void IsQualifyingActivity_NullOldClass_ReturnsFalse()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Task",
            null,
            StateClassification.InProgress);

        // Assert
        Assert.IsFalse(result, "Null old classification should not count as activity");
    }

    [TestMethod]
    [Description("Null new classification should not count as activity")]
    public void IsQualifyingActivity_NullNewClass_ReturnsFalse()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Task",
            StateClassification.New,
            null);

        // Assert
        Assert.IsFalse(result, "Null new classification should not count as activity");
    }

    [TestMethod]
    [Description("Unknown work item type should not count as activity")]
    public void IsQualifyingActivity_UnknownType_ReturnsFalse()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "Unknown Type",
            StateClassification.New,
            StateClassification.Done);

        // Assert
        Assert.IsFalse(result, "Unknown work item type should not count as activity");
    }

    [TestMethod]
    [Description("Work item type comparison should be case-insensitive")]
    public void IsQualifyingActivity_TaskLowerCase_WorksCorrectly()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "task",
            StateClassification.New,
            StateClassification.InProgress);

        // Assert
        Assert.IsTrue(result, "Work item type comparison should be case-insensitive");
    }

    [TestMethod]
    [Description("Bug type comparison should be case-insensitive")]
    public void IsQualifyingActivity_BugMixedCase_WorksCorrectly()
    {
        // Arrange & Act
        var result = _service.IsQualifyingActivity(
            "BUG",
            StateClassification.InProgress,
            StateClassification.Done);

        // Assert
        Assert.IsTrue(result, "Bug type comparison should be case-insensitive");
    }

    #endregion
}
