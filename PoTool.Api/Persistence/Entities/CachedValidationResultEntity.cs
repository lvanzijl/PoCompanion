using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Entity for caching validation results for Epics on the Release Planning Board.
/// </summary>
public class CachedValidationResultEntity
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
    /// Validation indicator (0=None, 1=Warning, 2=Error).
    /// </summary>
    [Required]
    public int Indicator { get; set; }

    /// <summary>
    /// When the validation was last updated.
    /// </summary>
    [Required]
    public DateTimeOffset LastUpdated { get; set; }
}
