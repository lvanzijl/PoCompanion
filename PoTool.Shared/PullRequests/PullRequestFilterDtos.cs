using PoTool.Shared.Metrics;

namespace PoTool.Shared.PullRequests;

public sealed record PullRequestFilterContextDto
{
    public required FilterSelectionDto<int> ProductIds { get; init; }

    public required FilterSelectionDto<int> TeamIds { get; init; }

    public required FilterSelectionDto<string> RepositoryNames { get; init; }

    public required FilterSelectionDto<string> IterationPaths { get; init; }

    public required FilterSelectionDto<string> CreatedBys { get; init; }

    public required FilterSelectionDto<string> Statuses { get; init; }

    public required FilterTimeSelectionDto Time { get; init; }
}

public sealed record PullRequestQueryResponseDto<T>
{
    public required T Data { get; init; }

    public required PullRequestFilterContextDto RequestedFilter { get; init; }

    public required PullRequestFilterContextDto EffectiveFilter { get; init; }

    public required IReadOnlyList<string> InvalidFields { get; init; }

    public required IReadOnlyList<FilterValidationIssueDto> ValidationMessages { get; init; }

    public required IReadOnlyDictionary<int, string> TeamLabels { get; init; }

    public required IReadOnlyDictionary<int, string> SprintLabels { get; init; }
}
