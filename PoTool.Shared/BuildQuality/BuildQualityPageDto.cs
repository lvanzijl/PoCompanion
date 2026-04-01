namespace PoTool.Shared.BuildQuality;

/// <summary>
/// Rolling-window BuildQuality page contract.
/// </summary>
public sealed class BuildQualityPageDto
{
    public int ProductOwnerId { get; set; }

    public DateTimeOffset WindowStartUtc { get; set; }

    public DateTimeOffset WindowEndUtc { get; set; }

    public IReadOnlyList<int> ProductIds { get; set; } = Array.Empty<int>();

    public IReadOnlyList<string> DefaultBranches { get; set; } = Array.Empty<string>();

    public BuildQualityResultDto Summary { get; set; } = new();

    public IReadOnlyList<BuildQualityProductDto> Products { get; set; } = Array.Empty<BuildQualityProductDto>();
}
