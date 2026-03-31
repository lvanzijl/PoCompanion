namespace PoTool.Client.ApiClient;

public partial class MetricsClient
{
    public Task<SprintQueryResponseDtoOfGetSprintTrendMetricsResponse> GetSprintTrendMetricsAsync(
        int? productOwnerId,
        IEnumerable<int>? sprintIds,
        bool? recompute,
        CancellationToken cancellationToken)
        => GetSprintTrendMetricsAsync(
            productOwnerId,
            sprintIds,
            productIds: null,
            recompute,
            includeDetails: null,
            cancellationToken);

    public async Task<MultiIterationBacklogHealthDto?> GetMultiIterationBacklogHealthAsync(
        IEnumerable<int>? productIds,
        string? areaPath,
        int? maxIterations,
        CancellationToken cancellationToken = default)
    {
        var response = await GetMultiIterationBacklogHealthAsync(
            productOwnerId: null,
            productIds,
            areaPath,
            maxIterations,
            cancellationToken);

        return response.Data;
    }
}

public partial class WorkItemDto
{
    public string? JsonPayload { get; set; }
}

public partial class WorkItemWithValidationDto
{
    public string? JsonPayload { get; set; }
}
