using System.Net;
using System.Net.Http.Headers;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Shared.Settings;
using PoTool.Shared.Exceptions;

namespace PoTool.Integrations.Tfs.Clients;

public partial class RealTfsClient
{
    private async Task<List<(string Name, string Id)>> GetRepositoriesInternalAsync(
        TfsConfigEntity config,
        HttpClient httpClient,
        string? repositoryName,
        CancellationToken cancellationToken)
    {
        // If specific repository requested, resolve it to get the canonical ID
        if (!string.IsNullOrEmpty(repositoryName))
        {
            _logger.LogDebug("Resolving repository name '{RepositoryName}' to canonical ID", repositoryName);

            // Call _apis/git/repositories/{repositoryName} to resolve canonical repo id/name
            var repoUrl = ProjectUrl(config, $"_apis/git/repositories/{Uri.EscapeDataString(repositoryName)}");
            var repoResponse = await SendGetAsync(httpClient, config, repoUrl, cancellationToken, handleErrors: false);

            if (repoResponse.IsSuccessStatusCode)
            {
                using var repoStream = await repoResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var repoDoc = await JsonDocument.ParseAsync(repoStream, cancellationToken: cancellationToken);

                var name = repoDoc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? repositoryName : repositoryName;
                var id = repoDoc.RootElement.TryGetProperty("id", out var i) ? i.GetString() ?? repositoryName : repositoryName;

                _logger.LogInformation("Resolved repository '{Name}' to ID '{Id}'", name, id);
                return new List<(string Name, string Id)> { (name, id) };
            }
            else
            {
                _logger.LogWarning("Failed to resolve repository '{RepositoryName}', using name as fallback", repositoryName);
                return new List<(string Name, string Id)> { (repositoryName, repositoryName) };
            }
        }

        // Git repositories are project-scoped (requirement #1)
        var url = ProjectUrl(config, "_apis/git/repositories");
        var repositories = new List<(string Name, string Id)>();
        string? continuationToken = null;
        var pageUrl = url;

        do
        {
            var response = await SendGetAsync(httpClient, config, pageUrl, cancellationToken, handleErrors: false);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (doc.RootElement.TryGetProperty("value", out var valueArray))
            {
                foreach (var repo in valueArray.EnumerateArray())
                {
                    var name = repo.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var id = repo.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";

                    if (!string.IsNullOrEmpty(name))
                    {
                        repositories.Add((name, id));
                    }
                }
            }

            continuationToken = GetContinuationToken(response, doc);
            pageUrl = AddContinuationToken(url, continuationToken);
        } while (!string.IsNullOrWhiteSpace(continuationToken));

        _logger.LogInformation("Found {Count} repositories in project {Project}", repositories.Count, config.Project);
        return repositories;
    }

    // Legacy method kept for backward compatibility - forwards to internal implementation
    private async Task<List<(string Name, string Id)>> GetRepositoriesAsync(
        TfsConfigEntity entity,
        string? repositoryName,
        CancellationToken cancellationToken)
    {
        var httpClient = GetAuthenticatedHttpClient();
        return await GetRepositoriesInternalAsync(entity, httpClient, repositoryName, cancellationToken);
    }

