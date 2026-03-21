namespace PoTool.Shared.Pipelines;

/// <summary>
/// Raw coverage facts linked to a TFS build.
/// </summary>
public record CoverageDto
{
    /// <summary>
    /// TFS build identifier used for linkage to the cached build anchor.
    /// </summary>
    public required int BuildId { get; init; }

    /// <summary>
    /// Covered lines reported by the source coverage payload.
    /// </summary>
    public required int CoveredLines { get; init; }

    /// <summary>
    /// Total lines reported by the source coverage payload.
    /// </summary>
    public required int TotalLines { get; init; }

    /// <summary>
    /// Optional source timestamp when the upstream payload exposes a verified coverage timestamp.
    /// </summary>
    public DateTimeOffset? Timestamp { get; init; }
}
