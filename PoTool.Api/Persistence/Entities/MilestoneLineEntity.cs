using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Entity representing a Milestone Line on the Release Planning Board.
/// </summary>
public class MilestoneLineEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Label for the Milestone Line.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Vertical position (row index where the line appears).
    /// </summary>
    [Required]
    public double VerticalPosition { get; set; }

    /// <summary>
    /// The type of milestone (0=Release, 1=Deadline, 2=Custom).
    /// </summary>
    [Required]
    public int Type { get; set; }
}
