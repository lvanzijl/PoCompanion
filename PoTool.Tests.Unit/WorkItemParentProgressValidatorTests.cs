using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Validators;
using PoTool.Core.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using Moq;

namespace PoTool.Tests.Unit;

[TestClass]
public class WorkItemParentProgressValidatorTests
{
    private WorkItemParentProgressValidator _validator = null!;
    private Mock<IWorkItemStateClassificationService> _mockStateService = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockStateService = new Mock<IWorkItemStateClassificationService>();
        
        // Setup default state classifications (matching typical TFS/Azure DevOps states)
        SetupStateClassification("Goal", "New", StateClassification.New);
        SetupStateClassification("Goal", "In Progress", StateClassification.InProgress);
        SetupStateClassification("Goal", "in progress", StateClassification.New); // lowercase not mapped
        SetupStateClassification("Goal", "Done", StateClassification.Done);
        
        SetupStateClassification("Epic", "New", StateClassification.New);
        SetupStateClassification("Epic", "In Progress", StateClassification.InProgress);
        SetupStateClassification("Epic", "Done", StateClassification.Done);
        SetupStateClassification("Epic 1", "In Progress", StateClassification.InProgress);
        SetupStateClassification("Epic 2", "In Progress", StateClassification.InProgress);
        SetupStateClassification("Epic 3", "In Progress", StateClassification.InProgress);
        
        SetupStateClassification("Feature", "New", StateClassification.New);
        SetupStateClassification("Feature", "In Progress", StateClassification.InProgress);
        SetupStateClassification("Feature", "Done", StateClassification.Done);
        
        SetupStateClassification("Story", "In Progress", StateClassification.InProgress);
        SetupStateClassification("Task", "In Progress", StateClassification.InProgress);
        
        _validator = new WorkItemParentProgressValidator(_mockStateService.Object);
    }

    private void SetupStateClassification(string type, string state, StateClassification classification)
    {
        _mockStateService
            .Setup(s => s.GetClassificationAsync(type, state, It.IsAny<CancellationToken>()))
            .ReturnsAsync(classification);
    }

    [TestMethod]
    public void ValidateWorkItems_ValidHierarchy_NoIssues()
    {
        // Arrange: All items in progress
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "In Progress", null),
            CreateWorkItem(2, "Epic", "In Progress", 1),
            CreateWorkItem(3, "Feature", "In Progress", 2)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.IsEmpty(result, "No validation issues expected for valid hierarchy");
    }

    [TestMethod]
    public void ValidateWorkItems_ChildInProgressParentNew_HasError()
    {
        // Arrange: Child in progress, parent not
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null),
            CreateWorkItem(2, "Epic", "In Progress", 1)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.HasCount(1, result, "Should have one item with issues");

#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(1), "Goal should have validation issues (it's the parent not in progress)");

        var issues = result[1];
        Assert.HasCount(1, issues, "Should have one error");
        Assert.AreEqual("Error", issues[0].Severity);
        Assert.Contains("children in progress", issues[0].Message.ToLower(), "Message should mention children in progress");
        Assert.AreEqual("RR-3", issues[0].RuleId, "RuleId should be RR-3");
    }

    [TestMethod]
    public void ValidateWorkItems_GrandchildInProgressGrandparentNew_HasWarning()
    {
        // Arrange: Grandchild in progress, immediate parent in progress, grandparent not
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null),
            CreateWorkItem(2, "Epic", "In Progress", 1),
            CreateWorkItem(3, "Feature", "In Progress", 2)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.HasCount(1, result, "Goal should have issues (it's not in progress but has descendants that are)");

        // Goal has error for direct child Epic being In Progress
        // and warning for grandchild Feature being In Progress

#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(1));
        Assert.HasCount(2, result[1]); // One error for child, one warning for descendant
        
        var error = result[1].FirstOrDefault(i => i.Severity == "Error");
        var warning = result[1].FirstOrDefault(i => i.Severity == "Warning");
        
        Assert.IsNotNull(error, "Should have error for direct child");
        Assert.AreEqual("RR-3", error.RuleId, "Error should have RuleId RR-3");
        Assert.Contains("children in progress", error.Message.ToLower());
        
        Assert.IsNotNull(warning, "Should have warning for descendants");
        Assert.AreEqual("RR-3", warning.RuleId, "Warning should have RuleId RR-3");
        Assert.Contains("descendants in progress", warning.Message.ToLower());
    }

    [TestMethod]
    public void ValidateWorkItems_ChildInProgressParentAndGrandparentNew_HasErrorAndWarning()
    {
        // Arrange: Child in progress, parent and grandparent both not in progress
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "Done", null),
            CreateWorkItem(2, "Epic", "New", 1),
            CreateWorkItem(3, "Feature", "In Progress", 2)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        // Both Epic (id=2) and Goal (id=1) should have issues
        // Epic has direct child in progress, Goal has descendant in progress
        Assert.HasCount(2, result, "Epic and Goal should have issues");

