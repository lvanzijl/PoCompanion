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
        Assert.AreEqual(0, result.Count, "No validation issues expected for valid hierarchy");
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
        Assert.AreEqual(1, result.Count, "Should have one item with issues");
        Assert.IsTrue(result.ContainsKey(2), "Epic should have validation issues");
        
        var issues = result[2];
        Assert.AreEqual(1, issues.Count, "Should have one error");
        Assert.AreEqual("Error", issues[0].Severity);
        Assert.IsTrue(issues[0].Message.Contains("Parent"), "Message should mention parent");
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
        Assert.AreEqual(2, result.Count, "Both Epic and Feature should have issues");
        
        // Epic has error for parent being New
        Assert.IsTrue(result.ContainsKey(2));
        Assert.AreEqual(1, result[2].Count);
        Assert.AreEqual("Error", result[2][0].Severity);
        
        // Feature has warning for grandparent being New
        Assert.IsTrue(result.ContainsKey(3));
        Assert.AreEqual(1, result[3].Count);
        Assert.AreEqual("Warning", result[3][0].Severity);
        Assert.IsTrue(result[3][0].Message.Contains("Ancestor"), "Message should mention ancestor");
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
        Assert.AreEqual(1, result.Count, "Only Feature should have issues");
        Assert.IsTrue(result.ContainsKey(3));
        
        var issues = result[3];
        Assert.AreEqual(2, issues.Count, "Should have error and warning");
        
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
        Assert.AreEqual(1, result.Count, "Should detect issue with case-sensitive match");
        Assert.IsTrue(result.ContainsKey(2));
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
