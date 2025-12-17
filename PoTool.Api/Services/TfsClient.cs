using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Api.Persistence.Entities;

namespace PoTool.Api.Services;

/// <summary>
/// Basic Azure DevOps/TFS REST client implementation using HttpClient.
/// This implementation expects configuration to be provided by `TfsConfigurationService`.
/// It supports a minimal query to fetch work items by area path.
/// </summary>
public class TfsClient : ITfsClient
{
    private readonly HttpClient _httpClient;
    private readonly TfsConfigurationService _configService;
    private readonly ILogger<TfsClient> _logger;

    public TfsClient(HttpClient httpClient, TfsConfigurationService configService, ILogger<TfsClient> logger)
    {
        _httpClient = httpClient;
        _configService = configService;
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

        var pat = _configService.UnprotectPatEntity(entity);
        if (string.IsNullOrEmpty(pat))
        {
            _logger.LogWarning("PAT not available for validation");
            return false;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($":{pat}")));

        try
        {
            var url = entity.Url.TrimEnd('/') + "/_apis/projects?api-version=7.0";
            var resp = await _httpClient.GetAsync(url, cancellationToken);
            _logger.LogInformation("Validation GET {Url} returned {StatusCode}", url, resp.StatusCode);
            return resp.IsSuccessStatusCode;
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

        var pat = _configService.UnprotectPatEntity(entity);
        if (string.IsNullOrEmpty(pat))
            throw new InvalidOperationException("PAT not configured");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($":{pat}")));

        // Build WIQL query to find work items under area path
        // Note: System.Parent is not directly queryable in WIQL, but we can retrieve it via fields
        var wiql = new
        {
            query = $"Select [System.Id], [System.WorkItemType], [System.Title], [System.State] From WorkItems Where [System.AreaPath] = '{areaPath.Replace("'", "''")}'"
        };

        var wiqlUrl = entity.Url.TrimEnd('/') + "/_apis/wit/wiql?api-version=7.0";
        using var content = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");

        try
        {
            var wiqlResponse = await _httpClient.PostAsync(wiqlUrl, content, cancellationToken);
            wiqlResponse.EnsureSuccessStatusCode();

            using var stream = await wiqlResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var ids = doc.RootElement.GetProperty("workItems").EnumerateArray()
                        .Select(e => e.GetProperty("id").GetInt32())
                        .ToArray();

            if (ids.Length == 0)
                return Enumerable.Empty<WorkItemDto>();

            // Batch get work items
            // Use $expand=All to get all fields including System.Parent
            var idsQuery = string.Join(',', ids);
            var itemsUrl = entity.Url.TrimEnd('/') + $"/_apis/wit/workitems?ids={idsQuery}&$expand=All&api-version=7.0";
            var itemsResponse = await _httpClient.GetAsync(itemsUrl, cancellationToken);
            itemsResponse.EnsureSuccessStatusCode();

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
                // System.Parent is returned as a URL string like "https://dev.azure.com/org/_apis/wit/workItems/12345"
                int? parentId = null;
                if (fields.TryGetProperty("System.Parent", out var parent) && parent.ValueKind == JsonValueKind.String)
                {
                    var parentUrl = parent.GetString();
                    if (!string.IsNullOrEmpty(parentUrl))
                    {
                        // Extract ID from URL (last segment)
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
                    RetrievedAt: DateTimeOffset.UtcNow
                ));
            }

            _logger.LogInformation("Retrieved {Count} work items for areaPath={AreaPath}", results.Count, areaPath);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching work items for areaPath={AreaPath}", areaPath);
            throw;
        }
    }
}
