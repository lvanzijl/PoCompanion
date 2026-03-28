namespace PoTool.Client.ApiClient;

public partial interface IPipelinesClient
{
    Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>> GetMetricsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<IReadOnlyList<PipelineRunDto>>> GetRunsForProductsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<PipelineInsightsDto>> GetInsightsEnvelopeAsync(
        int? productOwnerId,
        int? sprintId,
        bool? includePartiallySucceeded,
        bool? includeCanceled,
        CancellationToken cancellationToken);
}

public partial class PipelinesClient
{
    public async Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>> GetMetricsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/Pipelines/metrics");
        urlBuilder_.Append('?');

        AppendQuery(urlBuilder_, "productIds", productIds);
        AppendDateQuery(urlBuilder_, "fromDate", fromDate);
        AppendDateQuery(urlBuilder_, "toDate", toDate);

        urlBuilder_.Length--;
        return await SendEnvelopeAsync<IReadOnlyList<PipelineMetricsDto>>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    public async Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<IReadOnlyList<PipelineRunDto>>> GetRunsForProductsEnvelopeAsync(
        string? productIds,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/Pipelines/runs");
        urlBuilder_.Append('?');

        AppendQuery(urlBuilder_, "productIds", productIds);
        AppendDateQuery(urlBuilder_, "fromDate", fromDate);
        AppendDateQuery(urlBuilder_, "toDate", toDate);

        urlBuilder_.Length--;
        return await SendEnvelopeAsync<IReadOnlyList<PipelineRunDto>>(request_, urlBuilder_.ToString(), cancellationToken);
    }

    public async Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<PipelineInsightsDto>> GetInsightsEnvelopeAsync(
        int? productOwnerId,
        int? sprintId,
        bool? includePartiallySucceeded,
        bool? includeCanceled,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append("api/Pipelines/insights");
        urlBuilder_.Append('?');

        if (productOwnerId != null)
        {
            AppendQuery(urlBuilder_, "productOwnerId", ConvertToString(productOwnerId, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (sprintId != null)
        {
            AppendQuery(urlBuilder_, "sprintId", ConvertToString(sprintId, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (includePartiallySucceeded != null)
        {
            AppendQuery(urlBuilder_, "includePartiallySucceeded", ConvertToString(includePartiallySucceeded, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (includeCanceled != null)
        {
            AppendQuery(urlBuilder_, "includeCanceled", ConvertToString(includeCanceled, System.Globalization.CultureInfo.InvariantCulture));
        }

        urlBuilder_.Length--;
        return await SendEnvelopeAsync<PipelineInsightsDto>(request_, urlBuilder_.ToString(), cancellationToken);
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

    private static void AppendDateQuery(System.Text.StringBuilder urlBuilder, string key, DateTimeOffset? value)
    {
        if (value == null)
        {
            return;
        }

        AppendQuery(urlBuilder, key, value.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
    }

    private async Task<PoTool.Shared.Pipelines.PipelineQueryResponseDto<T>> SendEnvelopeAsync<T>(
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
            var objectResponse_ = await ReadObjectResponseAsync<PoTool.Shared.Pipelines.PipelineQueryResponseDto<T>>(
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
