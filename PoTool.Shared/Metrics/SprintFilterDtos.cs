namespace PoTool.Shared.Metrics;

public sealed record SprintFilterContextDto
{
    public required FilterSelectionDto<int> ProductIds { get; init; }

    public required FilterSelectionDto<int> TeamIds { get; init; }

    public required FilterSelectionDto<string> AreaPaths { get; init; }

    public required FilterSelectionDto<string> IterationPaths { get; init; }

    public required FilterTimeSelectionDto Time { get; init; }
}

public sealed record SprintQueryResponseDto<T>
{
    public required T Data { get; init; }

    public required SprintFilterContextDto RequestedFilter { get; init; }

    public required SprintFilterContextDto EffectiveFilter { get; init; }

    public required IReadOnlyList<string> InvalidFields { get; init; }

    public required IReadOnlyList<FilterValidationIssueDto> ValidationMessages { get; init; }
}
