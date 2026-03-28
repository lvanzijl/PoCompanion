using PoTool.Shared.Metrics;

namespace PoTool.Client.ApiClient;

public partial interface IMetricsClient
{
    Task<SprintQueryResponseDto<SprintMetricsDto>> GetSprintMetricsEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<BacklogHealthDto>> GetBacklogHealthEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<MultiIterationBacklogHealthDto>> GetMultiIterationBacklogHealthEnvelopeAsync(
        int? productOwnerId,
        IEnumerable<int>? productIds,
        string? areaPath,
        int? maxIterations,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<SprintCapacityPlanDto>> GetSprintCapacityPlanEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        int? defaultCapacity,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<GetSprintTrendMetricsResponse>> GetSprintTrendMetricsEnvelopeAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        bool? recompute,
        bool? includeDetails,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<SprintExecutionDto>> GetSprintExecutionEnvelopeAsync(
        int productOwnerId,
        int sprintId,
        int? productId,
        CancellationToken cancellationToken);

    Task<SprintQueryResponseDto<WorkItemActivityDetailsDto>> GetWorkItemActivityDetailsEnvelopeAsync(
        int workItemId,
        int productOwnerId,
        int? sprintId,
        DateTimeOffset? periodStartUtc,
        DateTimeOffset? periodEndUtc,
        CancellationToken cancellationToken);
}

public partial class MetricsClient
{
    public Task<SprintQueryResponseDto<SprintMetricsDto>> GetSprintMetricsEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        CancellationToken cancellationToken)
        => SendSprintEnvelopeAsync<SprintMetricsDto>(
            "api/Metrics/sprint",
            iterationPath,
            productOwnerId,
            productIds,
            sprintId,
            null,
            null,
            null,
            cancellationToken);

