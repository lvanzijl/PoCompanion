using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.PullRequests;
using PoTool.Core.Exceptions;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Services;

/// <summary>
/// Azure DevOps/TFS REST client implementation with retry logic and enhanced error handling.
/// Supports Azure DevOps Server 2022.2 (API 7.0) and TFS 2019+ (API 5.1+).
/// </summary>
public class TfsClient : ITfsClient
{
    private readonly HttpClient _httpClient;
    private readonly TfsConfigurationService _configService;
    private readonly TfsAuthenticationProvider _authProvider;
    private readonly ILogger<TfsClient> _logger;
    private const int MaxRetries = 3;

    public TfsClient(
        HttpClient httpClient, 
        TfsConfigurationService configService, 
        TfsAuthenticationProvider authProvider,
        ILogger<TfsClient> logger)
    {
        _httpClient = httpClient;
        _configService = configService;
        _authProvider = authProvider;
        _logger = logger;
    }

    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("No TFS configuration found for validation");
            return false;
        }

        try
        {
            await ConfigureAuthenticationAsync(entity, cancellationToken);
            
            var url = $"{entity.Url.TrimEnd('/')}/_apis/projects?api-version={entity.ApiVersion}";
            var resp = await _httpClient.GetAsync(url, cancellationToken);
            
            _logger.LogInformation("Validation GET {Url} returned {StatusCode}", url, resp.StatusCode);
            
            if (resp.IsSuccessStatusCode)
            {
                // Update last validated timestamp
                entity.LastValidated = DateTimeOffset.UtcNow;
                await _configService.SaveConfigEntityAsync(entity, cancellationToken);
                return true;
            }
            
            return false;
        }
        catch (TfsAuthenticationException ex)
        {
            _logger.LogError(ex, "Authentication failed during TFS connection validation");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating TFS connection");
            return false;
        }
    }

    public async Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        if (entity == null)
            throw new InvalidOperationException("TFS configuration not set");

        await ConfigureAuthenticationAsync(entity, cancellationToken);

        return await ExecuteWithRetryAsync(async () =>
        {
            // Build WIQL query to find work items under area path
            var wiql = new
            {
                query = $"Select [System.Id], [System.WorkItemType], [System.Title], [System.State] From WorkItems Where [System.AreaPath] = '{EscapeWiql(areaPath)}'"
            };

            var wiqlUrl = $"{entity.Url.TrimEnd('/')}/_apis/wit/wiql?api-version={entity.ApiVersion}";
            using var content = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");

            var wiqlResponse = await _httpClient.PostAsync(wiqlUrl, content, cancellationToken);
            await HandleHttpErrorsAsync(wiqlResponse, cancellationToken);

            using var stream = await wiqlResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var ids = doc.RootElement.GetProperty("workItems").EnumerateArray()
                        .Select(e => e.GetProperty("id").GetInt32())
                        .ToArray();

            if (ids.Length == 0)
                return Enumerable.Empty<WorkItemDto>();

            // Batch get work items - use $expand=All to get all fields including System.Parent
            var idsQuery = string.Join(',', ids);
            var itemsUrl = $"{entity.Url.TrimEnd('/')}/_apis/wit/workitems?ids={idsQuery}&$expand=All&api-version={entity.ApiVersion}";
            var itemsResponse = await _httpClient.GetAsync(itemsUrl, cancellationToken);
            await HandleHttpErrorsAsync(itemsResponse, cancellationToken);

            using var itemsStream = await itemsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var itemsDoc = await JsonDocument.ParseAsync(itemsStream, cancellationToken: cancellationToken);

            var results = new List<WorkItemDto>();

            foreach (var item in itemsDoc.RootElement.GetProperty("value").EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var fields = item.GetProperty("fields");
                var type = fields.TryGetProperty("System.WorkItemType", out var t) ? t.GetString() ?? "" : "";
                var title = fields.TryGetProperty("System.Title", out var ti) ? ti.GetString() ?? "" : "";
                var state = fields.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "";
                var area = fields.TryGetProperty("System.AreaPath", out var a) ? a.GetString() ?? "" : "";
                var iteration = fields.TryGetProperty("System.IterationPath", out var ip) ? ip.GetString() ?? "" : "";
                
                // Extract parent work item ID if present
                int? parentId = null;
                if (fields.TryGetProperty("System.Parent", out var parent) && parent.ValueKind == JsonValueKind.String)
                {
                    var parentUrl = parent.GetString();
                    if (!string.IsNullOrEmpty(parentUrl))
                    {
                        var segments = parentUrl.Split('/');
                        if (segments.Length > 0 && int.TryParse(segments[^1], out var parsedId))
                        {
                            parentId = parsedId;
                        }
                    }
                }

                results.Add(new WorkItemDto(
                    TfsId: id,
                    Type: type,
                    Title: title,
                    ParentTfsId: parentId,
                    AreaPath: area,
                    IterationPath: iteration,
                    State: state,
                    JsonPayload: item.GetRawText(),
                    RetrievedAt: DateTimeOffset.UtcNow,
                    Effort: null
                ));
            }

            _logger.LogInformation("Retrieved {Count} work items for areaPath={AreaPath}", results.Count, areaPath);
            return results;
        }, cancellationToken);
    }

    // Pull Request methods - placeholder implementations for Phase 2
    public Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetPullRequestsAsync not yet implemented - returning empty collection");
        return Task.FromResult(Enumerable.Empty<PullRequestDto>());
    }

    public Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetPullRequestIterationsAsync not yet implemented - returning empty collection");
        return Task.FromResult(Enumerable.Empty<PullRequestIterationDto>());
    }

    public Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetPullRequestCommentsAsync not yet implemented - returning empty collection");
        return Task.FromResult(Enumerable.Empty<PullRequestCommentDto>());
    }

    public Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(
        int pullRequestId,
        string repositoryName,
        int iterationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetPullRequestFileChangesAsync not yet implemented - returning empty collection");
        return Task.FromResult(Enumerable.Empty<PullRequestFileChangeDto>());
    }

    // Private helper methods

    private async Task ConfigureAuthenticationAsync(TfsConfigEntity entity, CancellationToken cancellationToken)
    {
        if (entity.AuthMode == TfsAuthMode.Pat)
        {
            var pat = _configService.UnprotectPatEntity(entity);
            if (string.IsNullOrEmpty(pat))
                throw new TfsAuthenticationException("PAT not configured", (string?)null);

            _authProvider.ConfigurePatAuthentication(_httpClient, pat);
        }
        // NTLM is configured via HttpClientHandler, so no additional configuration needed here
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken, int maxRetries = MaxRetries)
    {
        int attempt = 0;
        
        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxRetries)
            {
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

    private async Task HandleHttpErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        var exception = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new TfsAuthenticationException(
                "TFS authentication failed. Check your PAT or credentials.", errorContent),
            HttpStatusCode.Forbidden => new TfsAuthorizationException(
                "TFS authorization failed. Insufficient permissions.", errorContent),
            HttpStatusCode.NotFound => new TfsResourceNotFoundException(
                "TFS resource not found. Check project name and URL.", errorContent),
            HttpStatusCode.TooManyRequests => new TfsRateLimitException(
                "TFS rate limit exceeded. Please try again later.", errorContent, 
                GetRetryAfter(response)),
            _ when (int)response.StatusCode >= 500 => new TfsException(
                $"TFS server error: {response.StatusCode}", (int)response.StatusCode, errorContent),
            _ => new TfsException(
                $"TFS request failed: {response.StatusCode}", (int)response.StatusCode, errorContent)
        };

        _logger.LogError("TFS HTTP error: {StatusCode} - {Message}", response.StatusCode, exception.Message);
        throw exception;
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
