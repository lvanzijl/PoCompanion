using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using Moq;

namespace PoTool.Tests.Unit;

/// <summary>
/// Tests for the legacy WorkItemInProgressWithoutEffortValidator.
/// This validator is deprecated and replaced by RC-2 hierarchical rule.
/// Tests kept for backwards compatibility.
/// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
[TestClass]
public class WorkItemInProgressWithoutEffortValidatorTests
{
    private WorkItemInProgressWithoutEffortValidator _validator = null!;
    private Mock<IWorkItemStateClassificationService> _mockStateService = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockStateService = new Mock<IWorkItemStateClassificationService>();
        
        // Setup default state classifications (matching typical TFS/Azure DevOps states)
        SetupStateClassification("Feature", "New", StateClassification.New);
        SetupStateClassification("Feature", "In Progress", StateClassification.InProgress);
        SetupStateClassification("Feature", "Done", StateClassification.Done);
        
        _validator = new WorkItemInProgressWithoutEffortValidator(_mockStateService.Object);
    }

    private void SetupStateClassification(string type, string state, StateClassification classification)
    {
        _mockStateService
            .Setup(s => s.GetClassificationAsync(type, state, It.IsAny<CancellationToken>()))
            .ReturnsAsync(classification);
    }

    [TestMethod]
    public void ValidateWorkItems_InProgressWithEffort_NoIssues()
    {
        // Arrange: Item in progress with effort
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "In Progress", 8)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.IsEmpty(result, "No validation issues expected when effort is provided");
    }

    [TestMethod]
    public void ValidateWorkItems_InProgressWithoutEffort_HasError()
    {
        // Arrange: Item in progress without effort
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "In Progress", null)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.HasCount(1, result, "Should have one item with issues");

#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(1), "Item should have validation issues");

        var issues = result[1];
        Assert.HasCount(1, issues, "Should have one error");
        Assert.AreEqual("Error", issues[0].Severity);
        Assert.Contains("effort", issues[0].Message, "Message should mention effort");
        Assert.AreEqual("RC-2", issues[0].RuleId, "RuleId should be RC-2");
    }

    [TestMethod]
    public void ValidateWorkItems_InProgressWithZeroEffort_HasError()
    {
        // Arrange: Item in progress with zero effort
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "In Progress", 0)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.HasCount(1, result, "Should have one item with issues");

#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(1), "Item should have validation issues");
        Assert.AreEqual("RC-2", result[1][0].RuleId, "RuleId should be RC-2");
    }

    [TestMethod]
    public void ValidateWorkItems_NotInProgressWithoutEffort_NoIssues()
    {
        // Arrange: Item not in progress, no effort
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "New", null),
            CreateWorkItem(2, "Feature", "Done", null)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.IsEmpty(result, "No validation issues for items not in progress");
    }

    [TestMethod]
    public void ValidateWorkItems_MultipleItems_CorrectlyIdentifiesIssues()
    {
        // Arrange: Mix of valid and invalid items
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Feature", "In Progress", 8),
            CreateWorkItem(2, "Feature", "In Progress", null),
            CreateWorkItem(3, "Feature", "New", null),
            CreateWorkItem(4, "Feature", "In Progress", 0),
            CreateWorkItem(5, "Feature", "In Progress", 24)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.HasCount(2, result, "Should have two items with issues");

#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(2), "Item 2 should have issues");

#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(4), "Item 4 should have issues");

#pragma warning disable MSTEST0037
        Assert.IsFalse(result.ContainsKey(1), "Item 1 should not have issues");

#pragma warning disable MSTEST0037
        Assert.IsFalse(result.ContainsKey(3), "Item 3 should not have issues");

#pragma warning disable MSTEST0037
        Assert.IsFalse(result.ContainsKey(5), "Item 5 should not have issues");
    }

    private static WorkItemDto CreateWorkItem(int id, string type, string state, int? effort)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Test {type} {id}",
            ParentTfsId: null,
            AreaPath: "Test",
            IterationPath: "Test",
            State: state,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
                    Description: null,
                    Tags: null
        );
    }
}