    public Task<SprintQueryResponseDto<BacklogHealthDto>> GetBacklogHealthEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        CancellationToken cancellationToken)
        => SendSprintEnvelopeAsync<BacklogHealthDto>(
            "api/Metrics/backlog-health",
            iterationPath,
            productOwnerId,
            productIds,
            sprintId,
            null,
            null,
            null,
            cancellationToken);

    public async Task<SprintQueryResponseDto<MultiIterationBacklogHealthDto>> GetMultiIterationBacklogHealthEnvelopeAsync(
        int? productOwnerId,
        IEnumerable<int>? productIds,
        string? areaPath,
        int? maxIterations,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/Metrics/multi-iteration-health");
        urlBuilder_.Append('?');

        if (productOwnerId.HasValue)
        {
            AppendSprintQuery(urlBuilder_, "productOwnerId", ConvertToString(productOwnerId.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (var productId in productIds ?? Array.Empty<int>())
        {
            AppendSprintQuery(urlBuilder_, "productIds", ConvertToString(productId, System.Globalization.CultureInfo.InvariantCulture));
        }

        AppendSprintQuery(urlBuilder_, "areaPath", areaPath);
        if (maxIterations.HasValue)
        {
            AppendSprintQuery(urlBuilder_, "maxIterations", ConvertToString(maxIterations.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        urlBuilder_.Length--;
        return await SendSprintEnvelopeAsync<MultiIterationBacklogHealthDto>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    public async Task<SprintQueryResponseDto<SprintCapacityPlanDto>> GetSprintCapacityPlanEnvelopeAsync(
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        int? defaultCapacity,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = BuildSprintBaseUrl(
            "api/Metrics/capacity-plan",
            iterationPath,
            productOwnerId,
            productIds,
            sprintId);

        if (defaultCapacity.HasValue)
        {
            AppendSprintQuery(urlBuilder_, "defaultCapacity", ConvertToString(defaultCapacity.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        urlBuilder_.Length--;
        return await SendSprintEnvelopeAsync<SprintCapacityPlanDto>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    public async Task<SprintQueryResponseDto<GetSprintTrendMetricsResponse>> GetSprintTrendMetricsEnvelopeAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        bool? recompute,
        bool? includeDetails,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/Metrics/sprint-trend");
        urlBuilder_.Append('?');
        AppendSprintQuery(urlBuilder_, "productOwnerId", ConvertToString(productOwnerId, System.Globalization.CultureInfo.InvariantCulture));

        foreach (var sprintId in sprintIds)
        {
            AppendSprintQuery(urlBuilder_, "sprintIds", ConvertToString(sprintId, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (productIds != null)
        {
            foreach (var productId in productIds)
            {
                AppendSprintQuery(urlBuilder_, "productIds", ConvertToString(productId, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if (recompute.HasValue)
        {
            AppendSprintQuery(urlBuilder_, "recompute", ConvertToString(recompute.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (includeDetails.HasValue)
        {
            AppendSprintQuery(urlBuilder_, "includeDetails", ConvertToString(includeDetails.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        urlBuilder_.Length--;
        return await SendSprintEnvelopeAsync<GetSprintTrendMetricsResponse>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    public async Task<SprintQueryResponseDto<SprintExecutionDto>> GetSprintExecutionEnvelopeAsync(
        int productOwnerId,
        int sprintId,
        int? productId,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/Metrics/sprint-execution");
        urlBuilder_.Append('?');
        AppendSprintQuery(urlBuilder_, "productOwnerId", ConvertToString(productOwnerId, System.Globalization.CultureInfo.InvariantCulture));
        AppendSprintQuery(urlBuilder_, "sprintId", ConvertToString(sprintId, System.Globalization.CultureInfo.InvariantCulture));

        if (productId.HasValue)
        {
            AppendSprintQuery(urlBuilder_, "productId", ConvertToString(productId.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        urlBuilder_.Length--;
        return await SendSprintEnvelopeAsync<SprintExecutionDto>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    public async Task<SprintQueryResponseDto<WorkItemActivityDetailsDto>> GetWorkItemActivityDetailsEnvelopeAsync(
        int workItemId,
        int productOwnerId,
        int? sprintId,
        DateTimeOffset? periodStartUtc,
        DateTimeOffset? periodEndUtc,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/Metrics/work-item-activity/");
        urlBuilder_.Append(Uri.EscapeDataString(ConvertToString(workItemId, System.Globalization.CultureInfo.InvariantCulture)));
        urlBuilder_.Append('?');
        AppendSprintQuery(urlBuilder_, "productOwnerId", ConvertToString(productOwnerId, System.Globalization.CultureInfo.InvariantCulture));

        if (sprintId.HasValue)
        {
            AppendSprintQuery(urlBuilder_, "sprintId", ConvertToString(sprintId.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        AppendSprintDateQuery(urlBuilder_, "periodStartUtc", periodStartUtc);
        AppendSprintDateQuery(urlBuilder_, "periodEndUtc", periodEndUtc);

        urlBuilder_.Length--;
        return await SendSprintEnvelopeAsync<WorkItemActivityDetailsDto>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    private async Task<SprintQueryResponseDto<T>> SendSprintEnvelopeAsync<T>(
        string path,
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId,
        DateTimeOffset? periodStartUtc,
        DateTimeOffset? periodEndUtc,
        int? workItemId,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = BuildSprintBaseUrl(path, iterationPath, productOwnerId, productIds, sprintId);
        AppendSprintDateQuery(urlBuilder_, "periodStartUtc", periodStartUtc);
        AppendSprintDateQuery(urlBuilder_, "periodEndUtc", periodEndUtc);

        urlBuilder_.Length--;
        return await SendSprintEnvelopeAsync<T>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    private System.Text.StringBuilder BuildSprintBaseUrl(
        string path,
        string? iterationPath,
        int? productOwnerId,
        IEnumerable<int>? productIds,
        int? sprintId)
    {
        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append(path);
        urlBuilder_.Append('?');

        AppendSprintQuery(urlBuilder_, "iterationPath", iterationPath);
        if (productOwnerId.HasValue)
        {
            AppendSprintQuery(urlBuilder_, "productOwnerId", ConvertToString(productOwnerId.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (var productId in productIds ?? Array.Empty<int>())
        {
            AppendSprintQuery(urlBuilder_, "productIds", ConvertToString(productId, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (sprintId.HasValue)
        {
            AppendSprintQuery(urlBuilder_, "sprintId", ConvertToString(sprintId.Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        return urlBuilder_;
    }

    private static void AppendSprintQuery(System.Text.StringBuilder urlBuilder, string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        urlBuilder.Append(Uri.EscapeDataString(key))
            .Append('=')
            .Append(Uri.EscapeDataString(value))
            .Append('&');
    }

    private static void AppendSprintDateQuery(System.Text.StringBuilder urlBuilder, string key, DateTimeOffset? value)
    {
        if (value == null)
        {
            return;
        }

        AppendSprintQuery(urlBuilder, key, value.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
    }

    private async Task<SprintQueryResponseDto<T>> SendSprintEnvelopeAsync<T>(
        HttpRequestMessage request,
        string url,
        CancellationToken cancellationToken)
    {
        var client_ = _httpClient;
        PrepareRequest(client_, request, url);
        request.RequestUri = new Uri(url, UriKind.RelativeOrAbsolute);
        PrepareRequest(client_, request, url);

        using var response_ = await client_.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        var headers_ = new Dictionary<string, IEnumerable<string>>();
        foreach (var item_ in response_.Headers) headers_[item_.Key] = item_.Value;
        if (response_.Content?.Headers != null)
            foreach (var item_ in response_.Content.Headers) headers_[item_.Key] = item_.Value;

        ProcessResponse(client_, response_);

        var status_ = (int)response_.StatusCode;
        if (status_ == 200)
        {
            var objectResponse_ = await ReadObjectResponseAsync<SprintQueryResponseDto<T>>(
                response_,
                headers_,
                cancellationToken).ConfigureAwait(false);

            if (objectResponse_.Object == null)
            {
                throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
            }

            return objectResponse_.Object;
        }

        var responseData_ = response_.Content == null
            ? null
            : await response_.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new ApiException(
            "The HTTP status code of the response was not expected (" + status_ + ").",
            status_,
            responseData_,
            headers_,
            null);
    }
}
