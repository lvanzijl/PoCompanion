namespace PoTool.Shared.Planning;

/// <summary>
/// Represents the scope of the planning board view.
/// </summary>
public enum BoardScope
{
    /// <summary>
    /// Show all visible products as columns.
    /// </summary>
    AllProducts = 0,
    
    /// <summary>
    /// Show only a single selected product.
    /// </summary>
    SingleProduct = 1
}

/// <summary>
/// Type of marker row.
/// </summary>
public enum MarkerRowType
{
    /// <summary>
    /// Iteration line marker.
    /// </summary>
    Iteration = 0,
    
    /// <summary>
    /// Release line marker (visually dominant over iteration).
    /// </summary>
    Release = 1
}

/// <summary>
/// Represents the type of a board row.
/// </summary>
public enum BoardRowType
{
    /// <summary>
    /// Normal row for epic placements.
    /// </summary>
    Normal = 0,
    
    /// <summary>
    /// Marker row (iteration or release line).
    /// </summary>
    Marker = 1
}

/// <summary>
/// Represents a product column on the planning board.
/// </summary>
public sealed record ProductColumnDto
{
    /// <summary>
    /// The product ID.
    /// </summary>
    public int ProductId { get; init; }
    
    /// <summary>
    /// The product name.
    /// </summary>
    public string ProductName { get; init; } = string.Empty;
    
    /// <summary>
    /// Whether this product column is visible.
    /// </summary>
    public bool IsVisible { get; init; } = true;
    
    /// <summary>
    /// Display order of the column.
    /// </summary>
    public int DisplayOrder { get; init; }
}

/// <summary>
/// Represents a row on the planning board.
/// Rows are abstract ordered slots with no calendar/date meaning.
/// </summary>
public sealed record BoardRowDto
{
    /// <summary>
    /// Unique identifier for the row.
    /// </summary>
    public int Id { get; init; }
    
    /// <summary>
    /// Display order of the row (0-based, top to bottom).
    /// </summary>
    public int DisplayOrder { get; init; }
    
    /// <summary>
    /// Type of the row (Normal or Marker).
    /// </summary>
    public BoardRowType RowType { get; init; } = BoardRowType.Normal;
    
    /// <summary>
    /// For marker rows: the type of marker (Iteration or Release).
    /// </summary>
    public MarkerRowType? MarkerType { get; init; }
    
    /// <summary>
    /// For marker rows: the label to display.
    /// </summary>
    public string? MarkerLabel { get; init; }
}

/// <summary>
/// Represents an Epic's placement on the planning board.
/// An Epic can only appear in the column matching its Product.
/// </summary>
public sealed record EpicPlacementDto
{
    /// <summary>
    /// Unique identifier for this placement.
    /// </summary>
    public int Id { get; init; }
    
    /// <summary>
    /// The TFS ID of the Epic work item.
    /// </summary>
    public int EpicId { get; init; }
    
    /// <summary>
    /// Title of the Epic.
    /// </summary>
    public string EpicTitle { get; init; } = string.Empty;
    
    /// <summary>
    /// The Product this Epic belongs to (determines column).
    /// </summary>
    public int ProductId { get; init; }
    
    /// <summary>
    /// The row ID where this Epic is placed.
    /// </summary>
    public int RowId { get; init; }
    
    /// <summary>
    /// Order within the cell (for multiple epics in same cell).
    /// </summary>
    public int OrderInCell { get; init; }
    
    /// <summary>
    /// Whether this placement is currently selected.
    /// </summary>
    public bool IsSelected { get; init; }
    
    /// <summary>
    /// Effort estimate for the Epic.
    /// </summary>
    public int? Effort { get; init; }
    
    /// <summary>
    /// Current state of the Epic.
    /// </summary>
    public string State { get; init; } = string.Empty;
}

/// <summary>
/// Represents an Epic that is not placed on the board.
/// </summary>
public sealed record UnplannedEpicDto
{
    /// <summary>
    /// The TFS ID of the Epic work item.
    /// </summary>
    public int EpicId { get; init; }
    
    /// <summary>
    /// Title of the Epic.
    /// </summary>
    public string Title { get; init; } = string.Empty;
    
    /// <summary>
    /// The Product this Epic belongs to.
    /// </summary>
    public int ProductId { get; init; }
    
    /// <summary>
    /// Name of the Product this Epic belongs to.
    /// </summary>
    public string ProductName { get; init; } = string.Empty;
    
