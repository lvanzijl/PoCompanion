namespace PoTool.Shared.Metrics;

public sealed record DeliveryFilterContextDto
{
    public required FilterSelectionDto<int> ProductIds { get; init; }

    public required FilterTimeSelectionDto Time { get; init; }
}

public sealed record DeliveryQueryResponseDto<T>
{
    public required T Data { get; init; }

    public required DeliveryFilterContextDto RequestedFilter { get; init; }

    public required DeliveryFilterContextDto EffectiveFilter { get; init; }

    public required IReadOnlyList<string> InvalidFields { get; init; }

    public required IReadOnlyList<FilterValidationIssueDto> ValidationMessages { get; init; }

    public required IReadOnlyDictionary<int, string> TeamLabels { get; init; }

    public required IReadOnlyDictionary<int, string> SprintLabels { get; init; }
}
