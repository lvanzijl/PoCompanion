namespace PoTool.Shared.Pipelines;

/// <summary>
/// Represents a YAML pipeline definition from TFS/Azure DevOps.
/// Contains metadata about the pipeline and its location in the repository.
/// </summary>
public record PipelineDefinitionDto
{
    /// <summary>
    /// TFS pipeline definition ID.
    /// </summary>
    public required int PipelineDefinitionId { get; init; }

    /// <summary>
    /// Product ID (from local database).
    /// </summary>
    public int? ProductId { get; init; }

    /// <summary>
    /// Repository ID (from local database).
    /// </summary>
    public int? RepositoryId { get; init; }

    /// <summary>
    /// TFS repository ID (GUID).
    /// </summary>
    public required string RepoId { get; init; }

    /// <summary>
    /// Repository name.
    /// </summary>
    public required string RepoName { get; init; }

    /// <summary>
    /// Pipeline definition name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// YAML file path in the repository (normalized with leading /).
    /// Null if not a YAML pipeline.
    /// </summary>
    public string? YamlPath { get; init; }

    /// <summary>
    /// Pipeline folder/path in TFS.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>
    /// Web URL to the pipeline definition.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Timestamp when this definition was last synced.
    /// </summary>
    public required DateTimeOffset LastSyncedUtc { get; init; }
}
