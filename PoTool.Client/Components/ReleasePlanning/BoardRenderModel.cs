namespace PoTool.Client.Components.ReleasePlanning;

using PoTool.Shared.ReleasePlanning;

/// <summary>
/// Represents drag state information used to compute preview.
/// </summary>
public sealed class DragState
{
    public int? DraggingEpicId { get; init; }
    public int? DraggingPlacementId { get; init; }
    public int? SourceObjectiveId { get; init; }
    public int? HoverLaneId { get; init; }
    public int? HoverRowIndex { get; init; }
    public bool IsInsertingNewRow { get; init; }
    
    public static DragState Empty => new();
    
    public bool IsDragging => DraggingEpicId.HasValue;
}

/// <summary>
/// Represents an epic card or placeholder in the render model.
/// </summary>
public sealed class RenderCard
{
    public EpicPlacementDto? Placement { get; init; }
    public bool IsPlaceholder { get; init; }
    public int? DraggingEpicId { get; init; }
    public string PlaceholderText { get; init; } = "Drop here";
}

/// <summary>
/// Represents a row in the render model with layout information.
/// </summary>
public sealed class RenderRow
{
    public int RowIndex { get; init; }
    public IReadOnlyList<RenderCard> Cards { get; init; } = [];
    public bool IsInsertedPreview { get; init; }
    
    /// <summary>
    /// Gets the layout type for this row based on card count.
    /// </summary>
    public RowLayoutType LayoutType => Cards.Count == 1 ? RowLayoutType.Center : RowLayoutType.Justify;
}

/// <summary>
/// Layout type for rows.
/// </summary>
public enum RowLayoutType
{
    Center,   // 1 epic: centered
    Justify   // 2+ epics: justified across width
}

/// <summary>
/// Represents a lane in the render model.
/// </summary>
public sealed class RenderLane
{
    public LaneDto Lane { get; init; } = null!;
    public IReadOnlyList<RenderRow> Rows { get; init; } = [];
}

/// <summary>
/// Complete render model for the board.
/// </summary>
public sealed class BoardRenderModel
{
    public IReadOnlyList<RenderLane> Lanes { get; init; } = [];
    public int MaxRowIndex { get; init; }
}

/// <summary>
/// Factory for creating board render models from persisted state and drag state.
/// </summary>
public static class BoardRenderModelFactory
{
    /// <summary>
    /// Creates a render model from persisted board state and current drag state.
    /// This is a pure function that derives the preview from inputs.
    /// </summary>
    public static BoardRenderModel Create(
        ReleasePlanningBoardDto board,
        DragState dragState)
    {
        var lanes = new List<RenderLane>();
        
        foreach (var lane in board.Lanes)
        {
            lanes.Add(CreateRenderLane(lane, board.Placements, dragState));
        }
        
        // Calculate max row index from render model
        var maxRowIndex = lanes
            .SelectMany(l => l.Rows)
            .Select(r => r.RowIndex)
            .DefaultIfEmpty(0)
            .Max();
        
        return new BoardRenderModel
        {
            Lanes = lanes,
            MaxRowIndex = maxRowIndex
        };
    }
    
