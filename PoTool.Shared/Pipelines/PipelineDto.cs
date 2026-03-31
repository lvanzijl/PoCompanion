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
)
{
    public PipelineDto()
        : this(0, string.Empty, PipelineType.Build, null, default)
    {
    }
}

/// <summary>
/// Type of pipeline.
/// </summary>
public enum PipelineType
{
    Build,
    Release
}
