using PoTool.Shared.Metrics;

namespace PoTool.Shared.Pipelines;

public sealed record PipelineFilterContextDto
{
    public required FilterSelectionDto<int> ProductIds { get; init; }

    public required FilterSelectionDto<int> TeamIds { get; init; }

    public required FilterSelectionDto<int> RepositoryIds { get; init; }

    public required FilterTimeSelectionDto Time { get; init; }
}

public sealed record PipelineQueryResponseDto<T>
{
    public required T Data { get; init; }

    public required PipelineFilterContextDto RequestedFilter { get; init; }

    public required PipelineFilterContextDto EffectiveFilter { get; init; }

    public required IReadOnlyList<string> InvalidFields { get; init; }

    public required IReadOnlyList<FilterValidationIssueDto> ValidationMessages { get; init; }

    public required IReadOnlyDictionary<int, string> TeamLabels { get; init; }

    public required IReadOnlyDictionary<int, string> SprintLabels { get; init; }
}