    private static RenderLane CreateRenderLane(
        LaneDto lane,
        IReadOnlyList<EpicPlacementDto> allPlacements,
        DragState dragState)
    {
        // Get all placements for this lane
        var lanePlacements = allPlacements
            .Where(p => p.LaneId == lane.Id)
            .ToList();
        
        // Group by row
        var rowGroups = lanePlacements
            .GroupBy(p => p.RowIndex)
            .OrderBy(g => g.Key)
            .ToList();
        
        // Build render rows
        var renderRows = new List<RenderRow>();
        
        // Track if we're previewing a drop in this lane
        bool isPreviewingDropInThisLane = dragState.IsDragging 
            && dragState.HoverLaneId == lane.Id 
            && dragState.HoverRowIndex.HasValue;
        
        // If previewing, we need to handle row insertion/modification
        if (isPreviewingDropInThisLane && dragState.IsInsertingNewRow)
        {
            // Inserting a new row at HoverRowIndex
            int insertRowIndex = dragState.HoverRowIndex!.Value;
            
            // Add rows before insertion point
            foreach (var rowGroup in rowGroups.Where(g => g.Key < insertRowIndex))
            {
                renderRows.Add(CreateRenderRow(rowGroup.Key, rowGroup.ToList(), dragState, false));
            }
            
            // Add inserted preview row with placeholder
            renderRows.Add(CreateInsertedPreviewRow(insertRowIndex, dragState));
            
            // Add rows after insertion point (pushed down by 1 in visual preview)
            foreach (var rowGroup in rowGroups.Where(g => g.Key >= insertRowIndex))
            {
                // These rows are visually pushed down but keep their indices for now
                renderRows.Add(CreateRenderRow(rowGroup.Key + 1, rowGroup.ToList(), dragState, false));
            }
        }
        else if (isPreviewingDropInThisLane && !dragState.IsInsertingNewRow)
        {
            // Dropping into an existing row
            int targetRowIndex = dragState.HoverRowIndex!.Value;
            
            foreach (var rowGroup in rowGroups)
            {
                if (rowGroup.Key == targetRowIndex)
                {
                    // This is the target row - add placeholder
                    renderRows.Add(CreateRenderRowWithPlaceholder(rowGroup.Key, rowGroup.ToList(), dragState));
                }
                else
                {
                    renderRows.Add(CreateRenderRow(rowGroup.Key, rowGroup.ToList(), dragState, false));
                }
            }
            
            // If target row doesn't exist yet, create it
            if (!rowGroups.Any(g => g.Key == targetRowIndex))
            {
                renderRows.Add(CreateInsertedPreviewRow(targetRowIndex, dragState));
                renderRows = renderRows.OrderBy(r => r.RowIndex).ToList();
            }
        }
        else
        {
            // No preview - normal rendering
            foreach (var rowGroup in rowGroups)
            {
                renderRows.Add(CreateRenderRow(rowGroup.Key, rowGroup.ToList(), dragState, false));
            }
        }
        
        return new RenderLane
        {
            Lane = lane,
            Rows = renderRows
        };
    }
    
    private static RenderRow CreateRenderRow(
        int rowIndex,
        List<EpicPlacementDto> placements,
        DragState dragState,
        bool isInsertedPreview)
    {
        var cards = placements
            .OrderBy(p => p.OrderInRow)
            .Where(p => !dragState.DraggingPlacementId.HasValue || p.Id != dragState.DraggingPlacementId.Value)
            .Select(p => new RenderCard { Placement = p })
            .ToList();
        
        return new RenderRow
        {
            RowIndex = rowIndex,
            Cards = cards,
            IsInsertedPreview = isInsertedPreview
        };
    }
    
    private static RenderRow CreateRenderRowWithPlaceholder(
        int rowIndex,
        List<EpicPlacementDto> placements,
        DragState dragState)
    {
        var cards = placements
            .OrderBy(p => p.OrderInRow)
            .Where(p => !dragState.DraggingPlacementId.HasValue || p.Id != dragState.DraggingPlacementId.Value)
            .Select(p => new RenderCard { Placement = p })
            .ToList();
        
        // Add placeholder at the end
        cards.Add(new RenderCard
        {
            IsPlaceholder = true,
            DraggingEpicId = dragState.DraggingEpicId,
            PlaceholderText = "Drop here"
        });
        
        return new RenderRow
        {
            RowIndex = rowIndex,
            Cards = cards,
            IsInsertedPreview = false
        };
    }
    
    private static RenderRow CreateInsertedPreviewRow(int rowIndex, DragState dragState)
    {
        return new RenderRow
        {
            RowIndex = rowIndex,
            Cards = [new RenderCard
            {
                IsPlaceholder = true,
                DraggingEpicId = dragState.DraggingEpicId,
                PlaceholderText = "Drop here"
            }],
            IsInsertedPreview = true
        };
    }
}