#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(2), "Epic should have error for child in progress");
        Assert.IsTrue(result.ContainsKey(1), "Goal should have warning for descendant in progress");

        var epicIssues = result[2];
        Assert.HasCount(1, epicIssues, "Epic should have one error");
        Assert.AreEqual("Error", epicIssues[0].Severity);
        Assert.AreEqual("RR-3", epicIssues[0].RuleId, "Error should have RuleId RR-3");
        
        var goalIssues = result[1];
        Assert.HasCount(1, goalIssues, "Goal should have one warning");
        Assert.AreEqual("Warning", goalIssues[0].Severity);
        Assert.AreEqual("RR-3", goalIssues[0].RuleId, "Warning should have RuleId RR-3");
    }

    [TestMethod]
    public void ValidateWorkItems_ItemNotInProgress_NoIssues()
    {
        // Arrange: Items not in progress should not be validated
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null),
            CreateWorkItem(2, "Epic", "Done", 1),
            CreateWorkItem(3, "Feature", "New", 2)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.IsEmpty(result, "No validation issues for items not in progress");
    }

    [TestMethod]
    public void ValidateWorkItems_RootItemInProgress_NoIssues()
    {
        // Arrange: Root item (no parent) in progress
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "In Progress", null)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.IsEmpty(result, "Root item in progress should have no issues");
    }

    [TestMethod]
    public void ValidateWorkItems_MissingParent_NoIssues()
    {
        // Arrange: Item references parent that doesn't exist in dataset
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(3, "Feature", "In Progress", 999)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.IsEmpty(result, "Missing parent should not cause validation issues");
    }

    [TestMethod]
    public void ValidateWorkItems_CaseSensitiveState_DetectsIssue()
    {
        // Arrange: Parent has different case for state
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "in progress", null), // lowercase - not classified as InProgress
            CreateWorkItem(2, "Epic", "In Progress", 1)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.HasCount(1, result, "Should detect issue with case-sensitive match");

#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(1), "Goal should have the violation (not in proper InProgress state but has child that is)");
    }

    [TestMethod]
    public void ValidateWorkItems_EmptyList_NoIssues()
    {
        // Arrange: Empty list
        var items = new List<WorkItemDto>();

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.IsEmpty(result, "Empty list should have no validation issues");
    }

    [TestMethod]
    public void ValidateWorkItems_NullParentIdWithZero_NoIssues()
    {
        // Arrange: Item with null parent ID and another with parent ID 0
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "In Progress", null),
            CreateWorkItem(2, "Epic", "In Progress", 0) // Parent ID 0 (non-existent)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.IsEmpty(result, "Item with non-existent parent should have no issues");
    }

    [Ignore]
    [TestMethod]
    public void ValidateWorkItems_CircularReference_HandledGracefully()
    {
        // Arrange: Circular reference (should not crash)
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "In Progress", 2),
            CreateWorkItem(2, "Epic", "In Progress", 1)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert - Should not throw, result may vary
        Assert.IsNotNull(result, "Should handle circular reference gracefully");
    }

    [TestMethod]
    public void ValidateWorkItems_DeepHierarchy_ValidatesCorrectly()
    {
        // Arrange: Deep hierarchy with 5 levels
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "In Progress", null),
            CreateWorkItem(2, "Epic", "In Progress", 1),
            CreateWorkItem(3, "Feature", "In Progress", 2),
            CreateWorkItem(4, "Story", "In Progress", 3),
            CreateWorkItem(5, "Task", "In Progress", 4)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.IsEmpty(result, "Deep valid hierarchy should have no issues");
    }

    [TestMethod]
    public void ValidateWorkItems_MultipleChildren_SameParent()
    {
        // Arrange: Multiple children with same parent
        var items = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Goal", "New", null),
            CreateWorkItem(2, "Epic 1", "In Progress", 1),
            CreateWorkItem(3, "Epic 2", "In Progress", 1),
            CreateWorkItem(4, "Epic 3", "In Progress", 1)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.HasCount(1, result, "Goal should have issues (parent not in progress with multiple children in progress)");

#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(1), "Goal should have the violation");
        
        var issues = result[1];
        // Should have one error (for direct children) and potentially one warning (for descendants if any)
        Assert.IsGreaterThanOrEqualTo(1, issues.Count, "Should have at least one issue");
        Assert.Contains("children in progress", issues[0].Message.ToLower(), "Message should mention children in progress");
    }

    private static WorkItemDto CreateWorkItem(int id, string type, string state, int? parentId)
    {
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: $"Test {type} {id}",
            ParentTfsId: parentId,
            AreaPath: "Test",
            IterationPath: "Test",
            State: state,
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
                    Description: null
        );
    }
}
