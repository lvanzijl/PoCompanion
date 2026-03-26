using PoTool.Shared.Metrics;

namespace PoTool.Client.ApiClient;

/// <summary>
/// Extends IMetricsClient with the read-only portfolio consumption endpoints.
/// Handcrafted extension because the checked-in swagger client is not regenerated automatically during development.
/// </summary>
public partial interface IMetricsClient
{
    Task<PortfolioProgressDto> GetPortfolioProgressAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        CancellationToken cancellationToken = default);

    Task<PortfolioSnapshotDto> GetPortfolioSnapshotsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        CancellationToken cancellationToken = default);

    Task<PortfolioComparisonDto> GetPortfolioComparisonAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        long? compareToSnapshotId,
        CancellationToken cancellationToken = default);

    Task<PortfolioTrendDto> GetPortfolioTrendsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        int snapshotCount,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PortfolioDecisionSignalDto>> GetPortfolioSignalsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        int snapshotCount,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        long? compareToSnapshotId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Partial MetricsClient implementation for the read-only portfolio consumption endpoints.
/// </summary>
public partial class MetricsClient
{
    public Task<PortfolioProgressDto> GetPortfolioProgressAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        CancellationToken cancellationToken = default)
        => GetPortfolioReadModelAsync<PortfolioProgressDto>(
            "api/portfolio/progress",
            productOwnerId,
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            sortBy,
            sortDirection,
            groupBy,
            snapshotCount: null,
            rangeStartUtc: null,
            rangeEndUtc: null,
            includeArchivedSnapshots: false,
            compareToSnapshotId: null,
            cancellationToken);

    public Task<PortfolioSnapshotDto> GetPortfolioSnapshotsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        CancellationToken cancellationToken = default)
        => GetPortfolioReadModelAsync<PortfolioSnapshotDto>(
            "api/portfolio/snapshots",
            productOwnerId,
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            sortBy,
            sortDirection,
            groupBy,
            snapshotCount: null,
            rangeStartUtc: null,
            rangeEndUtc: null,
            includeArchivedSnapshots: false,
            compareToSnapshotId: null,
            cancellationToken);

    public Task<PortfolioComparisonDto> GetPortfolioComparisonAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        long? compareToSnapshotId,
        CancellationToken cancellationToken = default)
        => GetPortfolioReadModelAsync<PortfolioComparisonDto>(
            "api/portfolio/comparison",
            productOwnerId,
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            sortBy,
            sortDirection,
            groupBy,
            snapshotCount: null,
            rangeStartUtc,
            rangeEndUtc,
            includeArchivedSnapshots,
            compareToSnapshotId,
            cancellationToken);

    public Task<PortfolioTrendDto> GetPortfolioTrendsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        int snapshotCount,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        CancellationToken cancellationToken = default)
        => GetPortfolioReadModelAsync<PortfolioTrendDto>(
            "api/portfolio/trends",
            productOwnerId,
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            sortBy,
            sortDirection,
            groupBy,
            snapshotCount,
            rangeStartUtc,
            rangeEndUtc,
            includeArchivedSnapshots,
            compareToSnapshotId: null,
            cancellationToken);

    public Task<IReadOnlyList<PortfolioDecisionSignalDto>> GetPortfolioSignalsAsync(
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        int snapshotCount,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        long? compareToSnapshotId,
        CancellationToken cancellationToken = default)
        => GetPortfolioReadModelAsync<IReadOnlyList<PortfolioDecisionSignalDto>>(
            "api/portfolio/signals",
            productOwnerId,
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            sortBy,
            sortDirection,
            groupBy,
            snapshotCount,
            rangeStartUtc,
            rangeEndUtc,
            includeArchivedSnapshots,
            compareToSnapshotId,
            cancellationToken);

    private async Task<TResponse> GetPortfolioReadModelAsync<TResponse>(
        string path,
        int productOwnerId,
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        int? snapshotCount,
        DateTimeOffset? rangeStartUtc,
        DateTimeOffset? rangeEndUtc,
        bool includeArchivedSnapshots,
        long? compareToSnapshotId,
        CancellationToken cancellationToken)
    {
        var client_ = _httpClient;
        var disposeClient_ = false;
        try
        {
            using var request_ = new HttpRequestMessage();
            request_.Method = new HttpMethod("GET");
            request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

            var urlBuilder_ = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(_baseUrl))
            {
                urlBuilder_.Append(_baseUrl);
            }

            urlBuilder_.Append(path);
            urlBuilder_.Append('?');
            urlBuilder_.Append(Uri.EscapeDataString("productOwnerId"))
                .Append('=')
                .Append(Uri.EscapeDataString(ConvertToString(productOwnerId, System.Globalization.CultureInfo.InvariantCulture)))
                .Append('&');

            AppendOptionalQuery(urlBuilder_, "productId", productId);
            AppendOptionalQuery(urlBuilder_, "projectNumber", projectNumber);
            AppendOptionalQuery(urlBuilder_, "workPackage", workPackage);
            AppendOptionalQuery(urlBuilder_, "lifecycleState", lifecycleState);
            AppendOptionalQuery(urlBuilder_, "sortBy", sortBy);
            AppendOptionalQuery(urlBuilder_, "sortDirection", sortDirection);
            AppendOptionalQuery(urlBuilder_, "groupBy", groupBy);
            AppendOptionalQuery(urlBuilder_, "snapshotCount", snapshotCount);
            AppendOptionalQuery(urlBuilder_, "rangeStartUtc", rangeStartUtc);
            AppendOptionalQuery(urlBuilder_, "rangeEndUtc", rangeEndUtc);
            AppendOptionalQuery(urlBuilder_, "includeArchivedSnapshots", includeArchivedSnapshots);
            AppendOptionalQuery(urlBuilder_, "compareToSnapshotId", compareToSnapshotId);

            urlBuilder_.Length--;

            PrepareRequest(client_, request_, urlBuilder_);

            var url_ = urlBuilder_.ToString();
            request_.RequestUri = new Uri(url_, UriKind.RelativeOrAbsolute);
            PrepareRequest(client_, request_, url_);

            var response_ = await client_.SendAsync(
                request_,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            var disposeResponse_ = true;
            try
            {
                var headers_ = new Dictionary<string, IEnumerable<string>>();
                foreach (var item_ in response_.Headers)
                {
                    headers_[item_.Key] = item_.Value;
                }

                if (response_.Content?.Headers != null)
                {
                    foreach (var item_ in response_.Content.Headers)
                    {
                        headers_[item_.Key] = item_.Value;
                    }
                }

                ProcessResponse(client_, response_);

                var status_ = (int)response_.StatusCode;
                if (status_ == 200)
                {
                    var objectResponse_ = await ReadObjectResponseAsync<TResponse>(response_, headers_, cancellationToken).ConfigureAwait(false);
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
            finally
            {
                if (disposeResponse_)
                {
                    response_.Dispose();
                }
            }
        }
        finally
        {
            if (disposeClient_)
            {
                client_.Dispose();
            }
        }
    }

    private void AppendOptionalQuery<TValue>(System.Text.StringBuilder urlBuilder, string name, TValue? value)
    {
        if (value is null)
        {
            return;
        }

        urlBuilder.Append(Uri.EscapeDataString(name))
            .Append('=')
            .Append(Uri.EscapeDataString(ConvertToString(value, System.Globalization.CultureInfo.InvariantCulture)))
            .Append('&');
    }
}
