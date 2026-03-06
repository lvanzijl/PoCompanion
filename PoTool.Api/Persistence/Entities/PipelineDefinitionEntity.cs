using System.ComponentModel.DataAnnotations;

namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// EF Core entity for YAML Pipeline Definition persistence.
/// Represents a build pipeline definition retrieved from TFS/Azure DevOps.
/// </summary>
public class PipelineDefinitionEntity
{
    /// <summary>
    /// Internal database ID (primary key).
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// TFS pipeline definition ID (from TFS API).
    /// </summary>
    [Required]
    public int PipelineDefinitionId { get; set; }

    /// <summary>
    /// Foreign key to the Product.
    /// </summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>
    /// Foreign key to the Repository.
    /// </summary>
    [Required]
    public int RepositoryId { get; set; }

    /// <summary>
    /// TFS repository ID (GUID from TFS API).
    /// Standard GUID format is 36 characters (e.g., "12345678-1234-1234-1234-123456789012").
    /// </summary>
    [Required]
    [MaxLength(36)]
    public string RepoId { get; set; } = string.Empty;

    /// <summary>
    /// Repository name from TFS.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string RepoName { get; set; } = string.Empty;

    /// <summary>
    /// Pipeline definition name.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// YAML file path in the repository (e.g., /pipelines/build.yml).
    /// Null if not a YAML pipeline or path not available.
    /// </summary>
    [MaxLength(500)]
    public string? YamlPath { get; set; }

    /// <summary>
    /// Pipeline folder/path in TFS (often contains backslash separators).
    /// </summary>
    [MaxLength(500)]
    public string? Folder { get; set; }

    /// <summary>
    /// Web URL to the pipeline definition in TFS.
    /// </summary>
    [MaxLength(1000)]
    public string? Url { get; set; }

    /// <summary>
    /// Default branch of the repository this pipeline is defined in
    /// (e.g. "refs/heads/main"). Used to filter cached runs to the production branch.
    /// Null when not yet populated.
    /// </summary>
    [MaxLength(500)]
    public string? DefaultBranch { get; set; }

    /// <summary>
    /// Timestamp when this pipeline definition was last synced from TFS.
    /// </summary>
    [Required]
    public DateTimeOffset LastSyncedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Navigation property to the Product.
    /// </summary>
    public virtual ProductEntity Product { get; set; } = null!;

    /// <summary>
    /// Navigation property to the Repository.
    /// </summary>
    public virtual RepositoryEntity Repository { get; set; } = null!;
}
