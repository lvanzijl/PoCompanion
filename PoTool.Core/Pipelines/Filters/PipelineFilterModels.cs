using PoTool.Core.Filters;

namespace PoTool.Core.Pipelines.Filters;

public sealed record PipelineFilterContext(
    FilterSelection<int> ProductIds,
    FilterSelection<int> TeamIds,
    FilterSelection<int> RepositoryIds,
    FilterTimeSelection Time)
{
    public static PipelineFilterContext Empty() => new(
        FilterSelection<int>.All(),
        FilterSelection<int>.All(),
        FilterSelection<int>.All(),
        FilterTimeSelection.None());
}

public sealed record PipelineBranchScope(
    int PipelineId,
    string? DefaultBranch);

public sealed record PipelineEffectiveFilter(
    PipelineFilterContext Context,
    IReadOnlyList<int> RepositoryScope,
    IReadOnlyList<int> PipelineIds,
    IReadOnlyList<PipelineBranchScope> BranchScope,
    DateTimeOffset? RangeStartUtc,
    DateTimeOffset? RangeEndUtc,
    int? SprintId);