    /// <summary>
    /// Executes an operation with retry logic for transient errors.
    /// SAFETY: Only retries safe idempotent operations (GET requests).
    /// Non-idempotent operations (PATCH, POST create) are never retried to prevent unintended side effects.
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken,
        int maxRetries = MaxRetries,
        bool isIdempotent = true)
    {
        int attempt = 0;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (TfsRateLimitException rateLimitEx)
            {
                // Handle rate limiting separately - always retry with backoff
                attempt++;
                if (attempt >= maxRetries)
                {
                    _logger.LogError("TFS rate limit retry exhausted after {Attempt} attempts", attempt);
                    throw;
                }

                // Use Retry-After header if provided, otherwise exponential backoff
                var delay = rateLimitEx.RetryAfter ?? CalculateBackoffDelay(attempt);

                _logger.LogWarning(
                    "TFS rate limit hit (attempt {Attempt}/{MaxRetries}), retrying after {DelayMs}ms",
                    attempt, maxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex) when (isIdempotent && IsTransient(ex) && attempt < maxRetries)
            {
                // Only retry transient errors for idempotent operations
                attempt++;
                var delay = CalculateBackoffDelay(attempt);

                _logger.LogWarning(ex,
                    "TFS request failed (attempt {Attempt}/{MaxRetries}), retrying after {DelayMs}ms",
                    attempt, maxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private bool IsTransient(Exception ex)
    {
        return ex is TfsRateLimitException
            || ex is HttpRequestException
            || (ex is TfsException tfsEx &&
                tfsEx.StatusCode.HasValue &&
                IsRetryableStatusCode(tfsEx.StatusCode.Value));
    }

    private TimeSpan CalculateBackoffDelay(int attempt)
    {
        // Exponential backoff with jitter: 2^attempt seconds + random jitter up to 1 second
        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
        return baseDelay + jitter;
    }

    private static bool IsRetryableStatusCode(int statusCode)
    {
        return statusCode >= 500 || statusCode == 408;
    }

   private async Task HandleHttpErrorsAsync(HttpResponseMessage response, CancellationToken ct)
   {
      if (response.IsSuccessStatusCode) return;

      var body = response.Content != null
          ? await response.Content.ReadAsStringAsync(ct)
          : "<no body>";

      var request = response.RequestMessage;
      var url = request?.RequestUri?.ToString() ?? "<unknown url>";
      var method = request?.Method.Method ?? "<unknown method>";

      // Azure DevOps/TFS often returns an ActivityId header
      var activityId =
          response.Headers.TryGetValues("ActivityId", out var vals) ? string.Join(",", vals) :
          response.Headers.TryGetValues("X-TFS-Session", out var vals2) ? string.Join(",", vals2) :
          "<none>";

      var statusCode = (int)response.StatusCode;
      var retryAfter = GetRetryAfter(response);

      _logger.LogError(
          "TFS request failed. {Method} {Url} => {Status} {Reason}. ActivityId={ActivityId}. Body={Body}",
          method, url, statusCode, response.ReasonPhrase, activityId, body);

      var message = $"TFS request failed: {statusCode} {response.ReasonPhrase}. ActivityId={activityId}. Url={url}. Body={body}";

      if (response.StatusCode == HttpStatusCode.TooManyRequests)
      {
          throw new TfsRateLimitException(message, body, retryAfter);
      }

      throw new TfsException(message, statusCode, body);
   }

    private TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        return GetRetryAfter(response, DateTimeOffset.UtcNow);
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response, DateTimeOffset utcNow)
    {
        if (response.Headers.RetryAfter?.Delta.HasValue == true)
        {
            return response.Headers.RetryAfter.Delta.Value;
        }

        if (response.Headers.RetryAfter?.Date.HasValue == true)
        {
            var delay = response.Headers.RetryAfter.Date.Value - utcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private string EscapeWiql(string value)
    {
        return value.Replace("'", "''");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpClient httpClient,
        TfsConfigEntity config,
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool handleErrors = true)
    {
        return await _requestSender.SendAsync(
            httpClient,
            request,
            config.TimeoutSeconds,
            cancellationToken,
            handleErrors ? HandleHttpErrorsAsync : null);
    }

    private async Task<HttpResponseMessage> SendGetAsync(
        HttpClient httpClient,
        TfsConfigEntity config,
        string url,
        CancellationToken cancellationToken,
        bool handleErrors = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAsync(httpClient, config, request, cancellationToken, handleErrors);
    }

    private async Task<HttpResponseMessage> SendPostAsync(
        HttpClient httpClient,
        TfsConfigEntity config,
        string url,
        HttpContent content,
        CancellationToken cancellationToken,
        bool handleErrors = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        return await SendAsync(httpClient, config, request, cancellationToken, handleErrors);
    }

    private static string? GetContinuationToken(HttpResponseMessage response, JsonDocument doc)
    {
        if (response.Headers.TryGetValues("x-ms-continuationtoken", out var headerTokens))
        {
            return headerTokens.FirstOrDefault();
        }

        if (doc.RootElement.TryGetProperty("continuationToken", out var tokenElement) &&
            tokenElement.ValueKind == JsonValueKind.String)
        {
            return tokenElement.GetString();
        }

        return null;
    }

    private static string AddContinuationToken(string baseUrl, string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            return baseUrl;
        }

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}continuationToken={Uri.EscapeDataString(continuationToken)}";
    }

    private static string FormatUtcTimestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O");
    }
}
