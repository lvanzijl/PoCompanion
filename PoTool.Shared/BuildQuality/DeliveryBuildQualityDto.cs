namespace PoTool.Shared.BuildQuality;

/// <summary>
/// Sprint-window BuildQuality contract for Delivery consumers.
/// </summary>
public sealed class DeliveryBuildQualityDto
{
    public int ProductOwnerId { get; set; }

    public int SprintId { get; set; }

    public string SprintName { get; set; } = string.Empty;

    public int? TeamId { get; set; }

    public DateTimeOffset? SprintStartUtc { get; set; }

    public DateTimeOffset? SprintEndUtc { get; set; }

    public IReadOnlyList<int> ProductIds { get; set; } = Array.Empty<int>();

    public IReadOnlyList<string> DefaultBranches { get; set; } = Array.Empty<string>();

    public BuildQualityResultDto Summary { get; set; } = new();

    public IReadOnlyList<BuildQualityProductDto> Products { get; set; } = Array.Empty<BuildQualityProductDto>();
}
