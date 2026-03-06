using PoTool.Shared.PullRequests;

namespace PoTool.Client.ApiClient;

/// <summary>
/// Extends IPullRequestsClient with the PR Insights endpoint.
/// This is a handcrafted extension of the NSwag-generated client because
/// the swagger.json is not auto-generated from the live API during development.
/// </summary>
public partial interface IPullRequestsClient
{
    /// <summary>Gets PR Insights for a team over a date range. All data from local cache.</summary>
    /// <exception cref="ApiException">A server side error occurred.</exception>
    System.Threading.Tasks.Task<PullRequestInsightsDto> GetInsightsAsync(
        int? teamId,
        System.DateTimeOffset? fromDate,
        System.DateTimeOffset? toDate,
        string? repositoryName);

    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <exception cref="ApiException">A server side error occurred.</exception>
    System.Threading.Tasks.Task<PullRequestInsightsDto> GetInsightsAsync(
        int? teamId,
        System.DateTimeOffset? fromDate,
        System.DateTimeOffset? toDate,
        string? repositoryName,
        System.Threading.CancellationToken cancellationToken);
}

/// <summary>
/// Partial implementation of PullRequestsClient for the PR Insights endpoint.
/// </summary>
public partial class PullRequestsClient
{
    public virtual System.Threading.Tasks.Task<PullRequestInsightsDto> GetInsightsAsync(
        int? teamId,
        System.DateTimeOffset? fromDate,
        System.DateTimeOffset? toDate,
        string? repositoryName)
    {
        return GetInsightsAsync(teamId, fromDate, toDate, repositoryName, System.Threading.CancellationToken.None);
    }

    public virtual async System.Threading.Tasks.Task<PullRequestInsightsDto> GetInsightsAsync(
        int? teamId,
        System.DateTimeOffset? fromDate,
        System.DateTimeOffset? toDate,
        string? repositoryName,
        System.Threading.CancellationToken cancellationToken)
    {
        var client_ = _httpClient;
        var disposeClient_ = false;
        try
        {
            using var request_ = new System.Net.Http.HttpRequestMessage();
            request_.Method = new System.Net.Http.HttpMethod("GET");
            request_.Headers.Accept.Add(
                System.Net.Http.Headers.MediaTypeWithQualityHeaderValue.Parse("application/json"));

            var urlBuilder_ = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(_baseUrl)) urlBuilder_.Append(_baseUrl);
            urlBuilder_.Append("api/PullRequests/insights");
            urlBuilder_.Append('?');

            if (teamId != null)
                urlBuilder_.Append(System.Uri.EscapeDataString("teamId"))
                    .Append('=')
                    .Append(System.Uri.EscapeDataString(ConvertToString(teamId, System.Globalization.CultureInfo.InvariantCulture)))
                    .Append('&');

            if (fromDate != null)
                urlBuilder_.Append(System.Uri.EscapeDataString("fromDate"))
                    .Append('=')
                    .Append(System.Uri.EscapeDataString(fromDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture)))
                    .Append('&');

            if (toDate != null)
                urlBuilder_.Append(System.Uri.EscapeDataString("toDate"))
                    .Append('=')
                    .Append(System.Uri.EscapeDataString(toDate.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture)))
                    .Append('&');

            if (repositoryName != null)
                urlBuilder_.Append(System.Uri.EscapeDataString("repositoryName"))
                    .Append('=')
                    .Append(System.Uri.EscapeDataString(ConvertToString(repositoryName, System.Globalization.CultureInfo.InvariantCulture)))
                    .Append('&');

            urlBuilder_.Length--;

            PrepareRequest(client_, request_, urlBuilder_);

            var url_ = urlBuilder_.ToString();
            request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);
            PrepareRequest(client_, request_, url_);

            var response_ = await client_.SendAsync(
                request_,
                System.Net.Http.HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            var disposeResponse_ = true;
            try
            {
                var headers_ = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>>();
                foreach (var item_ in response_.Headers) headers_[item_.Key] = item_.Value;
                if (response_.Content?.Headers != null)
                    foreach (var item_ in response_.Content.Headers) headers_[item_.Key] = item_.Value;

                ProcessResponse(client_, response_);

                var status_ = (int)response_.StatusCode;
                if (status_ == 200)
                {
                    var objectResponse_ = await ReadObjectResponseAsync<PullRequestInsightsDto>(
                        response_, headers_, cancellationToken).ConfigureAwait(false);
                    if (objectResponse_.Object == null)
                        throw new ApiException("Response was null which was not expected.", status_, objectResponse_.Text, headers_, null);
                    return objectResponse_.Object;
                }
                else
                {
                    var responseData_ = response_.Content == null
                        ? null
                        : await response_.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    throw new ApiException(
                        "The HTTP status code of the response was not expected (" + status_ + ").",
                        status_, responseData_, headers_, null);
                }
            }
            finally
            {
                if (disposeResponse_) response_.Dispose();
            }
        }
        finally
        {
            if (disposeClient_) client_.Dispose();
        }
    }
}
