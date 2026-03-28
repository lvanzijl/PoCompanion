using SharedPrDeliveryInsightsDto = PoTool.Shared.PullRequests.PrDeliveryInsightsDto;
using SharedPrInsightsDto = PoTool.Shared.PullRequests.PullRequestInsightsDto;
using SharedPrMetricsDto = PoTool.Shared.PullRequests.PullRequestMetricsDto;
using SharedPrSprintTrendsResponse = PoTool.Shared.PullRequests.GetPrSprintTrendsResponse;
using SharedPullRequestDto = PoTool.Shared.PullRequests.PullRequestDto;

namespace PoTool.Client.ApiClient;

public partial interface IPullRequestsClient
{
    Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<IReadOnlyList<SharedPrMetricsDto>>> GetMetricsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<IReadOnlyList<SharedPullRequestDto>>> GetFilteredEnvelopeAsync(
        string? productIds,
        string? iterationPath,
        string? createdBy,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        string? status,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrSprintTrendsResponse>> GetSprintTrendsEnvelopeAsync(
        IEnumerable<int>? sprintIds,
        string? productIds,
        int? teamId,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrInsightsDto>> GetInsightsEnvelopeAsync(
        int? teamId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        string? repositoryName,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrDeliveryInsightsDto>> GetDeliveryInsightsEnvelopeAsync(
        int? teamId,
        int? sprintId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken);
}

public partial class PullRequestsClient
{
    public async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<IReadOnlyList<SharedPrMetricsDto>>> GetMetricsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/PullRequests/metrics");
        urlBuilder_.Append('?');

        if (productIds != null)
        {
            urlBuilder_.Append(Uri.EscapeDataString("productIds"))
                .Append('=')
                .Append(Uri.EscapeDataString(ConvertToString(productIds, System.Globalization.CultureInfo.InvariantCulture)))
                .Append('&');
        }

        if (fromDate != null)
        {
            urlBuilder_.Append(Uri.EscapeDataString("fromDate"))
                .Append('=')
                .Append(Uri.EscapeDataString(fromDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture)))
                .Append('&');
        }

        urlBuilder_.Length--;
        return await SendEnvelopeAsync<IReadOnlyList<SharedPrMetricsDto>>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    public async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<IReadOnlyList<SharedPullRequestDto>>> GetFilteredEnvelopeAsync(
        string? productIds,
        string? iterationPath,
        string? createdBy,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        string? status,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/PullRequests/filter");
        urlBuilder_.Append('?');

        AppendQuery(urlBuilder_, "productIds", productIds);
        AppendQuery(urlBuilder_, "iterationPath", iterationPath);
        AppendQuery(urlBuilder_, "createdBy", createdBy);
        AppendQuery(urlBuilder_, "status", status);

        if (fromDate != null)
        {
            AppendQuery(urlBuilder_, "fromDate", fromDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (toDate != null)
        {
            AppendQuery(urlBuilder_, "toDate", toDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
        }

        urlBuilder_.Length--;
        return await SendEnvelopeAsync<IReadOnlyList<SharedPullRequestDto>>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    public async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrSprintTrendsResponse>> GetSprintTrendsEnvelopeAsync(
        IEnumerable<int>? sprintIds,
        string? productIds,
        int? teamId,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/PullRequests/sprint-trends");
        urlBuilder_.Append('?');

        if (sprintIds != null)
        {
            foreach (var sprintId in sprintIds)
            {
                AppendQuery(urlBuilder_, "sprintIds", ConvertToString(sprintId, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        AppendQuery(urlBuilder_, "productIds", productIds);
        if (teamId != null)
        {
            AppendQuery(urlBuilder_, "teamId", ConvertToString(teamId, System.Globalization.CultureInfo.InvariantCulture));
        }

        urlBuilder_.Length--;
        return await SendEnvelopeAsync<SharedPrSprintTrendsResponse>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    public async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrInsightsDto>> GetInsightsEnvelopeAsync(
        int? teamId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        string? repositoryName,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/PullRequests/insights");
        urlBuilder_.Append('?');

        if (teamId != null)
        {
            AppendQuery(urlBuilder_, "teamId", ConvertToString(teamId, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (fromDate != null)
        {
            AppendQuery(urlBuilder_, "fromDate", fromDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (toDate != null)
        {
            AppendQuery(urlBuilder_, "toDate", toDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
        }

        AppendQuery(urlBuilder_, "repositoryName", repositoryName);
        urlBuilder_.Length--;
        return await SendEnvelopeAsync<SharedPrInsightsDto>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    public async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<SharedPrDeliveryInsightsDto>> GetDeliveryInsightsEnvelopeAsync(
        int? teamId,
        int? sprintId,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/PullRequests/delivery-insights");
        urlBuilder_.Append('?');

        if (teamId != null)
        {
            AppendQuery(urlBuilder_, "teamId", ConvertToString(teamId, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (sprintId != null)
        {
            AppendQuery(urlBuilder_, "sprintId", ConvertToString(sprintId, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (fromDate != null)
        {
            AppendQuery(urlBuilder_, "fromDate", fromDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (toDate != null)
        {
            AppendQuery(urlBuilder_, "toDate", toDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
        }

        urlBuilder_.Length--;
        return await SendEnvelopeAsync<SharedPrDeliveryInsightsDto>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    private static void AppendQuery(System.Text.StringBuilder urlBuilder, string key, string? value)
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

    private async Task<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<T>> SendEnvelopeAsync<T>(
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
            var objectResponse_ = await ReadObjectResponseAsync<PoTool.Shared.PullRequests.PullRequestQueryResponseDto<T>>(
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
