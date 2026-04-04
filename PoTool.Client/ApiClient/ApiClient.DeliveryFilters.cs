using PoTool.Shared.Metrics;
using PoTool.Client.Helpers;

namespace PoTool.Client.ApiClient;

public partial interface IMetricsClient
{
    Task<PoTool.Shared.Metrics.DeliveryQueryResponseDto<PortfolioProgressTrendDto>> GetPortfolioProgressTrendEnvelopeAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.Metrics.DeliveryQueryResponseDto<PortfolioDeliveryDto>> GetPortfolioDeliveryEnvelopeAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.Metrics.DeliveryQueryResponseDto<CapacityCalibrationDto>> GetCapacityCalibrationEnvelopeAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken);

    Task<PoTool.Shared.Metrics.DeliveryQueryResponseDto<HomeProductBarMetricsDto>> GetHomeProductBarMetricsEnvelopeAsync(
        int productOwnerId,
        int? productId,
        CancellationToken cancellationToken);
}

public partial class MetricsClient
{
    public async Task<PoTool.Shared.Metrics.DeliveryQueryResponseDto<PortfolioProgressTrendDto>> GetPortfolioProgressTrendEnvelopeAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken)
    {
        var response = await GetPortfolioProgressTrendAsync(
            productOwnerId,
            sprintIds,
            productIds,
            cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetPortfolioProgressTrendEnvelopeAsync));
    }

    public async Task<PoTool.Shared.Metrics.DeliveryQueryResponseDto<PortfolioDeliveryDto>> GetPortfolioDeliveryEnvelopeAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken)
    {
        var response = await GetPortfolioDeliveryAsync(
            productOwnerId,
            sprintIds,
            productIds,
            cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetPortfolioDeliveryEnvelopeAsync));
    }

    public async Task<PoTool.Shared.Metrics.DeliveryQueryResponseDto<CapacityCalibrationDto>> GetCapacityCalibrationEnvelopeAsync(
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken)
    {
        var response = await GetCapacityCalibrationAsync(
            productOwnerId,
            sprintIds,
            productIds,
            cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetCapacityCalibrationEnvelopeAsync));
    }

    public async Task<PoTool.Shared.Metrics.DeliveryQueryResponseDto<HomeProductBarMetricsDto>> GetHomeProductBarMetricsEnvelopeAsync(
        int productOwnerId,
        int? productId,
        CancellationToken cancellationToken)
    {
        var response = await GetHomeProductBarMetricsAsync(productOwnerId, productId, cancellationToken);
        return CacheBackedGeneratedClientHelper.RequireData(
            GeneratedCacheEnvelopeHelper.ToCacheBackedResult(
                response,
                static data => data.ToShared()),
            nameof(GetHomeProductBarMetricsEnvelopeAsync));
    }

    private async Task<PoTool.Shared.Metrics.DeliveryQueryResponseDto<T>> GetDeliveryEnvelopeAsync<T>(
        string path,
        int productOwnerId,
        IEnumerable<int> sprintIds,
        IEnumerable<int>? productIds,
        CancellationToken cancellationToken)
    {
        using var request_ = new HttpRequestMessage();
        request_.Method = new HttpMethod("GET");
        request_.Headers.Accept.Add(System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

        var urlBuilder_ = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
        urlBuilder_.Append(path);
        urlBuilder_.Append('?');

        AppendQuery(urlBuilder_, "productOwnerId", ConvertToString(productOwnerId, System.Globalization.CultureInfo.InvariantCulture));
        foreach (var sprintId in sprintIds ?? Array.Empty<int>())
        {
            AppendQuery(urlBuilder_, "sprintIds", ConvertToString(sprintId, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (productIds != null)
        {
            foreach (var productId in productIds)
            {
                AppendQuery(urlBuilder_, "productIds", ConvertToString(productId, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        urlBuilder_.Length--;
        return await SendDeliveryEnvelopeAsync<T>(request_, urlBuilder_.ToString(), cancellationToken);
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

    private async Task<PoTool.Shared.Metrics.DeliveryQueryResponseDto<T>> SendDeliveryEnvelopeAsync<T>(
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
            var objectResponse_ = await ReadObjectResponseAsync<PoTool.Shared.Metrics.DeliveryQueryResponseDto<T>>(
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
