using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for pull request file change persistence.
/// </summary>
public class PullRequestFileChangeEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Pull request ID (foreign key).
    /// </summary>
    [Required]
    public int PullRequestId { get; set; }

    /// <summary>
    /// Iteration ID.
    /// </summary>
    [Required]
    public int IterationId { get; set; }

    /// <summary>
    /// File path.
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Change type (add, edit, delete, rename).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// Number of lines added.
    /// </summary>
    [Required]
    public int LinesAdded { get; set; }

    /// <summary>
    /// Number of lines deleted.
    /// </summary>
    [Required]
    public int LinesDeleted { get; set; }

    /// <summary>
    /// Number of lines modified.
    /// </summary>
    [Required]
    public int LinesModified { get; set; }
}
