namespace PoTool.Shared.Settings;

/// <summary>
/// DTO for a team iteration retrieved from TFS.
/// Maps to the TFS team iterations API response.
/// </summary>
public sealed record TeamIterationDto(
    string? Id,
    string Name,
    string Path,
    DateTimeOffset? StartDate,
    DateTimeOffset? FinishDate,
    string? TimeFrame
);
