using PoTool.Core.Filters;

namespace PoTool.Core.Pipelines.Filters;

public sealed record PipelineFilterContext(
    FilterSelection<int> ProductIds,
    FilterSelection<int> TeamIds,
    FilterSelection<string> RepositoryNames,
    FilterTimeSelection Time)
{
    public static PipelineFilterContext Empty() => new(
        FilterSelection<int>.All(),
        FilterSelection<int>.All(),
        FilterSelection<string>.All(),
        FilterTimeSelection.None());
}

public sealed record PipelineBranchScope(
    int PipelineId,
    string? DefaultBranch);

public sealed record PipelineEffectiveFilter(
    PipelineFilterContext Context,
    IReadOnlyList<string> RepositoryScope,
    IReadOnlyList<int> PipelineIds,
    IReadOnlyList<PipelineBranchScope> BranchScope,
    DateTimeOffset? RangeStartUtc,
    DateTimeOffset? RangeEndUtc,
    int? SprintId);
