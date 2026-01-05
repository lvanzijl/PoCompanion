namespace PoTool.Client.Components.ReleasePlanning;

/// <summary>
/// Event args for when an Epic is dropped onto the board.
/// </summary>
public class EpicDropEventArgs
{
    /// <summary>
    /// The Epic TFS ID being dropped.
    /// </summary>
    public int EpicId { get; set; }

    /// <summary>
    /// The target Objective TFS ID (Lane).
    /// </summary>
    public int ObjectiveId { get; set; }

    /// <summary>
    /// The target row index.
    /// </summary>
    public int RowIndex { get; set; }
}

/// <summary>
/// Event args for when an Epic is moved within the board.
/// </summary>
public class EpicMoveEventArgs
{
    /// <summary>
    /// The placement ID being moved.
    /// </summary>
    public int PlacementId { get; set; }

    /// <summary>
    /// The new row index.
    /// </summary>
    public int NewRowIndex { get; set; }

    /// <summary>
    /// The new order within the row.
    /// </summary>
    public int NewOrderInRow { get; set; }
}

/// <summary>
/// Event args for Epic context menu.
/// </summary>
public class EpicContextMenuEventArgs
{
    /// <summary>
    /// The placement ID.
    /// </summary>
    public int PlacementId { get; set; }

    /// <summary>
    /// The Epic TFS ID.
    /// </summary>
    public int EpicId { get; set; }
}
