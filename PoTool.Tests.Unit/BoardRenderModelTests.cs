using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Client.Components.ReleasePlanning;
using PoTool.Shared.ReleasePlanning;

namespace PoTool.Tests.Unit;

[TestClass]
public class BoardRenderModelTests
{
    [TestMethod]
    public void Create_WithNoDragState_RendersNormally()
    {
        // Arrange
        var board = new ReleasePlanningBoardDto
        {
            Lanes = new[]
            {
                new LaneDto { Id = 1, ObjectiveId = 10, ObjectiveTitle = "Objective 1", DisplayOrder = 0 }
            },
            Placements = new[]
            {
                new EpicPlacementDto { Id = 1, EpicId = 100, LaneId = 1, RowIndex = 0, OrderInRow = 0, EpicTitle = "Epic 1" },
                new EpicPlacementDto { Id = 2, EpicId = 101, LaneId = 1, RowIndex = 1, OrderInRow = 0, EpicTitle = "Epic 2" }
            },
            MilestoneLines = [],
            IterationLines = [],
            Connectors = [],
            MaxRowIndex = 1,
            TotalRows = 2
        };
        var dragState = DragState.Empty;

        // Act
        var result = BoardRenderModelFactory.Create(board, dragState);

        // Assert
        Assert.HasCount(1, result.Lanes);
        Assert.HasCount(2, result.Lanes[0].Rows);
        Assert.AreEqual(0, result.Lanes[0].Rows[0].RowIndex);
        Assert.AreEqual(1, result.Lanes[0].Rows[1].RowIndex);
        Assert.IsFalse(result.Lanes[0].Rows[0].IsInsertedPreview);
        Assert.IsFalse(result.Lanes[0].Rows[1].IsInsertedPreview);
    }

    [TestMethod]
    public void Create_WithDragIntoExistingRow_ShowsPlaceholder()
    {
        // Arrange
        var board = new ReleasePlanningBoardDto
        {
            Lanes = new[]
            {
                new LaneDto { Id = 1, ObjectiveId = 10, ObjectiveTitle = "Objective 1", DisplayOrder = 0 }
            },
            Placements = new[]
            {
                new EpicPlacementDto { Id = 1, EpicId = 100, LaneId = 1, RowIndex = 0, OrderInRow = 0, EpicTitle = "Epic 1" }
            },
            MilestoneLines = [],
            IterationLines = [],
            Connectors = [],
            MaxRowIndex = 0,
            TotalRows = 1
        };
        var dragState = new DragState
        {
            DraggingEpicId = 101,
            SourceObjectiveId = 10,
            HoverLaneId = 1,
            HoverRowIndex = 0,
            IsInsertingNewRow = false
        };

        // Act
        var result = BoardRenderModelFactory.Create(board, dragState);

        // Assert
        Assert.HasCount(1, result.Lanes);
        var row = result.Lanes[0].Rows[0];
        Assert.AreEqual(0, row.RowIndex);
        Assert.HasCount(2, row.Cards); // Original epic + placeholder
        Assert.IsTrue(row.Cards.Any(c => c.IsPlaceholder));
        Assert.IsFalse(row.IsInsertedPreview);
    }

