using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Entity representing an Iteration Line on the Release Planning Board.
/// </summary>
public class IterationLineEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Label for the Iteration Line.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Vertical position (row index where the line appears).
    /// </summary>
    [Required]
    public double VerticalPosition { get; set; }
}
