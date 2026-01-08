namespace PoTool.Shared.Pipelines;

/// <summary>
/// Trigger type for a pipeline run.
/// </summary>
public enum PipelineRunTrigger
{
    Unknown = 0,
    Manual = 1,
    ContinuousIntegration = 2,
    Schedule = 3,
    PullRequest = 4,
    BuildCompletion = 5,
    ResourceTrigger = 6
}
