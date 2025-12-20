using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for pull request persistence.
/// </summary>
public class PullRequestEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int InternalId { get; set; }

    /// <summary>
    /// TFS/Azure DevOps pull request ID.
    /// </summary>
    [Required]
    public int Id { get; set; }

    /// <summary>
    /// Repository name.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>
    /// Pull request title.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Created by (author username).
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date when PR was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Date when PR was completed (nullable if still active).
    /// </summary>
    public DateTimeOffset? CompletedDate { get; set; }

    /// <summary>
    /// PR status (active, completed, abandoned).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Iteration path.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string IterationPath { get; set; } = string.Empty;

    /// <summary>
    /// Source branch.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string SourceBranch { get; set; } = string.Empty;

    /// <summary>
    /// Target branch.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TargetBranch { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this PR was retrieved.
    /// </summary>
    [Required]
    public DateTimeOffset RetrievedAt { get; set; }
}
