namespace PoTool.Shared.Pipelines;

/// <summary>
/// Represents a build or release pipeline definition.
/// </summary>
public record PipelineDto(
    int Id,
    string Name,
    PipelineType Type,
    string? Path,
    DateTimeOffset RetrievedAt
);

/// <summary>
/// Type of pipeline.
/// </summary>
public enum PipelineType
{
    Build,
    Release
}
