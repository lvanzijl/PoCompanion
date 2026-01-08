using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class WorkItemFilteringServiceClientTests
{
    private Mock<ITreeBuilderService> _mockTreeBuilderService = null!;
    private Mock<IFilteringClient> _mockFilteringClient = null!;
    private WorkItemFilteringService _service = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _mockTreeBuilderService = new Mock<ITreeBuilderService>();
        _mockFilteringClient = new Mock<IFilteringClient>();
        _service = new WorkItemFilteringService(_mockTreeBuilderService.Object, _mockFilteringClient.Object);
    }

    [TestMethod]
    public async Task FilterByValidationWithAncestorsAsync_CallsApiAndReturnsMatchingItems()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new() { TfsId = 1, Title = "Goal", ParentTfsId = 0, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 2, Title = "Feature", ParentTfsId = 1, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 3, Title = "Story", ParentTfsId = 2, ValidationIssues = new List<ValidationIssue>() }
        };
        var targetIds = new HashSet<int> { 3 };

        _mockFilteringClient
            .Setup(x => x.CreateByValidationWithAncestorsAsync(It.IsAny<FilterByValidationRequest>()))
            .ReturnsAsync(new FilterByValidationResponse
            {
                WorkItemIds = new List<int> { 1, 2, 3 }
            });

        // Act
        var result = await _service.FilterByValidationWithAncestorsAsync(workItems, targetIds);
        var resultList = result.ToList();

        // Assert
        Assert.HasCount(3, resultList);
        Assert.IsTrue(resultList.Any(wi => wi.TfsId == 1));
        Assert.IsTrue(resultList.Any(wi => wi.TfsId == 2));
        Assert.IsTrue(resultList.Any(wi => wi.TfsId == 3));
        
        _mockFilteringClient.Verify(x => x.CreateByValidationWithAncestorsAsync(
            It.Is<FilterByValidationRequest>(r => r.TargetIds.Count == targetIds.Count && targetIds.All(id => r.TargetIds.Contains(id)))),
            Times.Once);
    }

    [TestMethod]
    public async Task GetWorkItemIdsByValidationFilterAsync_CallsApiAndReturnsIds()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new() { TfsId = 1, Title = "Item 1", ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 2, Title = "Item 2", ValidationIssues = new List<ValidationIssue>() }
        };
        var filterId = "missingEffort";

        _mockFilteringClient
            .Setup(x => x.CreateIdsByValidationFilterAsync(It.IsAny<GetWorkItemIdsByValidationFilterRequest>()))
            .ReturnsAsync(new GetWorkItemIdsByValidationFilterResponse
            {
                WorkItemIds = new List<int> { 1, 2 }
            });

        // Act
        var result = await _service.GetWorkItemIdsByValidationFilterAsync(workItems, filterId);

        // Assert
        var resultList = result.ToList();
        Assert.HasCount(2, resultList);
        Assert.Contains(1, resultList);
        Assert.Contains(2, resultList);
        
        _mockFilteringClient.Verify(x => x.CreateIdsByValidationFilterAsync(
            It.Is<GetWorkItemIdsByValidationFilterRequest>(r => r.FilterId == filterId)),
            Times.Once);
    }

    [TestMethod]
    public async Task CountWorkItemsByValidationFilterAsync_CallsApiAndReturnsCount()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new() { TfsId = 1, Title = "Item 1", ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 2, Title = "Item 2", ValidationIssues = new List<ValidationIssue>() }
        };
        var filterId = "missingEffort";

        _mockFilteringClient
            .Setup(x => x.CreateCountByValidationFilterAsync(It.IsAny<CountWorkItemsByValidationFilterRequest>()))
            .ReturnsAsync(new CountWorkItemsByValidationFilterResponse
            {
                Count = 2
            });

        // Act
        var result = await _service.CountWorkItemsByValidationFilterAsync(workItems, filterId);

        // Assert
        Assert.AreEqual(2, result);
        
        _mockFilteringClient.Verify(x => x.CreateCountByValidationFilterAsync(
            It.Is<CountWorkItemsByValidationFilterRequest>(r => r.FilterId == filterId)),
            Times.Once);
    }

    [TestMethod]
    public async Task ApplyCombinedFilterAsync_WithValidationFilters_CallsApiCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemWithValidationDto>
        {
            new() { TfsId = 1, Title = "Item 1", ParentTfsId = 0, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 2, Title = "Item 2", ParentTfsId = 1, ValidationIssues = new List<ValidationIssue>() }
        };
        var textFilter = "";
        var enabledFilters = new List<string> { "missingEffort" };

        _mockFilteringClient
            .Setup(x => x.CreateIdsByValidationFilterAsync(It.IsAny<GetWorkItemIdsByValidationFilterRequest>()))
            .ReturnsAsync(new GetWorkItemIdsByValidationFilterResponse
            {
                WorkItemIds = new List<int> { 1 }
            });

        _mockFilteringClient
            .Setup(x => x.CreateByValidationWithAncestorsAsync(It.IsAny<FilterByValidationRequest>()))
            .ReturnsAsync(new FilterByValidationResponse
            {
                WorkItemIds = new List<int> { 1 }
            });

        // Act
        var result = await _service.ApplyCombinedFilterAsync(workItems, textFilter, enabledFilters);

        // Assert
        var resultList = result.ToList();
        Assert.HasCount(1, resultList);
        Assert.AreEqual(1, resultList[0].TfsId);
    }

    [TestMethod]
    public async Task IsDescendantOfGoalsAsync_CallsApiAndReturnsResult()
    {
        // Arrange
        var item = new WorkItemWithValidationDto 
        { 
            TfsId = 3, 
            Title = "Story", 
            ParentTfsId = 2, 
            ValidationIssues = new List<ValidationIssue>() 
        };
        var goalIds = new List<int> { 1 };
        var allWorkItems = new List<WorkItemWithValidationDto>
        {
            new() { TfsId = 1, Title = "Goal", ParentTfsId = 0, ValidationIssues = new List<ValidationIssue>() },
            new() { TfsId = 2, Title = "Feature", ParentTfsId = 1, ValidationIssues = new List<ValidationIssue>() },
            item
        };

        _mockFilteringClient
            .Setup(x => x.CreateIsDescendantOfGoalsAsync(It.IsAny<IsDescendantOfGoalsRequest>()))
            .ReturnsAsync(new IsDescendantOfGoalsResponse
            {
                IsDescendant = true
            });

        // Act
        var result = await _service.IsDescendantOfGoalsAsync(item, goalIds, allWorkItems);

        // Assert
        Assert.IsTrue(result);
        
        _mockFilteringClient.Verify(x => x.CreateIsDescendantOfGoalsAsync(
            It.Is<IsDescendantOfGoalsRequest>(r => r.WorkItemId == 3 && r.GoalIds.SequenceEqual(goalIds))),
            Times.Once);
    }
}
