using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for pull request iteration persistence.
/// </summary>
public class PullRequestIterationEntity
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
    /// Iteration number.
    /// </summary>
    [Required]
    public int IterationNumber { get; set; }

    /// <summary>
    /// Date when iteration was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Date when iteration was last updated.
    /// </summary>
    [Required]
    public DateTimeOffset UpdatedDate { get; set; }

    /// <summary>
    /// Number of commits in this iteration.
    /// </summary>
    [Required]
    public int CommitCount { get; set; }

    /// <summary>
    /// Number of changes in this iteration.
    /// </summary>
    [Required]
    public int ChangeCount { get; set; }
}
