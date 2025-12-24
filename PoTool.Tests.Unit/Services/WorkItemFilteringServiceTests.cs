using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using Moq;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemFilteringServiceTests
{
    private Mock<ITreeBuilderService> _mockTreeBuilderService = null!;
    private WorkItemFilteringService _service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _mockTreeBuilderService = new Mock<ITreeBuilderService>();
        _service = new WorkItemFilteringService(_mockTreeBuilderService.Object);
    }

    [TestMethod]
    public void FilterByValidationWithAncestors_IncludesTargetItemAndAncestors()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new() { TfsId = 1, Title = "Goal", ParentTfsId = null, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 2, Title = "Feature", ParentTfsId = 1, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 3, Title = "Story", ParentTfsId = 2, ValidationIssues = new List<ValidationIssue>() }
        };
        var targetIds = new HashSet<int> { 3 }; // Target the story

        // Act
        var result = _service.FilterByValidationWithAncestors(workItems, targetIds).ToList();

        // Assert
        Assert.HasCount(3, result);
        Assert.IsTrue(result.Any(wi => wi.TfsId == 1)); // Goal included
        Assert.IsTrue(result.Any(wi => wi.TfsId == 2)); // Feature included
        Assert.IsTrue(result.Any(wi => wi.TfsId == 3)); // Story included
    }

    [TestMethod]
    public void FilterByValidationWithAncestors_HandlesMultipleTargets()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new() { TfsId = 1, Title = "Goal", ParentTfsId = null, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 2, Title = "Feature A", ParentTfsId = 1, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 3, Title = "Feature B", ParentTfsId = 1, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 4, Title = "Story A", ParentTfsId = 2, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 5, Title = "Story B", ParentTfsId = 3, ValidationIssues = new List<ValidationIssue>() }
        };
        var targetIds = new HashSet<int> { 4, 5 }; // Target both stories

        // Act
        var result = _service.FilterByValidationWithAncestors(workItems, targetIds).ToList();

        // Assert
        Assert.HasCount(5, result); // All items should be included
    }

    [TestMethod]
    public void FilterByValidationWithAncestors_HandlesOrphanedItems()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new() { TfsId = 1, Title = "Goal", ParentTfsId = null, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 3, Title = "Orphan Story", ParentTfsId = 999, ValidationIssues = new List<ValidationIssue>() } // Parent doesn't exist
        };
        var targetIds = new HashSet<int> { 3 };

        // Act
        var result = _service.FilterByValidationWithAncestors(workItems, targetIds).ToList();

        // Assert
        Assert.HasCount(1, result);
        Assert.IsTrue(result.Any(wi => wi.TfsId == 3)); // Only the orphan itself
    }

    [TestMethod]
    public void GetWorkItemIdsByValidationFilter_ParentProgress_ReturnsMatchingIds()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new()
            {
                TfsId = 1,
                Title = "Item 1",
                ValidationIssues = new List<ValidationIssue>
                {
                    new() { Severity = "Error", Message = "Parent is not in progress" }
                }
            },
            new()
            {
                TfsId = 2,
                Title = "Item 2",
                ValidationIssues = new List<ValidationIssue>
                {
                    new() { Severity = "Error", Message = "Missing effort" }
                }
            },
            new()
            {
                TfsId = 3,
                Title = "Item 3",
                ValidationIssues = new List<ValidationIssue>
                {
                    new() { Severity = "Error", Message = "Ancestor validation failed" }
                }
            }
        };

        // Act
        var result = _service.GetWorkItemIdsByValidationFilter(workItems, "parentProgress").ToList();

        // Assert
        Assert.HasCount(2, result);
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.Contains(1));
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.Contains(3));
    }

    [TestMethod]
    public void GetWorkItemIdsByValidationFilter_MissingEffort_ReturnsMatchingIds()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new()
            {
                TfsId = 1,
                Title = "Item 1",
                ValidationIssues = new List<ValidationIssue>
                {
                    new() { Severity = "Error", Message = "Missing effort" }
                }
            },
            new()
            {
                TfsId = 2,
                Title = "Item 2",
                ValidationIssues = new List<ValidationIssue>
                {
                    new() { Severity = "Error", Message = "Parent is not in progress" }
                }
            }
        };

        // Act
        var result = _service.GetWorkItemIdsByValidationFilter(workItems, "missingEffort").ToList();

        // Assert
        Assert.HasCount(1, result);
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.Contains(1));
    }

    [TestMethod]
    public void CountWorkItemsByValidationFilter_ReturnsCorrectCount()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new()
            {
                TfsId = 1,
                ValidationIssues = new List<ValidationIssue>
                {
                    new() { Message = "Missing effort" }
                }
            },
            new()
            {
                TfsId = 2,
                ValidationIssues = new List<ValidationIssue>
                {
                    new() { Message = "Missing effort" }
                }
            },
            new()
            {
                TfsId = 3,
                ValidationIssues = new List<ValidationIssue>
                {
                    new() { Message = "Parent issue" }
                }
            }
        };

        // Act
        var count = _service.CountWorkItemsByValidationFilter(workItems, "missingEffort");

        // Assert
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void ApplyCombinedFilter_NoFilters_ReturnsAllItems()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new() { TfsId = 1, Title = "Item 1", ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 2, Title = "Item 2", ValidationIssues = new List<ValidationIssue>() }
        };

        // Act
        var result = _service.ApplyCombinedFilter(workItems, null, Enumerable.Empty<string>()).ToList();

        // Assert
        Assert.HasCount(2, result);
    }

    [TestMethod]
    public void ApplyCombinedFilter_WithValidationFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new()
            {
                TfsId = 1,
                Title = "Item 1",
                ParentTfsId = null,
                ValidationIssues = new List<ValidationIssue>
                {
                    new() { Message = "Missing effort" }
                }
            },
            new()
            {
                TfsId = 2,
                Title = "Item 2",
                ParentTfsId = null,
                ValidationIssues = new List<ValidationIssue>()
            }
        };
        var enabledFilters = new[] { "missingEffort" };

        // Act
        var result = _service.ApplyCombinedFilter(workItems, null, enabledFilters).ToList();

        // Assert
        Assert.HasCount(1, result);
        Assert.AreEqual(1, result[0].TfsId);
    }

    [TestMethod]
    public void IsDescendantOfGoals_ItemIsGoal_ReturnsTrue()
    {
        // Arrange
        var item = new WorkItemWithValidationDto { TfsId = 1, Title = "Goal", ValidationIssues = new List<ValidationIssue>() };
        var goalIds = new List<int> { 1 };
        var allWorkItems = new List<WorkItemWithValidationDto> { item };

        // Act
        var result = _service.IsDescendantOfGoals(item, goalIds, allWorkItems);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsDescendantOfGoals_ItemIsChildOfGoal_ReturnsTrue()
    {
        // Arrange
        var goal = new WorkItemWithValidationDto { TfsId = 1, Title = "Goal", ParentTfsId = null, ValidationIssues = new List<ValidationIssue>() };
        var child = new WorkItemWithValidationDto { TfsId = 2, Title = "Feature", ParentTfsId = 1, ValidationIssues = new List<ValidationIssue>() };
        var goalIds = new List<int> { 1 };
        var allWorkItems = new List<WorkItemWithValidationDto> { goal, child };

        // Act
        var result = _service.IsDescendantOfGoals(child, goalIds, allWorkItems);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsDescendantOfGoals_ItemNotRelatedToGoal_ReturnsFalse()
    {
        // Arrange
        var goal = new WorkItemWithValidationDto { TfsId = 1, Title = "Goal", ParentTfsId = null, ValidationIssues = new List<ValidationIssue>() };
        var unrelated = new WorkItemWithValidationDto { TfsId = 3, Title = "Unrelated", ParentTfsId = null, ValidationIssues = new List<ValidationIssue>() };
        var goalIds = new List<int> { 1 };
        var allWorkItems = new List<WorkItemWithValidationDto> { goal, unrelated };

        // Act
        var result = _service.IsDescendantOfGoals(unrelated, goalIds, allWorkItems);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsDescendantOfGoals_EmptyGoalIds_ReturnsTrue()
    {
        // Arrange
        var item = new WorkItemWithValidationDto { TfsId = 1, Title = "Item", ValidationIssues = new List<ValidationIssue>() };
        var goalIds = new List<int>();
        var allWorkItems = new List<WorkItemWithValidationDto> { item };

        // Act
        var result = _service.IsDescendantOfGoals(item, goalIds, allWorkItems);

        // Assert
        Assert.IsTrue(result);
    }
}
