namespace PoTool.Shared.Settings;

/// <summary>
/// Immutable DTO for a Sprint (Iteration).
/// A Sprint represents a TFS team iteration with name, dates, and timeframe.
/// </summary>
public sealed record SprintDto(
    int Id,
    int TeamId,
    string? TfsIterationId,
    string Path,
    string Name,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string? TimeFrame,
    DateTimeOffset LastSyncedUtc
)
{
    public SprintDto()
        : this(0, 0, null, string.Empty, string.Empty, null, null, null, default)
    {
    }
}
