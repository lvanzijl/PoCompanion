using PoTool.Core.Pipelines.Filters;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Services;

internal static class PipelineFiltering
{
    public static IReadOnlyList<PipelineRunDto> ApplyRunScope(
        IEnumerable<PipelineRunDto> runs,
        PipelineEffectiveFilter filter)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.PipelineIds.Count == 0 || filter.RepositoryScope.Count == 0)
        {
            return Array.Empty<PipelineRunDto>();
        }

        var pipelineIds = filter.PipelineIds.ToHashSet();
        var branchScope = filter.BranchScope.ToDictionary(scope => scope.PipelineId, scope => scope.DefaultBranch);

        IEnumerable<PipelineRunDto> filtered = runs.Where(run => pipelineIds.Contains(run.PipelineId));

        if (filter.RangeStartUtc.HasValue)
        {
            filtered = filtered.Where(run => run.StartTime.HasValue && run.StartTime.Value >= filter.RangeStartUtc.Value);
        }

        if (filter.RangeEndUtc.HasValue)
        {
            filtered = filtered.Where(run => run.StartTime.HasValue && run.StartTime.Value <= filter.RangeEndUtc.Value);
        }

        filtered = filtered.Where(run =>
        {
            if (!branchScope.TryGetValue(run.PipelineId, out var defaultBranch) || string.IsNullOrWhiteSpace(defaultBranch))
            {
                return true;
            }

            return string.Equals(run.Branch, defaultBranch, StringComparison.OrdinalIgnoreCase);
        });

        return filtered.ToList();
    }
}
