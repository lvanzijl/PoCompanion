using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// EF Core entity for persisting work items in SQLite.
/// </summary>
public sealed class WorkItemEntity
{
    /// <summary>
    /// Primary key for the entity.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The TFS/Azure DevOps work item ID (indexed for lookups).
    /// </summary>
    [Required]
    public int TfsId { get; set; }

    /// <summary>
    /// The type of work item (e.g., Epic, Feature, Product Backlog Item).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The title of the work item.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The area path for organizational hierarchy.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string AreaPath { get; set; } = string.Empty;

    /// <summary>
    /// The iteration path for sprint/release planning.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string IterationPath { get; set; } = string.Empty;

    /// <summary>
    /// The current state of the work item.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Complete JSON payload from TFS.
    /// </summary>
    [Required]
    public string JsonPayload { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this work item was retrieved from TFS.
    /// </summary>
    [Required]
    public DateTimeOffset RetrievedAt { get; set; }
}
