namespace PoTool.Shared.BuildQuality;

/// <summary>
/// BuildQuality breakdown for a single product.
/// </summary>
public sealed class BuildQualityProductDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public IReadOnlyList<int> PipelineDefinitionIds { get; set; } = Array.Empty<int>();

    public IReadOnlyList<int> RepositoryIds { get; set; } = Array.Empty<int>();

    public BuildQualityResultDto Result { get; set; } = new();
}
