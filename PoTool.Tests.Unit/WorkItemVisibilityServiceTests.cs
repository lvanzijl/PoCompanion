using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Client.ApiClient;
using PoTool.Client.Models;
using PoTool.Client.Services;
using PoTool.Shared.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PoTool.Tests.Unit;

[TestClass]
public class WorkItemVisibilityServiceTests
{
    private Mock<ISettingsClient> _mockSettingsClient = null!;
    private StateClassificationService _stateClassificationService = null!;
    private WorkItemVisibilityService _visibilityService = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockSettingsClient = new Mock<ISettingsClient>();
        
        // Setup default classifications for common states
        var classifications = new List<WorkItemStateClassificationDto>
        {
            new WorkItemStateClassificationDto 
            { 
                WorkItemType = "Epic", 
                StateName = "Closed", 
                Classification = StateClassification.Done 
            },
            new WorkItemStateClassificationDto 
            { 
                WorkItemType = "Epic", 
                StateName = "Active", 
                Classification = StateClassification.InProgress 
            },
            new WorkItemStateClassificationDto 
            { 
                WorkItemType = "Feature", 
                StateName = "Closed", 
                Classification = StateClassification.Done 
            },
            new WorkItemStateClassificationDto 
            { 
                WorkItemType = "Feature", 
                StateName = "Active", 
                Classification = StateClassification.InProgress 
            },
            new WorkItemStateClassificationDto 
            { 
                WorkItemType = "PBI", 
                StateName = "Done", 
                Classification = StateClassification.Done 
            },
            new WorkItemStateClassificationDto 
            { 
                WorkItemType = "PBI", 
                StateName = "Active", 
                Classification = StateClassification.InProgress 
            }
        };

        var response = new GetStateClassificationsResponse
        {
            ProjectName = "TestProject",
            Classifications = classifications,
            IsDefault = false
        };

        _mockSettingsClient
            .Setup(s => s.GetStateClassificationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        
        _stateClassificationService = new StateClassificationService(_mockSettingsClient.Object);
        _visibilityService = new WorkItemVisibilityService(_stateClassificationService);
    }

    [TestMethod]
    public async Task ShouldHideNode_RootItem_ReturnsFalse()
    {
        // Arrange
        var rootNode = new TreeNode
        {
            Id = 1,
            Title = "Root Item",
            Type = "Epic",
            State = "Closed",
            ParentId = null
        };
        var allNodes = new Dictionary<int, TreeNode> { { 1, rootNode } };

        // Act
        var result = await _visibilityService.ShouldHideNodeAsync(rootNode, allNodes);

        // Assert
        Assert.IsFalse(result, "Root items should never be hidden");
    }

    [TestMethod]
    public async Task ShouldHideNode_ItemNotDone_ReturnsFalse()
    {
        // Arrange
        var parentNode = new TreeNode
        {
            Id = 1,
            Title = "Parent",
            Type = "Epic",
            State = "Closed",
            ParentId = null,
            Children = new List<TreeNode>()
        };
        var childNode = new TreeNode
        {
            Id = 2,
            Title = "Child",
            Type = "Feature",
            State = "Active", // Not done
            ParentId = 1
        };
        parentNode.Children.Add(childNode);
        var allNodes = new Dictionary<int, TreeNode> { { 1, parentNode }, { 2, childNode } };

        // Act
        var result = await _visibilityService.ShouldHideNodeAsync(childNode, allNodes);

        // Assert
        Assert.IsFalse(result, "Items not in Done state should not be hidden");
    }

    [TestMethod]
    public async Task ShouldHideNode_ParentNotDone_ReturnsFalse()
    {
        // Arrange
        var parentNode = new TreeNode
        {
            Id = 1,
            Title = "Parent",
            Type = "Epic",
            State = "Active", // Not done
            ParentId = null,
            Children = new List<TreeNode>()
        };
        var childNode = new TreeNode
        {
            Id = 2,
            Title = "Child",
            Type = "Feature",
            State = "Closed",
            ParentId = 1
        };
        parentNode.Children.Add(childNode);
        var allNodes = new Dictionary<int, TreeNode> { { 1, parentNode }, { 2, childNode } };

        // Act
        var result = await _visibilityService.ShouldHideNodeAsync(childNode, allNodes);

        // Assert
        Assert.IsFalse(result, "Items with non-Done parent should not be hidden");
    }

