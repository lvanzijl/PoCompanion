using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Type of board row.
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
/// Type of marker for marker rows.
/// </summary>
public enum MarkerType
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
/// Entity representing a row on the Planning Board.
/// Rows are abstract ordered slots with no calendar/date meaning.
/// </summary>
public class BoardRowEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Display order of the row (0-based, top to bottom).
    /// </summary>
    [Required]
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Type of the row (Normal or Marker).
    /// </summary>
    [Required]
    public BoardRowType RowType { get; set; } = BoardRowType.Normal;

    /// <summary>
    /// For marker rows: the type of marker (Iteration or Release).
    /// Null for normal rows.
    /// </summary>
    public MarkerType? MarkerRowType { get; set; }

    /// <summary>
    /// For marker rows: the label to display.
    /// </summary>
    [MaxLength(200)]
    public string? MarkerLabel { get; set; }

    /// <summary>
    /// Timestamp when this row was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this row was last modified.
    /// </summary>
    [Required]
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Collection of Epic placements in this row.
    /// </summary>
    public virtual ICollection<PlanningEpicPlacementEntity> Placements { get; set; } = new List<PlanningEpicPlacementEntity>();
}
