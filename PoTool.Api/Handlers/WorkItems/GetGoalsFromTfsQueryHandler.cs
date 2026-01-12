using System.Net.Http;
using System.Text.Json;
using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Api.Services;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetGoalsFromTfsQuery.
/// Fetches goals directly from TFS using efficient WIQL query, bypassing the cache.
/// Used specifically for the Add Profile flow where cache is not yet populated.
/// </summary>
public sealed class GetGoalsFromTfsQueryHandler : IQueryHandler<GetGoalsFromTfsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TfsConfigurationService _configService;
    private readonly ILogger<GetGoalsFromTfsQueryHandler> _logger;

    // Minimal fields required for Goals selector (ID + Title only)
    private static readonly string[] MinimalGoalFields = new[]
    {
        "System.Id",
        "System.Title",
        "System.WorkItemType"
    };

    public GetGoalsFromTfsQueryHandler(
        IHttpClientFactory httpClientFactory,
        TfsConfigurationService configService,
        ILogger<GetGoalsFromTfsQueryHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configService = configService;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetGoalsFromTfsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching goals directly from TFS using WIQL query (cache bypass for Add Profile flow)");

        var config = await _configService.GetConfigEntityAsync(cancellationToken);

        if (config == null || string.IsNullOrWhiteSpace(config.DefaultAreaPath))
        {
            _logger.LogWarning("No TFS configuration or default area path found");
            return Enumerable.Empty<WorkItemDto>();
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("TfsClient.NTLM");

            // Step 1: Execute WIQL query to retrieve only Goal work item IDs
            var wiql = new
            {
                query = $"SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = '{EscapeWiql(WorkItemType.Goal)}' AND [System.AreaPath] UNDER '{EscapeWiql(config.DefaultAreaPath)}' ORDER BY [System.Title]"
            };

            var wiqlUrl = BuildProjectUrl(config, "_apis/wit/wiql");
            using var wiqlContent = new StringContent(
                JsonSerializer.Serialize(wiql),
                System.Text.Encoding.UTF8,
                "application/json");

            _logger.LogDebug("Executing WIQL query for Goals: {Query}", wiql.query);

            var wiqlResponse = await httpClient.PostAsync(wiqlUrl, wiqlContent, cancellationToken);

            if (!wiqlResponse.IsSuccessStatusCode)
            {
                var errorBody = await wiqlResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("WIQL query failed: HTTP {StatusCode}, Response: {ErrorBody}",
                    wiqlResponse.StatusCode, errorBody);

                // Check if this is a "work item type not found" error
                if (errorBody.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                    errorBody.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Goal work item type not found or not accessible in TFS project");
                }

                return Enumerable.Empty<WorkItemDto>();
            }

            using var wiqlStream = await wiqlResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var wiqlDoc = await JsonDocument.ParseAsync(wiqlStream, cancellationToken: cancellationToken);

            var goalIds = wiqlDoc.RootElement.GetProperty("workItems").EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32())
                .ToArray();

            if (goalIds.Length == 0)
            {
                _logger.LogInformation("No Goal work items found in TFS for area path: {AreaPath}", config.DefaultAreaPath);
                return Enumerable.Empty<WorkItemDto>();
            }

            _logger.LogDebug("Found {Count} Goal IDs, fetching minimal details (ID + Title)", goalIds.Length);

            // Step 2: Bulk-fetch minimal fields (ID + Title) for the Goal IDs
            var batchRequest = new
            {
                ids = goalIds,
                fields = MinimalGoalFields
            };

            var batchUrl = BuildCollectionUrl(config, "_apis/wit/workitemsbatch");
            using var batchContent = new StringContent(
                JsonSerializer.Serialize(batchRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            var batchResponse = await httpClient.PostAsync(batchUrl, batchContent, cancellationToken);

            if (!batchResponse.IsSuccessStatusCode)
            {
                var errorBody = await batchResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Batch fetch failed: HTTP {StatusCode}, Response: {ErrorBody}",
                    batchResponse.StatusCode, errorBody);
                return Enumerable.Empty<WorkItemDto>();
            }

            using var batchStream = await batchResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var batchDoc = await JsonDocument.ParseAsync(batchStream, cancellationToken: cancellationToken);

            // Parse the work items into DTOs
            var goals = new List<WorkItemDto>();
            foreach (var item in batchDoc.RootElement.GetProperty("value").EnumerateArray())
            {
                var fields = item.GetProperty("fields");
                var id = item.GetProperty("id").GetInt32();
                var title = fields.GetProperty("System.Title").GetString() ?? string.Empty;

                goals.Add(new WorkItemDto(
                    TfsId: id,
                    Type: WorkItemType.Goal,
                    Title: title,
                    ParentTfsId: null,  // Not needed for Goals selector
                    AreaPath: string.Empty,  // Not needed for Goals selector
                    IterationPath: string.Empty,  // Not needed for Goals selector
                    State: string.Empty,  // Not needed for Goals selector
                    JsonPayload: string.Empty,  // Not needed for Goals selector
                    RetrievedAt: DateTimeOffset.UtcNow,
                    Effort: null,  // Not needed for Goals selector
                    Description: null  // Not needed for Goals selector
                ));
            }

            _logger.LogInformation("Successfully fetched {Count} Goals from TFS", goals.Count);

            return goals;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Goal fetch operation was cancelled");
            return Enumerable.Empty<WorkItemDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching goals from TFS");
            return Enumerable.Empty<WorkItemDto>();
        }
    }

    private string EscapeWiql(string value)
    {
        return value.Replace("'", "''");
    }

    private string BuildCollectionUrl(PoTool.Api.Persistence.Entities.TfsConfigEntity config, string relativePath)
    {
        var path = relativePath.TrimStart('/');
        var separator = path.Contains('?') ? "&" : "?";
        return $"{config.Url.TrimEnd('/')}/{path}{separator}api-version={config.ApiVersion}";
    }

    private string BuildProjectUrl(PoTool.Api.Persistence.Entities.TfsConfigEntity config, string relativePath)
    {
        var encodedProject = Uri.EscapeDataString(config.Project);
        var path = relativePath.TrimStart('/');
        var separator = path.Contains('?') ? "&" : "?";
        return $"{config.Url.TrimEnd('/')}/{encodedProject}/{path}{separator}api-version={config.ApiVersion}";
    }
}
