namespace PoTool.Shared.Pipelines;

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

/// <summary>
/// Result of a pipeline run.
/// </summary>
public enum PipelineRunResult
{
    Unknown,
    Succeeded,
    Failed,
    PartiallySucceeded,
    Canceled,
    None
}

/// <summary>
/// What triggered the pipeline run.
/// </summary>
public enum PipelineRunTrigger
{
    Unknown,
    Manual,
    ContinuousIntegration,
    Schedule,
    PullRequest,
    BuildCompletion,
    ResourceTrigger
}
