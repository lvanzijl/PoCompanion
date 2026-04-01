namespace PoTool.Shared.BuildQuality;

/// <summary>
/// Pipeline- or repository-scoped BuildQuality detail contract.
/// </summary>
public sealed class PipelineBuildQualityDto
{
    public int ProductOwnerId { get; set; }

    public int SprintId { get; set; }

    public string SprintName { get; set; } = string.Empty;

    public int? TeamId { get; set; }

    public DateTimeOffset? SprintStartUtc { get; set; }

    public DateTimeOffset? SprintEndUtc { get; set; }

    public int? ProductId { get; set; }

    public int? RepositoryId { get; set; }

    public string? RepositoryName { get; set; }

    public int? PipelineDefinitionId { get; set; }

    public string? PipelineName { get; set; }

    public IReadOnlyList<string> DefaultBranches { get; set; } = Array.Empty<string>();

    public BuildQualityResultDto Result { get; set; } = new();
}