    [TestMethod]
    public void Create_WithNewRowInsertion_ShowsInsertedRowWithPushDown()
    {
        // Arrange
        var board = new ReleasePlanningBoardDto
        {
            Lanes = new[]
            {
                new LaneDto { Id = 1, ObjectiveId = 10, ObjectiveTitle = "Objective 1", DisplayOrder = 0 },
                new LaneDto { Id = 2, ObjectiveId = 20, ObjectiveTitle = "Objective 2", DisplayOrder = 1 }
            },
            Placements = new[]
            {
                new EpicPlacementDto { Id = 1, EpicId = 100, LaneId = 1, RowIndex = 0, OrderInRow = 0, EpicTitle = "Epic 1" },
                new EpicPlacementDto { Id = 2, EpicId = 101, LaneId = 1, RowIndex = 1, OrderInRow = 0, EpicTitle = "Epic 2" },
                new EpicPlacementDto { Id = 3, EpicId = 102, LaneId = 2, RowIndex = 0, OrderInRow = 0, EpicTitle = "Epic 3" }
            },
            MilestoneLines = [],
            IterationLines = [],
            Connectors = [],
            MaxRowIndex = 1,
            TotalRows = 2
        };
        var dragState = new DragState
        {
            DraggingEpicId = 103,
            SourceObjectiveId = 10,
            HoverLaneId = 1,
            HoverRowIndex = 1,
            IsInsertingNewRow = true
        };

        // Act
        var result = BoardRenderModelFactory.Create(board, dragState);

        // Assert
        Assert.HasCount(2, result.Lanes);
        
        // Check target lane (lane 1)
        var targetLane = result.Lanes[0];
        Assert.HasCount(3, targetLane.Rows);
        
        // Row 0 should remain unchanged
        Assert.AreEqual(0, targetLane.Rows[0].RowIndex);
        Assert.IsFalse(targetLane.Rows[0].IsInsertedPreview);
        
        // Row 1 should be the inserted preview row
        Assert.AreEqual(1, targetLane.Rows[1].RowIndex);
        Assert.IsTrue(targetLane.Rows[1].IsInsertedPreview);
        Assert.IsTrue(targetLane.Rows[1].Cards.Any(c => c.IsPlaceholder));
        
        // Original row 1 should be pushed to row 2
        Assert.AreEqual(2, targetLane.Rows[2].RowIndex);
        Assert.IsFalse(targetLane.Rows[2].IsInsertedPreview);
        
        // Check other lane (lane 2) also shows the inserted row
        var otherLane = result.Lanes[1];
        Assert.HasCount(2, otherLane.Rows); // Row 0 + inserted row 1 (no row 2 because nothing to push down)
        
        // Row 0 should remain unchanged
        Assert.AreEqual(0, otherLane.Rows[0].RowIndex);
        
        // Row 1 should be empty (no placeholder in other lanes)
        Assert.AreEqual(1, otherLane.Rows[1].RowIndex);
        Assert.IsFalse(otherLane.Rows[1].IsInsertedPreview);
        Assert.IsEmpty(otherLane.Rows[1].Cards);
    }

    [TestMethod]
    public void Create_RowWithSingleEpic_HasCenterLayout()
    {
        // Arrange
        var board = new ReleasePlanningBoardDto
        {
            Lanes = new[]
            {
                new LaneDto { Id = 1, ObjectiveId = 10, ObjectiveTitle = "Objective 1", DisplayOrder = 0 }
            },
            Placements = new[]
            {
                new EpicPlacementDto { Id = 1, EpicId = 100, LaneId = 1, RowIndex = 0, OrderInRow = 0, EpicTitle = "Epic 1" }
            },
            MilestoneLines = [],
            IterationLines = [],
            Connectors = [],
            MaxRowIndex = 0,
            TotalRows = 1
        };
        var dragState = DragState.Empty;

        // Act
        var result = BoardRenderModelFactory.Create(board, dragState);

        // Assert
        var row = result.Lanes[0].Rows[0];
        Assert.AreEqual(RowLayoutType.Center, row.LayoutType);
    }

