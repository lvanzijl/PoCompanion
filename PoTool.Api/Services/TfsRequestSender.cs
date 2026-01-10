using System.Net.Http.Headers;

namespace PoTool.Api.Services;

/// <summary>
/// Centralized HTTP request sender for TFS API calls with consistent headers,
/// timeout handling, and error processing.
/// </summary>
public sealed class TfsRequestSender
{
    private readonly ILogger<TfsRequestSender> _logger;
    private const string UserAgent = "PoTool/1.0";

    public TfsRequestSender(ILogger<TfsRequestSender> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sends an HTTP request with per-request timeout and consistent headers.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for the request.</param>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="timeoutSeconds">Timeout in seconds for this specific request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="handleErrors">Optional custom error handler. If not provided, uses default error handling.</param>
    /// <returns>The HTTP response message.</returns>
    public async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        HttpRequestMessage request,
        int timeoutSeconds,
        CancellationToken cancellationToken = default,
        Func<HttpResponseMessage, CancellationToken, Task>? handleErrors = null)
    {
        // Set standard headers
        if (!request.Headers.Contains("Accept"))
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        if (!request.Headers.Contains("User-Agent"))
        {
            request.Headers.UserAgent.ParseAdd(UserAgent);
        }

        // Create per-request timeout token
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        HttpResponseMessage? response = null;
        try
        {
            response = await httpClient.SendAsync(request, timeoutCts.Token);

            // Log correlation/activity IDs if present
            if (response.Headers.TryGetValues("X-TFS-Session", out var sessionValues))
            {
                _logger.LogDebug("TFS Session ID: {SessionId}", string.Join(", ", sessionValues));
            }

            if (response.Headers.TryGetValues("ActivityId", out var activityValues))
            {
                _logger.LogDebug("TFS Activity ID: {ActivityId}", string.Join(", ", activityValues));
            }

            // Handle errors if handler provided or if response is not successful
            if (handleErrors != null)
            {
                await handleErrors(response, cancellationToken);
            }

            return response;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred
            _logger.LogWarning("TFS request timed out after {TimeoutSeconds}s: {Method} {Uri}",
                timeoutSeconds, request.Method, request.RequestUri);
            response?.Dispose();
            throw new TimeoutException($"TFS request timed out after {timeoutSeconds} seconds");
        }
        catch
        {
            response?.Dispose();
            throw;
        }
    }
}
