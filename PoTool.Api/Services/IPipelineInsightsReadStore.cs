using PoTool.Core.Pipelines.Filters;

namespace PoTool.Api.Services;

/// <summary>
/// Cache-backed analytical Pipeline Insights read store.
/// Owns Pipeline Insights scope selection and raw run fact loading.
/// </summary>
public interface IPipelineInsightsReadStore
{
    Task<PipelineInsightsSprintWindow?> GetSprintWindowAsync(
        int sprintId,
        CancellationToken cancellationToken);

    Task<PipelineInsightsSprintWindow?> GetPreviousSprintWindowAsync(
        int teamId,
        DateTime sprintStartUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PipelineInsightsProductSelection>> GetProductsAsync(
        PipelineEffectiveFilter filter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PipelineInsightsDefinitionSelection>> GetPipelineDefinitionsAsync(
        IReadOnlyList<int> productIds,
        IReadOnlyCollection<string> repositories,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PipelineInsightsRun>> GetRunsAsync(
        IReadOnlyList<PipelineInsightsDefinitionSelection> pipelineDefinitions,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        CancellationToken cancellationToken);
}

public sealed record PipelineInsightsSprintWindow(
    int Id,
    string Name,
    int TeamId,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc);

public sealed record PipelineInsightsProductSelection(
    int Id,
    string Name);

public sealed record PipelineInsightsDefinitionSelection(
    int Id,
    int ExternalPipelineDefinitionId,
    int ProductId,
    string Name,
    string? DefaultBranch);

public sealed record PipelineInsightsRun(
    int DbId,
    int TfsRunId,
    int DefId,
    string? Result,
    string? RunName,
    DateTime? CreatedDateUtc,
    DateTime? FinishedDateUtc,
    DateTimeOffset? CreatedDateOffset,
    DateTimeOffset? FinishedDateOffset,
    string? SourceBranch,
    string? Url);