    [TestMethod]
    public void Create_RowWithMultipleEpics_HasJustifyLayout()
    {
        // Arrange
        var board = new ReleasePlanningBoardDto
        {
            Lanes = new[]
            {
                new LaneDto { Id = 1, ObjectiveId = 10, ObjectiveTitle = "Objective 1", DisplayOrder = 0 }
            },
            Placements = new[]
            {
                new EpicPlacementDto { Id = 1, EpicId = 100, LaneId = 1, RowIndex = 0, OrderInRow = 0, EpicTitle = "Epic 1" },
                new EpicPlacementDto { Id = 2, EpicId = 101, LaneId = 1, RowIndex = 0, OrderInRow = 1, EpicTitle = "Epic 2" }
            },
            MilestoneLines = [],
            IterationLines = [],
            Connectors = [],
            MaxRowIndex = 0,
            TotalRows = 1
        };
        var dragState = DragState.Empty;

        // Act
        var result = BoardRenderModelFactory.Create(board, dragState);

        // Assert
        var row = result.Lanes[0].Rows[0];
        Assert.AreEqual(RowLayoutType.Justify, row.LayoutType);
    }

    [TestMethod]
    public void Create_WithDraggingPlacement_KeepsItVisibleInSourceRow()
    {
        // Arrange
        var board = new ReleasePlanningBoardDto
        {
            Lanes = new[]
            {
                new LaneDto { Id = 1, ObjectiveId = 10, ObjectiveTitle = "Objective 1", DisplayOrder = 0 }
            },
            Placements = new[]
            {
                new EpicPlacementDto { Id = 1, EpicId = 100, LaneId = 1, RowIndex = 0, OrderInRow = 0, EpicTitle = "Epic 1" },
                new EpicPlacementDto { Id = 2, EpicId = 101, LaneId = 1, RowIndex = 0, OrderInRow = 1, EpicTitle = "Epic 2" }
            },
            MilestoneLines = [],
            IterationLines = [],
            Connectors = [],
            MaxRowIndex = 0,
            TotalRows = 1
        };
        var dragState = new DragState
        {
            DraggingEpicId = 100,
            DraggingPlacementId = 1,
            SourceObjectiveId = 10,
            HoverLaneId = 1,
            HoverRowIndex = 0,
            IsInsertingNewRow = false
        };

        // Act
        var result = BoardRenderModelFactory.Create(board, dragState);

        // Assert
        var row = result.Lanes[0].Rows[0];
        // Should have: Both epics (Epic 1 being dragged will be styled with .epic-being-dragged) + placeholder
        Assert.HasCount(3, row.Cards);
        Assert.IsTrue(row.Cards.Any(c => c.Placement?.EpicId == 100)); // Epic 1 (being dragged, but kept visible)
        Assert.IsTrue(row.Cards.Any(c => c.Placement?.EpicId == 101)); // Epic 2 remains
        Assert.IsTrue(row.Cards.Any(c => c.IsPlaceholder));
    }

    [TestMethod]
    public void Create_RowWithSingleEpicAndPlaceholder_HasCenterLayout()
    {
        // Arrange - Tests that placeholders are excluded from layout calculation
        var board = new ReleasePlanningBoardDto
        {
            Lanes = new[]
            {
                new LaneDto { Id = 1, ObjectiveId = 10, ObjectiveTitle = "Objective 1", DisplayOrder = 0 }
            },
            Placements = new[]
            {
                new EpicPlacementDto { Id = 1, EpicId = 100, LaneId = 1, RowIndex = 0, OrderInRow = 0, EpicTitle = "Epic 1" }
            },
            MilestoneLines = [],
            IterationLines = [],
            Connectors = [],
            MaxRowIndex = 0,
            TotalRows = 1
        };
        var dragState = new DragState
        {
            DraggingEpicId = 101,
            SourceObjectiveId = 10,
            HoverLaneId = 1,
            HoverRowIndex = 0,
            IsInsertingNewRow = false
        };

        // Act
        var result = BoardRenderModelFactory.Create(board, dragState);

        // Assert
        var row = result.Lanes[0].Rows[0];
        Assert.HasCount(2, row.Cards); // 1 epic + 1 placeholder
        Assert.IsTrue(row.Cards.Any(c => c.IsPlaceholder));
        // Layout should be Center because only 1 non-placeholder epic exists
        Assert.AreEqual(RowLayoutType.Center, row.LayoutType);
    }
}
