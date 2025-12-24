using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Validators;

namespace PoTool.Tests.Unit;

[TestClass]
public class WorkItemParentProgressValidatorTests
{
    private WorkItemParentProgressValidator _validator = null!;

    [TestInitialize]
    public void Setup()
    {
        _validator = new WorkItemParentProgressValidator();
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
        Assert.IsTrue(result.ContainsKey(2), "Epic should have validation issues");
        
        var issues = result[2];
        Assert.HasCount(1, issues, "Should have one error");
        Assert.AreEqual("Error", issues[0].Severity);
        Assert.Contains("Parent", issues[0].Message, "Message should mention parent");
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
        Assert.HasCount(2, result, "Both Epic and Feature should have issues");
        
        // Epic has error for parent being New
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(2));
        Assert.HasCount(1, result[2]);
        Assert.AreEqual("Error", result[2][0].Severity);
        
        // Feature has warning for grandparent being New
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(3));
        Assert.HasCount(1, result[3]);
        Assert.AreEqual("Warning", result[3][0].Severity);
        Assert.Contains("Ancestor", result[3][0].Message, "Message should mention ancestor");
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
        Assert.HasCount(1, result, "Only Feature should have issues");
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(3));
        
        var issues = result[3];
        Assert.HasCount(2, issues, "Should have error and warning");
        
        var error = issues.FirstOrDefault(i => i.Severity == "Error");
        var warning = issues.FirstOrDefault(i => i.Severity == "Warning");
        
        Assert.IsNotNull(error, "Should have an error for immediate parent");
        Assert.IsNotNull(warning, "Should have a warning for ancestor");
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
            CreateWorkItem(1, "Goal", "in progress", null), // lowercase
            CreateWorkItem(2, "Epic", "In Progress", 1)
        };

        // Act
        var result = _validator.ValidateWorkItems(items);

        // Assert
        Assert.HasCount(1, result, "Should detect issue with case-sensitive match");
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(2));
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
        Assert.HasCount(3, result, "All three children should have issues");
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(2));
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(3));
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.ContainsKey(4));
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
            Effort: null
        );
    }
}
