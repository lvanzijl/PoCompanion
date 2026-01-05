using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Entity representing an Epic's placement on the Release Planning Board.
/// </summary>
public class EpicPlacementEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The TFS ID of the Epic.
    /// </summary>
    [Required]
    public int EpicId { get; set; }

    /// <summary>
    /// Foreign key to the Lane.
    /// </summary>
    [Required]
    public int LaneId { get; set; }

    /// <summary>
    /// Navigation property to the Lane.
    /// </summary>
    [ForeignKey(nameof(LaneId))]
    public LaneEntity? Lane { get; set; }

    /// <summary>
    /// The row index (planning level). RowIndex increases top → bottom.
    /// </summary>
    [Required]
    public int RowIndex { get; set; }

    /// <summary>
    /// Order within the row (left to right for parallel Epics).
    /// </summary>
    [Required]
    public int OrderInRow { get; set; }
}