    /// <summary>
    /// Effort estimate for the Epic.
    /// </summary>
    public int? Effort { get; init; }
    
    /// <summary>
    /// Current state of the Epic.
    /// </summary>
    public string State { get; init; } = string.Empty;
}

/// <summary>
/// Complete planning board state.
/// </summary>
public sealed record PlanningBoardDto
{
    /// <summary>
    /// Current scope of the board view.
    /// </summary>
    public BoardScope Scope { get; init; } = BoardScope.AllProducts;
    
    /// <summary>
    /// When scope is SingleProduct, which product is selected.
    /// </summary>
    public int? SelectedProductId { get; init; }
    
    /// <summary>
    /// All product columns (includes hidden ones for state management).
    /// </summary>
    public IReadOnlyList<ProductColumnDto> ProductColumns { get; init; } = [];
    
    /// <summary>
    /// All board rows in display order.
    /// </summary>
    public IReadOnlyList<BoardRowDto> Rows { get; init; } = [];
    
    /// <summary>
    /// All epic placements on the board.
    /// </summary>
    public IReadOnlyList<EpicPlacementDto> Placements { get; init; } = [];
    
    /// <summary>
    /// Number of hidden product columns.
    /// </summary>
    public int HiddenColumnCount { get; init; }
}

/// <summary>
/// Request to create a new row.
/// </summary>
public sealed record CreateRowRequest
{
    /// <summary>
    /// Insert before this row index. If null, insert at end.
    /// </summary>
    public int? InsertBeforeRowId { get; init; }
    
    /// <summary>
    /// If true, insert below the reference row instead of above.
    /// </summary>
    public bool InsertBelow { get; init; }
}

/// <summary>
/// Request to create a marker row.
/// </summary>
public sealed record CreateMarkerRowRequest
{
    /// <summary>
    /// Insert before this row index. If null, insert at end.
    /// </summary>
    public int? InsertBeforeRowId { get; init; }
    
    /// <summary>
    /// If true, insert below the reference row instead of above.
    /// </summary>
    public bool InsertBelow { get; init; }
    
    /// <summary>
    /// Type of marker (Iteration or Release).
    /// </summary>
    public MarkerRowType MarkerType { get; init; }
    
    /// <summary>
    /// Label for the marker row.
    /// </summary>
    public string Label { get; init; } = string.Empty;
}

/// <summary>
/// Request to place an epic on the board.
/// </summary>
public sealed record CreateEpicPlacementRequest
{
    /// <summary>
    /// Epic TFS ID to place.
    /// </summary>
    public int EpicId { get; init; }
    
    /// <summary>
    /// Row to place the epic in.
    /// </summary>
    public int RowId { get; init; }
    
    /// <summary>
    /// Order within the cell.
    /// </summary>
    public int OrderInCell { get; init; }
}

/// <summary>
/// Request to move an epic to a different row on the new Planning Board.
/// </summary>
public sealed record MovePlanningEpicRequest
{
    /// <summary>
    /// New row ID.
    /// </summary>
    public int NewRowId { get; init; }
    
    /// <summary>
    /// New order within the cell.
    /// </summary>
    public int NewOrderInCell { get; init; }
}

/// <summary>
/// Request to update board scope.
/// </summary>
public sealed record UpdateBoardScopeRequest
{
    /// <summary>
    /// New scope.
    /// </summary>
    public BoardScope Scope { get; init; }
    
    /// <summary>
    /// When scope is SingleProduct, which product.
    /// </summary>
    public int? SelectedProductId { get; init; }
}

/// <summary>
/// Request to update product column visibility.
/// </summary>
public sealed record UpdateProductVisibilityRequest
{
    /// <summary>
    /// Product ID to update.
    /// </summary>
    public int ProductId { get; init; }
    
    /// <summary>
    /// Whether the product column should be visible.
    /// </summary>
    public bool IsVisible { get; init; }
}

/// <summary>
/// Result of a board operation.
/// </summary>
public sealed record BoardOperationResultDto
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of a row operation.
/// </summary>
public sealed record RowOperationResultDto
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// The created row ID, if applicable.
    /// </summary>
    public int? RowId { get; init; }
}

/// <summary>
/// Result of an epic placement operation.
/// </summary>
public sealed record PlacementOperationResultDto
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// The created placement ID, if applicable.
    /// </summary>
    public int? PlacementId { get; init; }
}
