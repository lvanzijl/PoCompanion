using PoTool.Client.ApiClient;
using PoTool.Shared.Metrics;
using PoTool.Shared.Pipelines;
using PoTool.Shared.PullRequests;

namespace PoTool.Client.Helpers;

public static class GeneratedClientDtoMappings
{
    public static IReadOnlyList<TItem> ToReadOnlyList<TItem>(this ICollection<TItem>? items)
        => items is null ? Array.Empty<TItem>() : items.ToList();

    public static DeliveryQueryResponseDto<BuildQualityPageDto> ToShared(this DeliveryQueryResponseDtoOfBuildQualityPageDto source)
        => CreateDelivery(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static DeliveryQueryResponseDto<DeliveryBuildQualityDto> ToShared(this DeliveryQueryResponseDtoOfDeliveryBuildQualityDto source)
        => CreateDelivery(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static DeliveryQueryResponseDto<PortfolioProgressTrendDto> ToShared(this DeliveryQueryResponseDtoOfPortfolioProgressTrendDto source)
        => CreateDelivery(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static DeliveryQueryResponseDto<PortfolioDeliveryDto> ToShared(this DeliveryQueryResponseDtoOfPortfolioDeliveryDto source)
        => CreateDelivery(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static DeliveryQueryResponseDto<CapacityCalibrationDto> ToShared(this DeliveryQueryResponseDtoOfCapacityCalibrationDto source)
        => CreateDelivery(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static DeliveryQueryResponseDto<HomeProductBarMetricsDto> ToShared(this DeliveryQueryResponseDtoOfHomeProductBarMetricsDto source)
        => CreateDelivery(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static SprintQueryResponseDto<SprintMetricsDto> ToShared(this SprintQueryResponseDtoOfSprintMetricsDto source)
        => CreateSprint(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static SprintQueryResponseDto<BacklogHealthDto> ToShared(this SprintQueryResponseDtoOfBacklogHealthDto source)
        => CreateSprint(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static SprintQueryResponseDto<MultiIterationBacklogHealthDto> ToShared(this SprintQueryResponseDtoOfMultiIterationBacklogHealthDto source)
        => CreateSprint(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static SprintQueryResponseDto<SprintCapacityPlanDto> ToShared(this SprintQueryResponseDtoOfSprintCapacityPlanDto source)
        => CreateSprint(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static SprintQueryResponseDto<GetSprintTrendMetricsResponse> ToShared(this SprintQueryResponseDtoOfGetSprintTrendMetricsResponse source)
        => CreateSprint(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static SprintQueryResponseDto<SprintExecutionDto> ToShared(this SprintQueryResponseDtoOfSprintExecutionDto source)
        => CreateSprint(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static SprintQueryResponseDto<WorkItemActivityDetailsDto> ToShared(this SprintQueryResponseDtoOfWorkItemActivityDetailsDto source)
        => CreateSprint(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>> ToShared(this PipelineQueryResponseDtoOfIReadOnlyListOfPipelineMetricsDto source)
        => CreatePipeline(source.Data.ToReadOnlyList(), source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static PipelineQueryResponseDto<IReadOnlyList<PipelineRunDto>> ToShared(this PipelineQueryResponseDtoOfIReadOnlyListOfPipelineRunDto source)
        => CreatePipeline(source.Data.ToReadOnlyList(), source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static PipelineQueryResponseDto<PipelineInsightsDto> ToShared(this PipelineQueryResponseDtoOfPipelineInsightsDto source)
        => CreatePipeline(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static PullRequestQueryResponseDto<IReadOnlyList<PullRequestMetricsDto>> ToShared(this PullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestMetricsDto source)
        => CreatePullRequest(source.Data.ToReadOnlyList(), source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static PullRequestQueryResponseDto<IReadOnlyList<PullRequestDto>> ToShared(this PullRequestQueryResponseDtoOfIReadOnlyListOfPullRequestDto source)
        => CreatePullRequest(source.Data.ToReadOnlyList(), source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static PullRequestQueryResponseDto<GetPrSprintTrendsResponse> ToShared(this PullRequestQueryResponseDtoOfGetPrSprintTrendsResponse source)
        => CreatePullRequest(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static PullRequestQueryResponseDto<PullRequestInsightsDto> ToShared(this PullRequestQueryResponseDtoOfPullRequestInsightsDto source)
        => CreatePullRequest(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    public static PullRequestQueryResponseDto<PrDeliveryInsightsDto> ToShared(this PullRequestQueryResponseDtoOfPrDeliveryInsightsDto source)
        => CreatePullRequest(source.Data!, source.RequestedFilter, source.EffectiveFilter, source.InvalidFields, source.ValidationMessages, source.TeamLabels, source.SprintLabels);

    private static DeliveryQueryResponseDto<TData> CreateDelivery<TData>(
        TData data,
        DeliveryFilterContextDto requestedFilter,
        DeliveryFilterContextDto effectiveFilter,
        ICollection<string> invalidFields,
        ICollection<FilterValidationIssueDto> validationMessages,
        ICollection<KeyValuePair<int, string>>? teamLabels,
        ICollection<KeyValuePair<int, string>>? sprintLabels)
        => new()
        {
            Data = data,
            RequestedFilter = requestedFilter,
            EffectiveFilter = effectiveFilter,
            InvalidFields = invalidFields.ToList(),
            ValidationMessages = validationMessages.ToList(),
            TeamLabels = ToDictionary(teamLabels),
            SprintLabels = ToDictionary(sprintLabels)
        };

    private static SprintQueryResponseDto<TData> CreateSprint<TData>(
        TData data,
        SprintFilterContextDto requestedFilter,
        SprintFilterContextDto effectiveFilter,
        ICollection<string> invalidFields,
        ICollection<FilterValidationIssueDto> validationMessages,
        ICollection<KeyValuePair<int, string>>? teamLabels,
        ICollection<KeyValuePair<int, string>>? sprintLabels)
        => new()
        {
            Data = data,
            RequestedFilter = requestedFilter,
            EffectiveFilter = effectiveFilter,
            InvalidFields = invalidFields.ToList(),
            ValidationMessages = validationMessages.ToList(),
            TeamLabels = ToDictionary(teamLabels),
            SprintLabels = ToDictionary(sprintLabels)
        };

    private static PipelineQueryResponseDto<TData> CreatePipeline<TData>(
        TData data,
        PipelineFilterContextDto requestedFilter,
        PipelineFilterContextDto effectiveFilter,
        ICollection<string> invalidFields,
        ICollection<FilterValidationIssueDto> validationMessages,
        ICollection<KeyValuePair<int, string>>? teamLabels,
        ICollection<KeyValuePair<int, string>>? sprintLabels)
        => new()
        {
            Data = data,
            RequestedFilter = requestedFilter,
            EffectiveFilter = effectiveFilter,
            InvalidFields = invalidFields.ToList(),
            ValidationMessages = validationMessages.ToList(),
            TeamLabels = ToDictionary(teamLabels),
            SprintLabels = ToDictionary(sprintLabels)
        };

    private static PullRequestQueryResponseDto<TData> CreatePullRequest<TData>(
        TData data,
        PullRequestFilterContextDto requestedFilter,
        PullRequestFilterContextDto effectiveFilter,
        ICollection<string> invalidFields,
        ICollection<FilterValidationIssueDto> validationMessages,
        ICollection<KeyValuePair<int, string>>? teamLabels,
        ICollection<KeyValuePair<int, string>>? sprintLabels)
        => new()
        {
            Data = data,
            RequestedFilter = requestedFilter,
            EffectiveFilter = effectiveFilter,
            InvalidFields = invalidFields.ToList(),
            ValidationMessages = validationMessages.ToList(),
            TeamLabels = ToDictionary(teamLabels),
            SprintLabels = ToDictionary(sprintLabels)
        };

    private static IReadOnlyDictionary<int, string> ToDictionary(ICollection<KeyValuePair<int, string>>? values)
    {
        if (values is null)
        {
            return new Dictionary<int, string>();
        }

        var grouped = values
            .GroupBy(pair => pair.Key)
            .ToList();

        var duplicateKeys = grouped
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key)
            .ToArray();
        if (duplicateKeys.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate canonical filter labels were returned for keys: {string.Join(", ", duplicateKeys)}.");
        }

        return grouped.ToDictionary(group => group.Key, group => group.Single().Value);
    }
}
