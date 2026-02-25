using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for pull request comment persistence.
/// </summary>
public class PullRequestCommentEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int InternalId { get; set; }

    /// <summary>
    /// Comment ID from TFS/Azure DevOps.
    /// </summary>
    [Required]
    public int Id { get; set; }

    /// <summary>
    /// Pull request ID (foreign key).
    /// </summary>
    [Required]
    public int PullRequestId { get; set; }

    /// <summary>
    /// Thread ID for grouping related comments.
    /// </summary>
    [Required]
    public int ThreadId { get; set; }

    /// <summary>
    /// Comment author username.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Comment content.
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Date when comment was created.
    /// </summary>
    [Required]
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// UTC created date used in SQLite-translatable predicates/sorting.
    /// </summary>
    [Required]
    public DateTime CreatedDateUtc { get; set; }

    /// <summary>
    /// Date when comment was last updated (nullable).
    /// </summary>
    public DateTimeOffset? UpdatedDate { get; set; }

    /// <summary>
    /// Whether the comment thread is resolved.
    /// </summary>
    [Required]
    public bool IsResolved { get; set; }

    /// <summary>
    /// Date when comment was resolved (nullable).
    /// </summary>
    public DateTimeOffset? ResolvedDate { get; set; }

    /// <summary>
    /// Username of who resolved the comment (nullable).
    /// </summary>
    [MaxLength(200)]
    public string? ResolvedBy { get; set; }
}
