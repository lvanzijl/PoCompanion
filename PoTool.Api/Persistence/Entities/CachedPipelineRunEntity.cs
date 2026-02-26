using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for caching pipeline runs per ProductOwner.
/// </summary>
public class CachedPipelineRunEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to ProfileEntity (ProductOwner).
    /// </summary>
    [Required]
    public int ProductOwnerId { get; set; }

    /// <summary>
    /// Foreign key to PipelineDefinitionEntity.
    /// </summary>
    [Required]
    public int PipelineDefinitionId { get; set; }

    /// <summary>
    /// TFS pipeline run ID (unique within pipeline definition).
    /// </summary>
    [Required]
    public int TfsRunId { get; set; }

    /// <summary>
    /// Run name/build number.
    /// </summary>
    [MaxLength(200)]
    public string? RunName { get; set; }

    /// <summary>
    /// Run state (e.g., "completed", "inProgress").
    /// </summary>
    [MaxLength(50)]
    public string? State { get; set; }

    /// <summary>
    /// Run result (e.g., "succeeded", "failed", "canceled").
    /// </summary>
    [MaxLength(50)]
    public string? Result { get; set; }

    /// <summary>
    /// Timestamp when the run was created.
    /// </summary>
    public DateTimeOffset? CreatedDate { get; set; }

    /// <summary>
    /// Timestamp when the run finished.
    /// </summary>
    public DateTimeOffset? FinishedDate { get; set; }

    /// <summary>
    /// UTC finished date used in SQLite-translatable predicates/sorting.
    /// </summary>
    public DateTime? FinishedDateUtc { get; set; }

    /// <summary>
    /// Source branch for the run.
    /// </summary>
    [MaxLength(500)]
    public string? SourceBranch { get; set; }

    /// <summary>
    /// Source version (commit SHA).
    /// </summary>
    [MaxLength(50)]
    public string? SourceVersion { get; set; }

    /// <summary>
    /// URL to the pipeline run in TFS/Azure DevOps.
    /// </summary>
    [MaxLength(1000)]
    public string? Url { get; set; }

    /// <summary>
    /// Timestamp when this record was cached.
    /// </summary>
    [Required]
    public DateTimeOffset CachedAt { get; set; }

    /// <summary>
    /// Navigation property to ProductOwner.
    /// </summary>
    public virtual ProfileEntity ProductOwner { get; set; } = null!;

    /// <summary>
    /// Navigation property to PipelineDefinition.
    /// </summary>
    public virtual PipelineDefinitionEntity PipelineDefinition { get; set; } = null!;
}
