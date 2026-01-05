using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Entity representing a Lane (Objective) on the Release Planning Board.
/// </summary>
public class LaneEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The TFS ID of the Objective this Lane represents.
    /// </summary>
    [Required]
    public int ObjectiveId { get; set; }

    /// <summary>
    /// Display order of the Lane (left to right).
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Collection of Epic placements in this Lane.
    /// </summary>
    public ICollection<EpicPlacementEntity> Placements { get; set; } = [];
}
