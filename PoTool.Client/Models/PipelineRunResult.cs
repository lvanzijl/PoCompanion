namespace PoTool.Client.Models;

/// <summary>
/// Result of a pipeline run.
/// Client-side copy that matches PoTool.Core.Pipelines.PipelineRunResult.
/// </summary>
public enum PipelineRunResult
{
    Unknown = 0,
    Succeeded = 1,
    Failed = 2,
    PartiallySucceeded = 3,
    Canceled = 4,
    None = 5
}
