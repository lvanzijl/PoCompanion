using System.Net.Http.Headers;
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
            var repoResponse = await httpClient.GetAsync(repoUrl, cancellationToken);

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
        var response = await httpClient.GetAsync(url, cancellationToken);
        await HandleHttpErrorsAsync(response, cancellationToken);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var repositories = new List<(string Name, string Id)>();

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
            || (ex is TfsException tfsEx && tfsEx.StatusCode >= 500);
    }

    private TimeSpan CalculateBackoffDelay(int attempt)
    {
        // Exponential backoff with jitter: 2^attempt seconds + random jitter up to 1 second
        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
        return baseDelay + jitter;
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

      _logger.LogError(
          "TFS request failed. {Method} {Url} => {(int)Status} {Reason}. ActivityId={ActivityId}. Body={Body}",
          method, url, (int)response.StatusCode, response.ReasonPhrase, activityId, body);

      throw new TfsException(
          $"TFS request failed: {(int)response.StatusCode} {response.ReasonPhrase}. ActivityId={activityId}. Url={url}. Body={body}");
   }

   private TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta.HasValue == true)
        {
            return response.Headers.RetryAfter.Delta.Value;
        }
        return null;
    }

    private string EscapeWiql(string value)
    {
        return value.Replace("'", "''");
    }
}
