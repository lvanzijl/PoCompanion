using PoTool.Api.Services;
using PoTool.Shared.ReleasePlanning;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public class ConnectorDerivationServiceTests
{
    private ConnectorDerivationService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new ConnectorDerivationService();
    }

    [TestMethod]
    public void DeriveConnectorsForLane_EmptyPlacements_ReturnsEmpty()
    {
        // Arrange
        var placements = new List<EpicPlacementDto>();

        // Act
        var result = _service.DeriveConnectorsForLane(placements);

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void DeriveConnectorsForLane_SingleRowSingleEpic_ReturnsEmpty()
    {
        // Arrange
        var placements = new List<EpicPlacementDto>
        {
            new() { Id = 1, EpicId = 101, LaneId = 1, RowIndex = 0, OrderInRow = 0 }
        };

        // Act
        var result = _service.DeriveConnectorsForLane(placements);

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void DeriveConnectorsForLane_TwoRowsSingleEpicEach_ReturnsDirectConnector()
    {
        // Arrange
        var placements = new List<EpicPlacementDto>
        {
            new() { Id = 1, EpicId = 101, LaneId = 1, RowIndex = 0, OrderInRow = 0 },
            new() { Id = 2, EpicId = 102, LaneId = 1, RowIndex = 1, OrderInRow = 0 }
        };

        // Act
        var result = _service.DeriveConnectorsForLane(placements);

        // Assert
        Assert.HasCount(1, result);
        Assert.AreEqual(1, result[0].SourcePlacementId);
        Assert.AreEqual(2, result[0].TargetPlacementId);
        Assert.AreEqual(ConnectorType.Direct, result[0].Type);
    }

    [TestMethod]
    public void DeriveConnectorsForLane_OneToMany_ReturnsSplitConnectors()
    {
        // Arrange: 1 Epic in row 0, 3 Epics in row 1 (split)
        var placements = new List<EpicPlacementDto>
        {
            new() { Id = 1, EpicId = 101, LaneId = 1, RowIndex = 0, OrderInRow = 0 },
            new() { Id = 2, EpicId = 102, LaneId = 1, RowIndex = 1, OrderInRow = 0 },
            new() { Id = 3, EpicId = 103, LaneId = 1, RowIndex = 1, OrderInRow = 1 },
            new() { Id = 4, EpicId = 104, LaneId = 1, RowIndex = 1, OrderInRow = 2 }
        };

        // Act
        var result = _service.DeriveConnectorsForLane(placements);

        // Assert
        Assert.HasCount(3, result);
        Assert.IsTrue(result.All(c => c.Type == ConnectorType.Split));
        Assert.IsTrue(result.All(c => c.SourcePlacementId == 1));
        Assert.IsTrue(result.Select(c => c.TargetPlacementId).OrderBy(x => x).SequenceEqual(new[] { 2, 3, 4 }));
    }

    [TestMethod]
    public void DeriveConnectorsForLane_ManyToOne_ReturnsMergeConnectors()
    {
        // Arrange: 3 Epics in row 0, 1 Epic in row 1 (merge)
        var placements = new List<EpicPlacementDto>
        {
            new() { Id = 1, EpicId = 101, LaneId = 1, RowIndex = 0, OrderInRow = 0 },
            new() { Id = 2, EpicId = 102, LaneId = 1, RowIndex = 0, OrderInRow = 1 },
            new() { Id = 3, EpicId = 103, LaneId = 1, RowIndex = 0, OrderInRow = 2 },
            new() { Id = 4, EpicId = 104, LaneId = 1, RowIndex = 1, OrderInRow = 0 }
        };

        // Act
        var result = _service.DeriveConnectorsForLane(placements);

        // Assert
        Assert.HasCount(3, result);
        Assert.IsTrue(result.All(c => c.Type == ConnectorType.Merge));
        Assert.IsTrue(result.All(c => c.TargetPlacementId == 4));
        Assert.IsTrue(result.Select(c => c.SourcePlacementId).OrderBy(x => x).SequenceEqual(new[] { 1, 2, 3 }));
    }

    [TestMethod]
    public void DeriveConnectorsForLane_ParallelContinuation_ReturnsParallelConnectors()
    {
        // Arrange: 2 Epics in row 0, 2 Epics in row 1 (parallel)
        var placements = new List<EpicPlacementDto>
        {
            new() { Id = 1, EpicId = 101, LaneId = 1, RowIndex = 0, OrderInRow = 0 },
            new() { Id = 2, EpicId = 102, LaneId = 1, RowIndex = 0, OrderInRow = 1 },
            new() { Id = 3, EpicId = 103, LaneId = 1, RowIndex = 1, OrderInRow = 0 },
            new() { Id = 4, EpicId = 104, LaneId = 1, RowIndex = 1, OrderInRow = 1 }
        };

        // Act
        var result = _service.DeriveConnectorsForLane(placements);

        // Assert
        Assert.HasCount(2, result);
        Assert.IsTrue(result.All(c => c.Type == ConnectorType.Parallel));
        // 1 -> 3, 2 -> 4
        Assert.IsTrue(result.Any(c => c.SourcePlacementId == 1 && c.TargetPlacementId == 3));
        Assert.IsTrue(result.Any(c => c.SourcePlacementId == 2 && c.TargetPlacementId == 4));
    }

    [TestMethod]
    public void DeriveConnectorsForLane_ThreeRowsChain_ReturnsChainedConnectors()
    {
        // Arrange: 1 -> 1 -> 1 (chain of direct connectors)
        var placements = new List<EpicPlacementDto>
        {
            new() { Id = 1, EpicId = 101, LaneId = 1, RowIndex = 0, OrderInRow = 0 },
            new() { Id = 2, EpicId = 102, LaneId = 1, RowIndex = 1, OrderInRow = 0 },
            new() { Id = 3, EpicId = 103, LaneId = 1, RowIndex = 2, OrderInRow = 0 }
        };

        // Act
        var result = _service.DeriveConnectorsForLane(placements);

        // Assert
        Assert.HasCount(2, result);
        Assert.IsTrue(result.All(c => c.Type == ConnectorType.Direct));
        Assert.IsTrue(result.Any(c => c.SourcePlacementId == 1 && c.TargetPlacementId == 2));
        Assert.IsTrue(result.Any(c => c.SourcePlacementId == 2 && c.TargetPlacementId == 3));
    }

    [TestMethod]
    public void DeriveAllConnectors_MultipleLanes_ProcessesEachLaneIndependently()
    {
        // Arrange
        var lanes = new List<LaneDto>
        {
            new() { Id = 1, ObjectiveId = 100, ObjectiveTitle = "Obj 1" },
            new() { Id = 2, ObjectiveId = 200, ObjectiveTitle = "Obj 2" }
        };

        var placements = new List<EpicPlacementDto>
        {
            // Lane 1: 1 -> 1 (direct)
            new() { Id = 1, EpicId = 101, LaneId = 1, RowIndex = 0, OrderInRow = 0 },
            new() { Id = 2, EpicId = 102, LaneId = 1, RowIndex = 1, OrderInRow = 0 },
            // Lane 2: 1 -> 2 (split)
            new() { Id = 3, EpicId = 201, LaneId = 2, RowIndex = 0, OrderInRow = 0 },
            new() { Id = 4, EpicId = 202, LaneId = 2, RowIndex = 1, OrderInRow = 0 },
            new() { Id = 5, EpicId = 203, LaneId = 2, RowIndex = 1, OrderInRow = 1 }
        };

        // Act
        var result = _service.DeriveAllConnectors(lanes, placements);

        // Assert
        Assert.HasCount(3, result); // 1 direct + 2 split
        
        // Lane 1 connector
        var lane1Connector = result.Single(c => c.SourcePlacementId == 1);
        Assert.AreEqual(2, lane1Connector.TargetPlacementId);
        Assert.AreEqual(ConnectorType.Direct, lane1Connector.Type);
        
        // Lane 2 connectors
        var lane2Connectors = result.Where(c => c.SourcePlacementId == 3).ToList();
        Assert.HasCount(2, lane2Connectors);
        Assert.IsTrue(lane2Connectors.All(c => c.Type == ConnectorType.Split));
    }
}