    [TestMethod]
    public async Task ShouldHideNode_SiblingNotDone_ReturnsFalse()
    {
        // Arrange
        var parentNode = new TreeNode
        {
            Id = 1,
            Title = "Parent",
            Type = "Epic",
            State = "Closed",
            ParentId = null,
            Children = new List<TreeNode>()
        };
        var childNode1 = new TreeNode
        {
            Id = 2,
            Title = "Child 1",
            Type = "Feature",
            State = "Closed",
            ParentId = 1
        };
        var childNode2 = new TreeNode
        {
            Id = 3,
            Title = "Child 2",
            Type = "Feature",
            State = "Active", // Sibling not done
            ParentId = 1
        };
        parentNode.Children.Add(childNode1);
        parentNode.Children.Add(childNode2);
        var allNodes = new Dictionary<int, TreeNode> { { 1, parentNode }, { 2, childNode1 }, { 3, childNode2 } };

        // Act
        var result = await _visibilityService.ShouldHideNodeAsync(childNode1, allNodes);

        // Assert
        Assert.IsFalse(result, "Items with non-Done siblings should not be hidden");
    }

    [TestMethod]
    public async Task ShouldHideNode_HasValidationIssues_ReturnsFalse()
    {
        // Arrange
        var parentNode = new TreeNode
        {
            Id = 1,
            Title = "Parent",
            Type = "Epic",
            State = "Closed",
            ParentId = null,
            Children = new List<TreeNode>()
        };
        var childNode = new TreeNode
        {
            Id = 2,
            Title = "Child",
            Type = "Feature",
            State = "Closed",
            ParentId = 1,
            ValidationIssues = new List<string> { "Error: Some issue" }
        };
        parentNode.Children.Add(childNode);
        var allNodes = new Dictionary<int, TreeNode> { { 1, parentNode }, { 2, childNode } };

        // Act
        var result = await _visibilityService.ShouldHideNodeAsync(childNode, allNodes);

        // Assert
        Assert.IsFalse(result, "Items with validation issues should not be hidden");
    }

    [TestMethod]
    public async Task ShouldHideNode_HasDescendantValidationIssues_ReturnsFalse()
    {
        // Arrange
        var parentNode = new TreeNode
        {
            Id = 1,
            Title = "Parent",
            Type = "Epic",
            State = "Closed",
            ParentId = null,
            Children = new List<TreeNode>()
        };
        var childNode = new TreeNode
        {
            Id = 2,
            Title = "Child",
            Type = "Feature",
            State = "Closed",
            ParentId = 1,
            InvalidDescendantIds = new List<int> { 3 } // Has descendant issues
        };
        parentNode.Children.Add(childNode);
        var allNodes = new Dictionary<int, TreeNode> { { 1, parentNode }, { 2, childNode } };

        // Act
        var result = await _visibilityService.ShouldHideNodeAsync(childNode, allNodes);

        // Assert
        Assert.IsFalse(result, "Items with descendant validation issues should not be hidden");
    }

    [TestMethod]
    public async Task ShouldHideNode_AllConditionsMet_ReturnsTrue()
    {
        // Arrange
        var parentNode = new TreeNode
        {
            Id = 1,
            Title = "Parent",
            Type = "Epic",
            State = "Closed",
            ParentId = null,
            Children = new List<TreeNode>()
        };
        var childNode1 = new TreeNode
        {
            Id = 2,
            Title = "Child 1",
            Type = "Feature",
            State = "Closed",
            ParentId = 1
        };
        var childNode2 = new TreeNode
        {
            Id = 3,
            Title = "Child 2",
            Type = "Feature",
            State = "Closed",
            ParentId = 1
        };
        parentNode.Children.Add(childNode1);
        parentNode.Children.Add(childNode2);
        var allNodes = new Dictionary<int, TreeNode> { { 1, parentNode }, { 2, childNode1 }, { 3, childNode2 } };

        // Act
        var result = await _visibilityService.ShouldHideNodeAsync(childNode1, allNodes);

        // Assert
        Assert.IsTrue(result, "Items meeting all hiding criteria should be hidden");
    }

