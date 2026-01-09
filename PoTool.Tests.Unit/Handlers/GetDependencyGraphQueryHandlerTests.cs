using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Handlers.WorkItems;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Tests.Unit.Handlers;

[TestClass]
public class GetDependencyGraphQueryHandlerTests
{
    private Mock<IWorkItemRepository> _mockRepository = null!;
    private Mock<ILogger<GetDependencyGraphQueryHandler>> _mockLogger = null!;
    private GetDependencyGraphQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockRepository = new Mock<IWorkItemRepository>();
        _mockLogger = new Mock<ILogger<GetDependencyGraphQueryHandler>>();
        _handler = new GetDependencyGraphQueryHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [TestMethod]
    public async Task Handle_WithNoWorkItems_ReturnsEmptyGraph()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());
        var query = new GetDependencyGraphQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsEmpty(result.Nodes);
        Assert.IsEmpty(result.Links);
        Assert.IsEmpty(result.CriticalPaths);
        Assert.IsEmpty(result.BlockedWorkItemIds);
        Assert.IsEmpty(result.CircularDependencies);
    }

    [TestMethod]
    public async Task Handle_WithBasicDependencies_BuildsGraphCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", "Project\\TeamA", 20, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Forward"",""url"":""http://tfs/workItems/2""}]}"),
            CreateWorkItem(2, "Feature", "New", "Project\\TeamA", 10, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Reverse"",""url"":""http://tfs/workItems/1""}]}")
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetDependencyGraphQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result.Nodes);
        Assert.HasCount(2, result.Links);
        
        var node1 = result.Nodes.FirstOrDefault(n => n.WorkItemId == 1);
        Assert.IsNotNull(node1);
        Assert.AreEqual(1, node1.DependencyCount);
        Assert.AreEqual(0, node1.DependentCount);
        Assert.IsFalse(node1.IsBlocking);

        var node2 = result.Nodes.FirstOrDefault(n => n.WorkItemId == 2);
        Assert.IsNotNull(node2);
        Assert.AreEqual(0, node2.DependencyCount);
        Assert.AreEqual(1, node2.DependentCount);
        Assert.IsTrue(node2.IsBlocking);
    }

    [TestMethod]
    public async Task Handle_WithAreaPathFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", "Project\\TeamA", 20, "{}"),
            CreateWorkItem(2, "Feature", "New", "Project\\TeamB", 10, "{}"),
            CreateWorkItem(3, "Task", "New", "Project\\TeamA", 5, "{}")
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetDependencyGraphQuery(AreaPathFilter: "TeamA");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result.Nodes);
        Assert.IsTrue(result.Nodes.All(n => n.WorkItemId == 1 || n.WorkItemId == 3));
    }

    [TestMethod]
    public async Task Handle_WithWorkItemTypeFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", "Project\\TeamA", 20, "{}"),
            CreateWorkItem(2, "Feature", "New", "Project\\TeamA", 10, "{}"),
            CreateWorkItem(3, "Task", "New", "Project\\TeamA", 5, "{}")
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetDependencyGraphQuery(WorkItemTypes: new[] { "Epic", "Feature" });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result.Nodes);
        Assert.IsTrue(result.Nodes.All(n => n.Type == "Epic" || n.Type == "Feature"));
    }

    [TestMethod]
    public async Task Handle_WithWorkItemIdsFilter_FiltersCorrectly()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", "Project\\TeamA", 20, "{}"),
            CreateWorkItem(2, "Feature", "New", "Project\\TeamA", 10, "{}"),
            CreateWorkItem(3, "Task", "New", "Project\\TeamA", 5, "{}")
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetDependencyGraphQuery(WorkItemIds: new[] { 1, 3 });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result.Nodes);
        Assert.IsTrue(result.Nodes.All(n => n.WorkItemId == 1 || n.WorkItemId == 3));
    }

    [TestMethod]
    public async Task Handle_WithCircularDependencies_DetectsCircles()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "New", "Project\\TeamA", 5, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Forward"",""url"":""http://tfs/workItems/2""}]}"),
            CreateWorkItem(2, "Task", "New", "Project\\TeamA", 5, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Forward"",""url"":""http://tfs/workItems/3""}]}"),
            CreateWorkItem(3, "Task", "New", "Project\\TeamA", 5, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Forward"",""url"":""http://tfs/workItems/1""}]}")
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetDependencyGraphQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(3, result.Nodes);
        Assert.IsNotEmpty(result.CircularDependencies, "Should detect circular dependency");
    }

    [TestMethod]
    public async Task Handle_WithLongDependencyChain_FindsCriticalPaths()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "New", "Project\\TeamA", 5, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Forward"",""url"":""http://tfs/workItems/2""}]}"),
            CreateWorkItem(2, "Task", "New", "Project\\TeamA", 8, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Forward"",""url"":""http://tfs/workItems/3""}]}"),
            CreateWorkItem(3, "Task", "New", "Project\\TeamA", 10, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Forward"",""url"":""http://tfs/workItems/4""}]}"),
            CreateWorkItem(4, "Task", "New", "Project\\TeamA", 15, "{}"),
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetDependencyGraphQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(4, result.Nodes);
        Assert.IsNotEmpty(result.CriticalPaths, "Should find critical paths");
        
        var longestPath = result.CriticalPaths.OrderByDescending(p => p.ChainLength).First();
        Assert.AreEqual(4, longestPath.ChainLength);
        Assert.AreEqual(38, longestPath.TotalEffort); // 5 + 8 + 10 + 15
    }

    [TestMethod]
    public async Task Handle_WithBlockingWorkItems_IdentifiesBlockers()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Task", "New", "Project\\TeamA", 10, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Reverse"",""url"":""http://tfs/workItems/2""}]}"),
            CreateWorkItem(2, "Task", "New", "Project\\TeamA", 5, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Forward"",""url"":""http://tfs/workItems/1""}]}")
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetDependencyGraphQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.BlockedWorkItemIds, "Should identify blocking work items");
        
#pragma warning disable MSTEST0037
        Assert.IsTrue(result.BlockedWorkItemIds.Contains(1), "Work item 1 should be marked as blocking");
    }

    [TestMethod]
    public async Task Handle_WithParentChildLinks_CreatesHierarchyLinks()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", "Project\\TeamA", 20, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Hierarchy-Forward"",""url"":""http://tfs/workItems/2""}]}"),
            CreateWorkItem(2, "Feature", "New", "Project\\TeamA", 10, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Hierarchy-Reverse"",""url"":""http://tfs/workItems/1""}]}")
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetDependencyGraphQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result.Nodes);
        Assert.HasCount(2, result.Links);
        
        // Hierarchy links are mapped as RelatedTo since they don't contain "parent" or "child" keywords
        var hierarchyLink = result.Links.FirstOrDefault(l => l.LinkType == DependencyLinkType.RelatedTo);
        Assert.IsNotNull(hierarchyLink, "Should have hierarchy link mapped as RelatedTo");
    }

    [TestMethod]
    public async Task Handle_WithInvalidJsonPayload_HandlesGracefully()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", "Project\\TeamA", 20, "invalid json"),
            CreateWorkItem(2, "Feature", "New", "Project\\TeamA", 10, "{}")
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetDependencyGraphQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result.Nodes);
        
        var node1 = result.Nodes.FirstOrDefault(n => n.WorkItemId == 1);
        Assert.IsNotNull(node1);
        Assert.AreEqual(0, node1.DependencyCount, "Should have no dependencies when JSON is invalid");
    }

    [TestMethod]
    public async Task Handle_WithMissingTargetWorkItem_IgnoresLink()
    {
        // Arrange
        var workItems = new List<WorkItemDto>
        {
            CreateWorkItem(1, "Epic", "New", "Project\\TeamA", 20, 
                @"{""relations"":[{""rel"":""System.LinkTypes.Dependency-Forward"",""url"":""http://tfs/workItems/999""}]}")
        };

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);
        var query = new GetDependencyGraphQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Nodes);
        Assert.IsEmpty(result.Links, "Should not create link to missing work item");
    }

    private static WorkItemDto CreateWorkItem(int tfsId, string type, string state, string areaPath, int? effort, string jsonPayload)
    {
        return new WorkItemDto(
            TfsId: tfsId,
            Type: type,
            Title: $"Work Item {tfsId}",
            ParentTfsId: null,
            AreaPath: areaPath,
            IterationPath: "Project\\2024\\Sprint1",
            State: state,
            JsonPayload: jsonPayload,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort
        );
    }
}
