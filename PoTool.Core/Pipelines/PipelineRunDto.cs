using PoTool.Shared.Pipelines;

namespace PoTool.Core.Pipelines;

/// <summary>
/// Represents a single pipeline run/execution.
/// </summary>
public record PipelineRunDto(
    int RunId,
    int PipelineId,
    string PipelineName,
    DateTimeOffset? StartTime,
    DateTimeOffset? FinishTime,
    TimeSpan? Duration,
    PipelineRunResult Result,
    PipelineRunTrigger Trigger,
    string? TriggerInfo,
    string? Branch,
    string? RequestedFor,
    DateTimeOffset RetrievedAt
);