    [TestMethod]
    public async Task FilterHiddenNodes_HidesCompletedItems()
    {
        // Arrange
        var parentNode = new TreeNode
        {
            Id = 1,
            Title = "Parent",
            Type = "Epic",
            State = "Closed",
            ParentId = null,
            Children = new List<TreeNode>()
        };
        var childNode1 = new TreeNode
        {
            Id = 2,
            Title = "Child 1 (should be hidden with Child 2)",
            Type = "Feature",
            State = "Closed",
            ParentId = 1
        };
        var childNode2 = new TreeNode
        {
            Id = 3,
            Title = "Child 2 (should be hidden with Child 1)",
            Type = "Feature",
            State = "Closed", // Both children are done now
            ParentId = 1
        };
        parentNode.Children.Add(childNode1);
        parentNode.Children.Add(childNode2);
        var allNodes = new Dictionary<int, TreeNode> { { 1, parentNode }, { 2, childNode1 }, { 3, childNode2 } };

        // Act
        var result = await _visibilityService.FilterHiddenNodesAsync(parentNode.Children, allNodes);

        // Assert
        Assert.HasCount(0, result, "Both children should be hidden since both are done");
    }

    [TestMethod]
    public async Task FilterHiddenNodes_KeepsVisibleWhenSiblingNotDone()
    {
        // Arrange
        var parentNode = new TreeNode
        {
            Id = 1,
            Title = "Parent",
            Type = "Epic",
            State = "Closed",
            ParentId = null,
            Children = new List<TreeNode>()
        };
        var childNode1 = new TreeNode
        {
            Id = 2,
            Title = "Child 1 (should be visible)",
            Type = "Feature",
            State = "Closed",
            ParentId = 1
        };
        var childNode2 = new TreeNode
        {
            Id = 3,
            Title = "Child 2 (should be visible)",
            Type = "Feature",
            State = "Active", // One child is not done
            ParentId = 1
        };
        parentNode.Children.Add(childNode1);
        parentNode.Children.Add(childNode2);
        var allNodes = new Dictionary<int, TreeNode> { { 1, parentNode }, { 2, childNode1 }, { 3, childNode2 } };

        // Act
        var result = await _visibilityService.FilterHiddenNodesAsync(parentNode.Children, allNodes);

        // Assert
        Assert.HasCount(2, result, "Both children should be visible because one sibling is not done");
    }

    [TestMethod]
    public async Task FilterHiddenNodes_PreservesTreeStructure()
    {
        // Arrange - Create nested tree
        var rootNode = new TreeNode
        {
            Id = 1,
            Title = "Root",
            Type = "Epic",
            State = "Active",
            ParentId = null,
            Children = new List<TreeNode>()
        };
        var level1 = new TreeNode
        {
            Id = 2,
            Title = "Level 1",
            Type = "Feature",
            State = "Closed",
            ParentId = 1,
            Children = new List<TreeNode>()
        };
        var level2a = new TreeNode
        {
            Id = 3,
            Title = "Level 2a (both should be visible)",
            Type = "PBI",
            State = "Done",
            ParentId = 2
        };
        var level2b = new TreeNode
        {
            Id = 4,
            Title = "Level 2b (both should be visible)",
            Type = "PBI",
            State = "Active",
            ParentId = 2
        };
        level1.Children.Add(level2a);
        level1.Children.Add(level2b);
        rootNode.Children.Add(level1);
        
        var allNodes = new Dictionary<int, TreeNode> 
        { 
            { 1, rootNode }, 
            { 2, level1 },
            { 3, level2a },
            { 4, level2b }
        };

        // Act
        var result = await _visibilityService.FilterHiddenNodesAsync(new List<TreeNode> { rootNode }, allNodes);

        // Assert
        Assert.HasCount(1, result, "Root should still be visible");
        Assert.HasCount(1, result[0].Children, "Level 1 should still be visible");
        Assert.HasCount(2, result[0].Children[0].Children, "Both Level 2 children should be visible because one sibling is not done");
    }

    [TestMethod]
    public async Task ShouldHideNode_ParentMissing_ReturnsFalse()
    {
        // Arrange - Child node references a parent that doesn't exist in the tree
        var childNode = new TreeNode
        {
            Id = 2,
            Title = "Orphaned Child",
            Type = "Feature",
            State = "Closed",
            ParentId = 999 // Parent doesn't exist
        };
        var allNodes = new Dictionary<int, TreeNode> { { 2, childNode } };

        // Act
        var result = await _visibilityService.ShouldHideNodeAsync(childNode, allNodes);

        // Assert
        Assert.IsFalse(result, "Items with missing parent should not be hidden");
    }
}
