namespace PoTool.Shared.Pipelines;

/// <summary>
/// Result of a pipeline run.
/// </summary>
public enum PipelineRunResult
{
    Succeeded = 0,
    Failed = 1,
    Canceled = 2,
    PartiallySucceeded = 3,
    Unknown = 4
}
