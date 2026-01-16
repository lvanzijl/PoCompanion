using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Contracts;
using PoTool.Core.Pipelines;
using PoTool.Core.PullRequests;
using PoTool.Core.WorkItems;
using PoTool.Shared.Contracts.TfsVerification;
using PoTool.Shared.Exceptions;
using PoTool.Shared.Pipelines;
using PoTool.Shared.PullRequests;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Request payload for Azure DevOps Work Items Batch API.
/// Used with POST _apis/wit/workitemsbatch to retrieve multiple work items efficiently.
/// </summary>
internal sealed class WorkItemBatchRequest
{
    /// <summary>
    /// Array of work item IDs to retrieve.
    /// Required for valid API requests.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("ids")]
    public required int[] Ids { get; init; }

    /// <summary>
    /// Optional array of field reference names to retrieve.
    /// If not specified, all fields are returned.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("fields")]
   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public string[]? Fields { get; init; }

    /// <summary>
    /// Optional expansion for additional data (e.g., "relations").
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("$expand")]
   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public string? Expand { get; init; }
}

/// <summary>
/// Real Azure DevOps/TFS REST client implementation with retry logic and enhanced error handling.
/// Supports Azure DevOps Server 2022.2 (API 7.0) and TFS 2019+ (API 5.1+).
/// This is the production implementation that connects to actual Azure DevOps/TFS servers.
/// </summary>
public class RealTfsClient : ITfsClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TfsConfigurationService _configService;
    private readonly ILogger<RealTfsClient> _logger;
    private readonly TfsRequestThrottler _throttler;
    private readonly TfsRequestSender _requestSender;
    private const int MaxRetries = 3;

    // ID offset for release pipelines/runs to avoid collision with build IDs
    private const int ReleaseIdOffset = 100000;

    // TFS field paths
    private const string TfsFieldEffort = "Microsoft.VSTS.Scheduling.Effort";
    private const string TfsFieldStoryPoints = "Microsoft.VSTS.Scheduling.StoryPoints";
    private const string TfsFieldState = "System.State";

    // Required work item fields for queries
    private static readonly string[] RequiredWorkItemFields = new[]
    {
        "System.Id",
        "System.WorkItemType",
        "System.Title",
        "System.State",
        "System.AreaPath",
        "System.IterationPath",
        "System.Description",
        TfsFieldEffort,
        TfsFieldStoryPoints
    };

    // Batch size for Work Items Batch API calls
    // Azure DevOps supports up to 200 work items per batch for optimal performance
    // Larger batches (up to 500) may work but could impact response time
    internal const int WorkItemBatchSize = 200;

    // Ancestor completion safety limits
    // MaxAncestorDepth: Prevents infinite loops in case of circular references or very deep hierarchies
    // Typical org hierarchies: Goal (1) → Objective (2) → Epic (3) → Feature (4) → PBI (5) → Task (6) = 6 levels
    // Setting to 20 provides comfortable headroom while preventing runaway scenarios
    private const int MaxAncestorDepth = 20;
    
    // MaxAncestorCount: Caps total ancestors to add, preventing excessive API calls
    // In practice, most hierarchies have < 100 ancestors
    // Setting to 1000 handles large org structures while maintaining reasonable performance
    private const int MaxAncestorCount = 1000;

    public RealTfsClient(
        IHttpClientFactory httpClientFactory,
        TfsConfigurationService configService,
        ILogger<RealTfsClient> logger,
        TfsRequestThrottler throttler,
        TfsRequestSender requestSender)
    {
        _httpClientFactory = httpClientFactory;
        _configService = configService;
        _logger = logger;
        _throttler = throttler;
        _requestSender = requestSender;
    }

    /// <summary>
    /// Gets an HttpClient configured for NTLM authentication.
    /// Uses named HttpClient from IHttpClientFactory to ensure correct handler configuration.
    /// Per-request timeouts are handled via CancellationToken, not HttpClient.Timeout property.
    /// </summary>
    /// <returns>Configured HttpClient with NTLM authentication.</returns>
    private HttpClient GetAuthenticatedHttpClient()
    {
        // Get NTLM-configured client (with UseDefaultCredentials=true in handler)
        var client = _httpClientFactory.CreateClient("TfsClient.NTLM");

        // NOTE: Do NOT set client.Timeout here - factory-managed clients should not have their
        // timeout mutated. Per-request timeouts are handled via CancellationTokenSource.

        _logger.LogDebug("Using NTLM-authenticated HttpClient for TFS request");

        return client;
    }

    /// <summary>
    /// Builds a collection-scoped URL (no project in path).
    /// Use for: _apis/projects, _apis/wit/fields, _apis/wit/workitems?ids=...
    /// </summary>
    /// <param name="config">TFS configuration entity.</param>
    /// <param name="relativePath">Path relative to collection root (e.g., "_apis/projects").</param>
    /// <returns>Full URL including api-version.</returns>
    private string CollectionUrl(TfsConfigEntity config, string relativePath)
    {
        ValidateCollectionUrl(config.Url);

        // Ensure relativePath doesn't start with /
        var path = relativePath.TrimStart('/');
        var separator = path.Contains('?') ? "&" : "?";
        return $"{config.Url.TrimEnd('/')}/{path}{separator}api-version={config.ApiVersion}";
    }

    /// <summary>
    /// Builds a project-scoped URL (project in path).
    /// Use for: WIQL, Git repositories, pull requests, build/release pipelines.
    /// Project name is URL-encoded to support spaces.
    /// </summary>
    /// <param name="config">TFS configuration entity.</param>
    /// <param name="relativePath">Path relative to project (e.g., "_apis/wit/wiql").</param>
    /// <returns>Full URL including api-version.</returns>
    private string ProjectUrl(TfsConfigEntity config, string relativePath)
    {
        ValidateCollectionUrl(config.Url);

        // URL-encode project name to support spaces and special characters
        var encodedProject = Uri.EscapeDataString(config.Project);
        // Ensure relativePath doesn't start with /
        var path = relativePath.TrimStart('/');
        var separator = path.Contains('?') ? "&" : "?";
        return $"{config.Url.TrimEnd('/')}/{encodedProject}/{path}{separator}api-version={config.ApiVersion}";
    }

    /// <summary>
    /// Validates that the TFS URL is a collection root (not a project URL).
    /// Expected format: https://server/tfs/DefaultCollection or https://dev.azure.com/org
    /// </summary>
    /// <param name="url">The TFS URL to validate.</param>
    private void ValidateCollectionUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new TfsConfigurationException("TFS URL cannot be empty");
        }

        // Basic validation that it's a valid URI
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new TfsConfigurationException($"TFS URL is not a valid absolute URI: {url}");
        }

        // Check for common mistakes - project URLs typically have _apis or project-specific paths
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Contains("/_apis/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "TFS URL appears to include an API path segment (_apis). " +
                "Expected collection root (e.g., https://server/tfs/DefaultCollection), got: {Url}", url);
        }

        // For Azure DevOps Services, validate format
        if (uri.Host.EndsWith("visualstudio.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1)
            {
                _logger.LogWarning(
                    "Azure DevOps URL may include project name in path. " +
                    "Expected organization root (e.g., https://dev.azure.com/org), got: {Url}", url);
            }
        }
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
            // Use auth-mode-specific HttpClient to avoid credential conflicts
            var httpClient = GetAuthenticatedHttpClient();

            // Create per-request timeout token
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(entity.TimeoutSeconds));

            // Step 1: Validate server connectivity using collection-scoped projects endpoint
            var projectsUrl = CollectionUrl(entity, "_apis/projects");
            _logger.LogInformation("Validating TFS connection: GET {Url} (using NTLM authentication)", projectsUrl);

            var resp = await httpClient.GetAsync(projectsUrl, timeoutCts.Token);

            _logger.LogInformation("Validation GET {Url} returned {StatusCode}", projectsUrl, resp.StatusCode);

            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("TFS connection validation failed: HTTP {StatusCode}, Response: {ErrorBody}",
                    resp.StatusCode, errorBody);
                return false;
            }

            // Step 2: Validate that the configured project exists (requirement #6)
            var encodedProject = Uri.EscapeDataString(entity.Project);
            var projectUrl = CollectionUrl(entity, $"_apis/projects/{encodedProject}");
            _logger.LogInformation("Validating project access: GET {Url}", projectUrl);

            var projectResp = await httpClient.GetAsync(projectUrl, timeoutCts.Token);

            if (!projectResp.IsSuccessStatusCode)
            {
                if (projectResp.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogError("Project '{Project}' does not exist or is not accessible", entity.Project);
                }
                else
                {
                    var errorBody = await projectResp.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Project validation failed: HTTP {StatusCode}, Response: {ErrorBody}",
                        projectResp.StatusCode, errorBody);
                }
                return false;
            }

            // Store confirmed project info from response
            using var stream = await projectResp.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var confirmedProjectName = doc.RootElement.GetProperty("name").GetString();
            _logger.LogInformation("Project validated: {ProjectName}", confirmedProjectName);

            // Update last validated timestamp
            entity.LastValidated = DateTimeOffset.UtcNow;
            await _configService.SaveConfigEntityAsync(entity, cancellationToken);
            _logger.LogInformation("TFS connection validation successful");
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("TFS connection validation timed out after {Timeout}s", entity.TimeoutSeconds);
            return false;
        }
        catch (TfsAuthenticationException ex)
        {
            _logger.LogError(ex, "Authentication failed during TFS connection validation");
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during TFS connection validation");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during TFS connection validation");
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetAreaPathsAsync(int? depth = null, CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);

        // Null assertion after validation - entity is guaranteed non-null here
        var config = entity!;

        // Get auth-mode-specific HttpClient to avoid credential conflicts
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Fetching area paths from TFS Classification Nodes API");

            // Build Classification Nodes API URL
            // Format: GET {organization}/{project}/_apis/wit/classificationnodes/areas?$depth={depth}&api-version=5.1
            var depthParam = depth.HasValue ? $"$depth={depth.Value}" : "";
            var url = ProjectUrl(config, $"_apis/wit/classificationnodes/areas?{depthParam}");

            _logger.LogDebug("Calling Classification Nodes API: {Url}", url);

            var response = await httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            // Parse the hierarchical area path tree and flatten it to a list of full paths
            var areaPaths = new List<string>();

            if (doc.RootElement.TryGetProperty("name", out var rootName))
            {
                var rootPath = rootName.GetString() ?? "";

                // Add root area path
                areaPaths.Add(rootPath);

                // Recursively extract child area paths
                if (doc.RootElement.TryGetProperty("children", out var children))
                {
                    ExtractAreaPathsRecursive(children, rootPath, areaPaths);
                }
            }

            _logger.LogInformation("Retrieved {Count} area paths from TFS Classification Nodes API", areaPaths.Count);

            return areaPaths.OrderBy(ap => ap).AsEnumerable();
        }, cancellationToken);
    }

    /// <summary>
    /// Recursively extracts area paths from the Classification Nodes API response tree.
    /// </summary>
    /// <param name="children">The children array from the Classification Nodes response.</param>
    /// <param name="parentPath">The parent path prefix.</param>
    /// <param name="areaPaths">The list to accumulate area paths.</param>
    private void ExtractAreaPathsRecursive(JsonElement children, string parentPath, List<string> areaPaths)
    {
        if (children.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var child in children.EnumerateArray())
        {
            if (child.TryGetProperty("name", out var childName))
            {
                var name = childName.GetString() ?? "";
                var fullPath = $"{parentPath}\\{name}";

                areaPaths.Add(fullPath);

                // Recursively process children of this node
                if (child.TryGetProperty("children", out var grandchildren))
                {
                    ExtractAreaPathsRecursive(grandchildren, fullPath, areaPaths);
                }
            }
        }
    }

    public async Task<WorkItemDto?> GetWorkItemByIdAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);

        // Null assertion after validation - entity is guaranteed non-null here
        var config = entity!;

        // Get auth-mode-specific HttpClient to avoid credential conflicts
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Fetching work item {WorkItemId} directly from TFS", workItemId);

            // TFS Server 2022 limitation: Cannot combine $expand=relations with fields= parameter
            // Apply two-phase retrieval strategy (same as GetWorkItemsAsync)
            // Phase 1: Fetch relations only to get parent ID
            // Phase 2: Fetch fields only to get work item data

            var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");

            // Phase 1: Fetch relations to get parent ID
            _logger.LogDebug("Phase 1: Fetching relations for work item {WorkItemId}", workItemId);
            var relationsRequest = new WorkItemBatchRequest
            {
                Ids = new[] { workItemId },
                Expand = "relations" // Only expand relations, no fields
            };

            using var relationsContent = new StringContent(
                JsonSerializer.Serialize(relationsRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            var relationsResponse = await httpClient.PostAsync(batchUrl, relationsContent, cancellationToken);
            
            // If work item not found, return null instead of throwing
            if (relationsResponse.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Work item {WorkItemId} not found in TFS", workItemId);
                return null;
            }

            await HandleHttpErrorsAsync(relationsResponse, cancellationToken);

            using var relationsStream = await relationsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var relationsDoc = await JsonDocument.ParseAsync(relationsStream, cancellationToken: cancellationToken);

            // Check if any work items were returned
            if (!relationsDoc.RootElement.TryGetProperty("value", out var relationsArray) || relationsArray.ValueKind != JsonValueKind.Array)
            {
                _logger.LogDebug("Work item {WorkItemId} not found in TFS response", workItemId);
                return null;
            }

            var relationsItems = relationsArray.EnumerateArray().ToList();
            if (relationsItems.Count == 0)
            {
                _logger.LogDebug("Work item {WorkItemId} not found in TFS", workItemId);
                return null;
            }

            // Extract parent ID from relations immediately (before document disposal)
            var relationsItem = relationsItems[0];
            var parentId = ExtractParentIdFromRelations(relationsItem);

            // Phase 2: Fetch fields to get work item data
            _logger.LogDebug("Phase 2: Fetching fields for work item {WorkItemId}", workItemId);
            var fieldsRequest = new WorkItemBatchRequest
            {
                Ids = new[] { workItemId },
                Fields = RequiredWorkItemFields // Only fetch fields, no expand
            };

            using var fieldsContent = new StringContent(
                JsonSerializer.Serialize(fieldsRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            var fieldsResponse = await httpClient.PostAsync(batchUrl, fieldsContent, cancellationToken);
            
            // If work item not found in Phase 2, return null
            if (fieldsResponse.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Work item {WorkItemId} not found in TFS fields response", workItemId);
                return null;
            }

            await HandleHttpErrorsAsync(fieldsResponse, cancellationToken);

            using var fieldsStream = await fieldsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var fieldsDoc = await JsonDocument.ParseAsync(fieldsStream, cancellationToken: cancellationToken);

            // Check if any work items were returned
            if (!fieldsDoc.RootElement.TryGetProperty("value", out var fieldsArray) || fieldsArray.ValueKind != JsonValueKind.Array)
            {
                _logger.LogDebug("Work item {WorkItemId} not found in TFS fields response", workItemId);
                return null;
            }

            var fieldsItems = fieldsArray.EnumerateArray().ToList();
            if (fieldsItems.Count == 0)
            {
                _logger.LogDebug("Work item {WorkItemId} not found in TFS fields response", workItemId);
                return null;
            }

            var item = fieldsItems[0];
            var id = item.GetProperty("id").GetInt32();
            var fields = item.GetProperty("fields");
            var type = fields.TryGetProperty("System.WorkItemType", out var t) ? t.GetString() ?? "" : "";
            var title = fields.TryGetProperty("System.Title", out var ti) ? ti.GetString() ?? "" : "";
            var state = fields.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "";
            var area = fields.TryGetProperty("System.AreaPath", out var a) ? a.GetString() ?? "" : "";
            var iteration = fields.TryGetProperty("System.IterationPath", out var ip) ? ip.GetString() ?? "" : "";
            var description = fields.TryGetProperty("System.Description", out var d) ? d.GetString() : null;

            // Extract effort field with robust parsing
            int? effort = ParseEffortField(fields);

            var workItem = new WorkItemDto(
                TfsId: id,
                Type: type,
                Title: title,
                ParentTfsId: parentId, // Use parent ID from Phase 1
                AreaPath: area,
                IterationPath: iteration,
                State: state,
                JsonPayload: item.GetRawText(), // Note: Contains fields only, not relations (TFS Server 2022 limitation)
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: effort,
                Description: description
            );

            _logger.LogInformation("Retrieved work item {WorkItemId} from TFS: {Title}", id, title);
            return workItem;
        }, cancellationToken);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        return GetWorkItemsAsync(areaPath, since: null, cancellationToken);
    }

    public async Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        // NOTE: This method fetches work items by area path, not by hierarchy.
        // It uses the same two-phase retrieval (relations then fields) to extract parent IDs.
        // Unlike GetWorkItemsByRootIdsAsync, this does NOT complete ancestors because:
        // 1. Area path queries are flat - they return all items under the area
        // 2. Parent items should naturally be in the same area path in most cases
        // 3. If parents are in a different area, they're intentionally excluded from this query scope
        // For hierarchy-based queries with ancestor completion, use GetWorkItemsByRootIdsAsync.
        
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);

        // Null assertion after validation - entity is guaranteed non-null here
        var config = entity!;

        // Get auth-mode-specific HttpClient to avoid credential conflicts
        var httpClient = GetAuthenticatedHttpClient();

        var startTime = DateTimeOffset.UtcNow; // Phase 4: Performance metrics

        return await ExecuteWithRetryAsync(async () =>
        {
            // Build WIQL query with optional date filter for incremental sync (Phase 3)
            // Use UNDER operator for area path to support deeper hierarchies (requirement #3)
            // Note: WIQL Select only needs System.Id since we fetch full work items in a separate batch call
            // with all RequiredWorkItemFields. The other fields here are for debugging/logging purposes.
            var dateFilter = since.HasValue
                ? $" AND [System.ChangedDate] >= '{since.Value:yyyy-MM-ddTHH:mm:ssZ}'"
                : "";

            var wiql = new
            {
                query = $"Select [System.Id] From WorkItems Where [System.AreaPath] UNDER '{EscapeWiql(areaPath)}'{dateFilter}"
            };

            // WIQL is project-scoped (requirement #1)
            var wiqlUrl = ProjectUrl(config, "_apis/wit/wiql");
            using var content = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");

            // Phase 4: Enhanced logging
            _logger.LogDebug("Executing WIQL query: {Query}", wiql.query);

            var wiqlResponse = await httpClient.PostAsync(wiqlUrl, content, cancellationToken);
            await HandleHttpErrorsAsync(wiqlResponse, cancellationToken);

            using var stream = await wiqlResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var ids = doc.RootElement.GetProperty("workItems").EnumerateArray()
                        .Select(e => e.GetProperty("id").GetInt32())
                        .ToArray();

            if (ids.Length == 0)
            {
                _logger.LogInformation("No work items found for areaPath={AreaPath}, since={Since}", areaPath, since);
                return Enumerable.Empty<WorkItemDto>();
            }

            _logger.LogDebug("Found {Count} work item IDs, fetching details using two-phase retrieval", ids.Length);

            // TFS Server 2022 limitation: Cannot combine $expand=relations with fields= parameter
            // 
            // IMPLEMENTATION: Two-phase retrieval strategy
            // Phase 1: Fetch relations only (no fields parameter) to extract parent hierarchy
            // Phase 2: Fetch fields only (no expand parameter) to get work item data
            // Then merge by work item ID
            //
            // RATIONALE: This approach is required for TFS Server 2022 compatibility.
            // Azure DevOps Services does not have this limitation, but we maintain this
            // approach for cross-platform compatibility.
            //
            // PERFORMANCE CONSIDERATIONS (Phase 4.3):
            // - Two API calls per batch vs one combined call
            // - Tradeoff: Correctness (works on TFS 2022) vs Performance (fewer calls)
            // - Optimization opportunity: Skip Phase 1 for types without parent relationships
            // - Current batch size: 200 items per batch (WorkItemBatchSize constant)
            // - Could increase batch size for relations-only phase (typically smaller payload)
            //
            // FUTURE OPTIMIZATION: Make Phase 1 conditional based on work item types
            // that actually use parent relationships (Epic, Feature, User Story, Task).
            // Would require: Type detection in WIQL results, conditional Phase 1 execution.

            var totalBatches = (int)Math.Ceiling((double)ids.Length / WorkItemBatchSize);
            _logger.LogInformation("Fetching {TotalIds} work items in {BatchCount} batches of {BatchSize} (two-phase retrieval)",
                ids.Length, totalBatches, WorkItemBatchSize);

            // Phase 1: Fetch relations to get parent IDs
            var relationsMap = new Dictionary<int, int?>();
            var relationsPresent = 0;
            var reverseLinksPresent = 0;
            var parentIdsExtracted = 0;

            for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var batchIds = ids.Skip(batchIndex * WorkItemBatchSize).Take(WorkItemBatchSize).ToArray();

                _logger.LogDebug("Phase 1 (relations): Processing batch {BatchIndex}/{TotalBatches} with {IdCount} IDs",
                    batchIndex + 1, totalBatches, batchIds.Length);

                var relationsRequest = new WorkItemBatchRequest
                {
                    Ids = batchIds,
                    Expand = "relations" // Only expand relations, no fields
                };

                var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");
                using var relationsContent = new StringContent(
                    JsonSerializer.Serialize(relationsRequest),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var relationsResponse = await httpClient.PostAsync(batchUrl, relationsContent, cancellationToken);
                await HandleHttpErrorsAsync(relationsResponse, cancellationToken);

                using var relationsStream = await relationsResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var relationsDoc = await JsonDocument.ParseAsync(relationsStream, cancellationToken: cancellationToken);

                // Extract parent IDs from relations
                foreach (var item in relationsDoc.RootElement.GetProperty("value").EnumerateArray())
                {
                    var id = item.GetProperty("id").GetInt32();

                    // Check if relations are present
                    if (item.TryGetProperty("relations", out var relations) && relations.ValueKind == JsonValueKind.Array)
                    {
                        relationsPresent++;

                        // Count reverse hierarchy links
                        foreach (var rel in relations.EnumerateArray())
                        {
                            if (rel.TryGetProperty("rel", out var relType))
                            {
                                var relTypeStr = relType.GetString();
                                if (string.Equals(relTypeStr, "System.LinkTypes.Hierarchy-Reverse", StringComparison.OrdinalIgnoreCase))
                                {
                                    reverseLinksPresent++;
                                    break;
                                }
                            }
                        }
                    }

                    var parentId = ExtractParentIdFromRelations(item);
                    relationsMap[id] = parentId;

                    if (parentId.HasValue)
                    {
                        parentIdsExtracted++;
                    }
                }

                _logger.LogDebug(
                    "Phase 1 batch {BatchIndex}/{TotalBatches} completed: {IdCount} IDs, HTTP {StatusCode}",
                    batchIndex + 1, totalBatches, batchIds.Length, (int)relationsResponse.StatusCode);
            }

            _logger.LogInformation(
                "Phase 1 complete: Relations present: {RelationsCount}, Reverse links: {ReverseLinksCount}, Parent IDs extracted: {ParentIdsCount}",
                relationsPresent, reverseLinksPresent, parentIdsExtracted);

            // Phase 2: Fetch fields for all work items
            var results = new List<WorkItemDto>();

            for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var batchStartTime = DateTimeOffset.UtcNow;
                var batchIds = ids.Skip(batchIndex * WorkItemBatchSize).Take(WorkItemBatchSize).ToArray();

                _logger.LogDebug("Phase 2 (fields): Processing batch {BatchIndex}/{TotalBatches} with {IdCount} IDs",
                    batchIndex + 1, totalBatches, batchIds.Length);

                var fieldsRequest = new WorkItemBatchRequest
                {
                    Ids = batchIds,
                    Fields = RequiredWorkItemFields // Only fetch fields, no expand
                };

                var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");
                using var fieldsContent = new StringContent(
                    JsonSerializer.Serialize(fieldsRequest),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var fieldsResponse = await httpClient.PostAsync(batchUrl, fieldsContent, cancellationToken);
                await HandleHttpErrorsAsync(fieldsResponse, cancellationToken);

                using var fieldsStream = await fieldsResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var fieldsDoc = await JsonDocument.ParseAsync(fieldsStream, cancellationToken: cancellationToken);

                // Process work items and merge with relations data
                foreach (var item in fieldsDoc.RootElement.GetProperty("value").EnumerateArray())
                {
                    var id = item.GetProperty("id").GetInt32();
                    var fields = item.GetProperty("fields");
                    var type = fields.TryGetProperty("System.WorkItemType", out var t) ? t.GetString() ?? "" : "";
                    var title = fields.TryGetProperty("System.Title", out var ti) ? ti.GetString() ?? "" : "";
                    var state = fields.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "";
                    var area = fields.TryGetProperty("System.AreaPath", out var a) ? a.GetString() ?? "" : "";
                    var iteration = fields.TryGetProperty("System.IterationPath", out var ip) ? ip.GetString() ?? "" : "";
                    var description = fields.TryGetProperty("System.Description", out var d) ? d.GetString() : null;

                    // Get parent ID from relations map (populated in Phase 1)
                    var parentId = relationsMap.TryGetValue(id, out var pid) ? pid : null;

                    // Extract effort field with robust parsing (requirement #5)
                    // Handle int, double, and string values safely
                    int? effort = ParseEffortField(fields);

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
                        Effort: effort,
                        Description: description
                    ));
                }

                var batchElapsed = DateTimeOffset.UtcNow - batchStartTime;
                _logger.LogInformation(
                    "Phase 2 batch {BatchIndex}/{TotalBatches} completed: {IdCount} IDs fetched, HTTP {StatusCode}, {ElapsedMs}ms",
                    batchIndex + 1, totalBatches, batchIds.Length, (int)fieldsResponse.StatusCode, batchElapsed.TotalMilliseconds);
            }

            // Performance metrics and summary
            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogInformation(
                "Retrieved {Count} work items for areaPath={AreaPath}, since={Since} in {ElapsedMs}ms. " +
                "Hierarchy stats: Relations={RelationsCount}, ReverseLinks={ReverseLinksCount}, ParentIDs={ParentIdsCount}",
                results.Count, areaPath, since, elapsed.TotalMilliseconds,
                relationsPresent, reverseLinksPresent, parentIdsExtracted);

            return results;
        }, cancellationToken);
    }

    /// <summary>
    /// Retrieves work items starting from specified root work item IDs and traverses the hierarchy
    /// to collect ALL DESCENDANTS (children, grandchildren, etc.) AND their ANCESTORS (parents).
    /// 
    /// Traversal Strategy:
    /// - Phase 1: Starts with root work item IDs as the initial frontier
    /// - Uses breadth-first search (BFS) to explore descendants
    /// - For each work item in frontier, queries for its children using Hierarchy-Forward links
    /// - Children become the next frontier, process repeats until no more children found
    /// - Phase 1.5: Completes ancestors by walking UP to fetch missing parents
    /// 
    /// Link Direction:
    /// - Uses System.LinkTypes.Hierarchy-Forward which represents parent → child
    /// - Source = Parent, Target = Child
    /// - Query finds links where Source (parent) is in our frontier
    /// - Extracts Target IDs which are the children
    /// 
    /// For incremental sync:
    /// - Applies date filter only to field refresh, NEVER to graph discovery
    /// - Ensures complete hierarchy context is maintained
    /// 
    /// Expected Result:
    /// - Root work items + ALL their descendants (full subtree)
    /// - ALL ancestors (parents) needed to build a connected hierarchy
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsAsync(
        int[] rootWorkItemIds,
        DateTimeOffset? since = null,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (rootWorkItemIds == null || rootWorkItemIds.Length == 0)
        {
            _logger.LogWarning("GetWorkItemsByRootIdsAsync called with no root work item IDs");
            return Enumerable.Empty<WorkItemDto>();
        }

        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();
        var startTime = DateTimeOffset.UtcNow;
        var lastActivity = DateTimeOffset.UtcNow;
        const int inactivityTimeoutSeconds = 300; // 5 minutes of no progress = timeout

        _logger.LogInformation("Starting hierarchy sync for {Count} root work items: [{Ids}], incremental={Incremental}", 
            rootWorkItemIds.Length, string.Join(", ", rootWorkItemIds), since.HasValue);

        // Helper to update heartbeat and check for inactivity timeout
        void UpdateHeartbeat()
        {
            lastActivity = DateTimeOffset.UtcNow;
        }

        void CheckInactivity()
        {
            var elapsed = DateTimeOffset.UtcNow - lastActivity;
            if (elapsed.TotalSeconds > inactivityTimeoutSeconds)
            {
                _logger.LogError("Sync stalled: No progress for {Seconds} seconds", elapsed.TotalSeconds);
                throw new TimeoutException($"Sync cancelled due to inactivity (no progress for {elapsed.TotalSeconds:F0} seconds)");
            }
        }

        // Step 1: Traverse DOWN the hierarchy to collect ALL DESCENDANTS (children → grandchildren → ...)
        // Uses System.LinkTypes.Hierarchy-Forward to follow parent→child links
        // Does NOT traverse UP (no ancestor/parent expansion from roots)
        // 
        // CRITICAL: Discovery phase NEVER filters by date/since parameter.
        // Incremental sync (if since != null) will affect refresh logic AFTER discovery is complete.
        // This ensures the complete graph structure is always discovered.
        progressCallback?.Invoke(1, 3, "Querying work item hierarchy (descendants)...");
        UpdateHeartbeat();

        var allWorkItemIds = new HashSet<int>(rootWorkItemIds);
        var idsToProcess = new Queue<int>(rootWorkItemIds);
        var processedIds = new HashSet<int>();

        // BFS traversal to find all descendant work items
        // NO date filters - discovery must always find the complete hierarchy
        while (idsToProcess.Count > 0)
        {
            CheckInactivity();
            
            var currentBatch = new List<int>();
            while (idsToProcess.Count > 0 && currentBatch.Count < WorkItemBatchSize)
            {
                var id = idsToProcess.Dequeue();
                if (!processedIds.Contains(id))
                {
                    currentBatch.Add(id);
                    processedIds.Add(id);
                }
            }

            if (currentBatch.Count == 0) break;

            // Build WIQL query to find children of current batch
            // Note: idList is safe from injection as currentBatch contains only validated integers
            var idList = string.Join(",", currentBatch);
            
            // Query for DESCENDANTS (children) using WorkItemLinks with Hierarchy-Forward
            // 
            // Azure DevOps Link Semantics:
            // - Hierarchy-Forward: Represents parent → child direction
            // - Source: Parent work item
            // - Target: Child work item
            //
            // This query finds all links WHERE:
            // - Source (parent) is in our current batch
            // - Link type is Hierarchy-Forward (parent→child)
            //
            // Expected result: Target IDs = children of items in our batch
            //
            // CRITICAL: NO date filters, NO MODE(Recursive)
            // - Date filters would break graph discovery during incremental sync
            // - MODE(Recursive) conflicts with our explicit BFS traversal loop
            // - Discovery must always return the complete hierarchy
            var wiql = new
            {
                query = $"SELECT [System.Id] FROM WorkItemLinks WHERE " +
                        $"([Source].[System.Id] IN ({idList})) AND " +
                        $"([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward')"
            };

            _logger.LogDebug(
                "Querying descendants: Batch={BatchIds}, LinkType=Hierarchy-Forward (parent→child)",
                idList);

            try
            {
                var wiqlUrl = ProjectUrl(config, "_apis/wit/wiql");
                using var content = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");

                var wiqlResponse = await httpClient.PostAsync(wiqlUrl, content, cancellationToken);
                UpdateHeartbeat();
                
                if (wiqlResponse.IsSuccessStatusCode)
                {
                    using var stream = await wiqlResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    var childrenFound = 0;
               if (doc.RootElement.TryGetProperty("workItemRelations", out var relations))
               {
                  foreach (var relation in relations.EnumerateArray())
                  {
                     // Extract TARGET id which should be the child
                     if (!relation.TryGetProperty("rel", out var relProp) ||
                        relProp.ValueKind != JsonValueKind.String ||
                        !string.Equals(relProp.GetString(),
                            "System.LinkTypes.Hierarchy-Forward",
                            StringComparison.OrdinalIgnoreCase))
                     {
                        continue;
                     }

                     if (!relation.TryGetProperty("source", out var sourceProp) ||
                         sourceProp.ValueKind != JsonValueKind.Object ||
                         !sourceProp.TryGetProperty("id", out var sourceIdProp))
                        continue;

                     if (!relation.TryGetProperty("target", out var targetProp) ||
                         targetProp.ValueKind != JsonValueKind.Object ||
                         !targetProp.TryGetProperty("id", out var targetIdProp))
                        continue;

                     var parentId = sourceIdProp.GetInt32();
                     var childId = targetIdProp.GetInt32();

                     if (parentId == childId) continue;

                     if (allWorkItemIds.Add(childId) && !processedIds.Contains(childId))
                        idsToProcess.Enqueue(childId);
                  }
               }
                    
                    _logger.LogDebug(
                        "Found {ChildCount} new children for batch {BatchIds}. Total accumulated: {TotalCount}",
                        childrenFound, idList, allWorkItemIds.Count);
                }
                else
                {
                    _logger.LogWarning("WIQL tree query failed, falling back to simple query. Status: {Status}", 
                        wiqlResponse.StatusCode);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error querying work item hierarchy, continuing with direct query");
            }
        }

        _logger.LogInformation("Found {Count} work items in hierarchy (roots={RootCount}, descendants={DescCount})", 
            allWorkItemIds.Count, rootWorkItemIds.Length, allWorkItemIds.Count - rootWorkItemIds.Length);

        if (allWorkItemIds.Count == 0)
        {
            return Enumerable.Empty<WorkItemDto>();
        }

        // Step 2: Fetch all work items in the hierarchy using batch API
        progressCallback?.Invoke(2, 3, $"Fetching {allWorkItemIds.Count} work items...");
        UpdateHeartbeat();

        var ids = allWorkItemIds.ToArray();
        var totalBatches = (int)Math.Ceiling((double)ids.Length / WorkItemBatchSize);
        var results = new List<WorkItemDto>();

        // Phase 1: Fetch relations
        var relationsMap = new Dictionary<int, int?>();
        var relationsPresent = 0;
        var reverseLinksPresent = 0;
        var parentIdsExtracted = 0;

        _logger.LogInformation("Phase 1: Fetching relations for {Count} work items in {BatchCount} batches", 
            ids.Length, totalBatches);

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            CheckInactivity();
            var batchStartTime = DateTimeOffset.UtcNow;
            var batchIds = ids.Skip(batchIndex * WorkItemBatchSize).Take(WorkItemBatchSize).ToArray();

            _logger.LogDebug("Phase 1 (relations): Processing batch {BatchIndex}/{TotalBatches} with {IdCount} IDs",
                batchIndex + 1, totalBatches, batchIds.Length);

            progressCallback?.Invoke(2, 3, $"Phase 1: Relations batch {batchIndex + 1}/{totalBatches}...");

            var relationsRequest = new WorkItemBatchRequest
            {
                Ids = batchIds,
                Expand = "relations"
            };

            var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");
            using var relationsContent = new StringContent(
                JsonSerializer.Serialize(relationsRequest),
                System.Text.Encoding.UTF8,
                "application/json");

         var relationsResponse = await httpClient.PostAsync(batchUrl, relationsContent, cancellationToken);
            UpdateHeartbeat();
            await HandleHttpErrorsAsync(relationsResponse, cancellationToken);

            using var relationsStream = await relationsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var relationsDoc = await JsonDocument.ParseAsync(relationsStream, cancellationToken: cancellationToken);

         var relationsMissing = 0;
            var firstMissingId = (int?)null;
            var firstMissingKeys = (string?)null;

            foreach (var item in relationsDoc.RootElement.GetProperty("value").EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                
                // Check if relations are present
                if (item.TryGetProperty("relations", out var relations) && relations.ValueKind == JsonValueKind.Array)
                {
                    relationsPresent++;
                    
                    // Count reverse hierarchy links
                    foreach (var rel in relations.EnumerateArray())
                    {
                        if (rel.TryGetProperty("rel", out var relType))
                        {
                            var relTypeStr = relType.GetString();
                            if (string.Equals(relTypeStr, "System.LinkTypes.Hierarchy-Reverse", StringComparison.OrdinalIgnoreCase))
                            {
                                reverseLinksPresent++;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // Relations missing - capture diagnostic info for first occurrence
                    relationsMissing++;
                    if (!firstMissingId.HasValue)
                    {
                        firstMissingId = id;
                        try
                        {
                            // Safely capture the property names present in the item
                            var propNames = new List<string>();
                            foreach (var prop in item.EnumerateObject())
                            {
                                propNames.Add(prop.Name);
                            }
                            firstMissingKeys = string.Join(", ", propNames);
                        }
                        catch
                        {
                            firstMissingKeys = "(failed to enumerate properties)";
                        }
                    }
                }
                
                var parentId = ExtractParentIdFromRelations(item);
                relationsMap[id] = parentId;
                
                if (parentId.HasValue)
                {
                    parentIdsExtracted++;
                }
            }

            // Log diagnostics for missing relations if any
            if (relationsMissing > 0 && firstMissingId.HasValue)
            {
                _logger.LogDebug(
                    "Phase 1 batch {BatchIndex}/{TotalBatches}: {MissingCount} items missing 'relations' property. " +
                    "Sample item ID={SampleId}, properties present: [{Properties}]",
                    batchIndex + 1, totalBatches, relationsMissing, firstMissingId.Value, firstMissingKeys ?? "(none)");
            }

            var batchElapsed = DateTimeOffset.UtcNow - batchStartTime;
            _logger.LogDebug("Phase 1 batch {BatchIndex}/{TotalBatches} completed: {IdCount} IDs, HTTP {StatusCode}, {ElapsedMs}ms",
                batchIndex + 1, totalBatches, batchIds.Length, (int)relationsResponse.StatusCode, batchElapsed.TotalMilliseconds);
        }

        _logger.LogInformation(
            "Phase 1 complete: Relations present: {RelationsCount}, Reverse links: {ReverseLinksCount}, Parent IDs extracted: {ParentIdsCount}",
            relationsPresent, reverseLinksPresent, parentIdsExtracted);

        // Phase 1.5: Complete ancestors (fetch missing parents)
        // After descendants discovery and relations fetching, we may have parent IDs that aren't in our set
        // This phase walks UP the hierarchy to fetch all ancestors needed to build a connected tree
        var ancestorsAdded = await CompleteAncestorsAsync(
            config,
            httpClient,
            allWorkItemIds,
            relationsMap,
            progressCallback,
            UpdateHeartbeat,
            CheckInactivity,
            cancellationToken);

        _logger.LogInformation("Phase 1.5 complete: Added {AncestorCount} ancestors to complete hierarchy", ancestorsAdded);

        // Recalculate batch count and IDs array after adding ancestors
        ids = allWorkItemIds.ToArray();
        totalBatches = (int)Math.Ceiling((double)ids.Length / WorkItemBatchSize);

        // Phase 2: Fetch fields
        _logger.LogInformation("Phase 2: Fetching fields for {Count} work items in {BatchCount} batches", 
            ids.Length, totalBatches);

        progressCallback?.Invoke(3, 3, "Phase 2: Fetching work item fields...");
        UpdateHeartbeat();

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            CheckInactivity();
            var batchStartTime = DateTimeOffset.UtcNow;
            var batchIds = ids.Skip(batchIndex * WorkItemBatchSize).Take(WorkItemBatchSize).ToArray();

            _logger.LogDebug("Phase 2 (fields): Processing batch {BatchIndex}/{TotalBatches} with {IdCount} IDs",
                batchIndex + 1, totalBatches, batchIds.Length);

            progressCallback?.Invoke(3, 3, $"Phase 2: Fields batch {batchIndex + 1}/{totalBatches}...");

            var fieldsRequest = new WorkItemBatchRequest
            {
                Ids = batchIds,
                Fields = RequiredWorkItemFields
            };

            var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");
            using var fieldsContent = new StringContent(
                JsonSerializer.Serialize(fieldsRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            var fieldsResponse = await httpClient.PostAsync(batchUrl, fieldsContent, cancellationToken);
            UpdateHeartbeat();
            await HandleHttpErrorsAsync(fieldsResponse, cancellationToken);

            using var fieldsStream = await fieldsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var fieldsDoc = await JsonDocument.ParseAsync(fieldsStream, cancellationToken: cancellationToken);

         foreach (var item in fieldsDoc.RootElement.GetProperty("value").EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var fields = item.GetProperty("fields");
                var type = fields.TryGetProperty("System.WorkItemType", out var t) ? t.GetString() ?? "" : "";
                var title = fields.TryGetProperty("System.Title", out var ti) ? ti.GetString() ?? "" : "";
                var state = fields.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "";
                var area = fields.TryGetProperty("System.AreaPath", out var a) ? a.GetString() ?? "" : "";
                var iteration = fields.TryGetProperty("System.IterationPath", out var ip) ? ip.GetString() ?? "" : "";
                var description = fields.TryGetProperty("System.Description", out var d) ? d.GetString() : null;

                var parentId = relationsMap.TryGetValue(id, out var pid) ? pid : null;
                int? effort = ParseEffortField(fields);

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
                    Effort: effort,
                    Description: description
                ));
            }

            var batchElapsed = DateTimeOffset.UtcNow - batchStartTime;
            _logger.LogInformation(
                "Phase 2 batch {BatchIndex}/{TotalBatches} completed: {IdCount} IDs fetched, HTTP {StatusCode}, {ElapsedMs}ms",
                batchIndex + 1, totalBatches, batchIds.Length, (int)fieldsResponse.StatusCode, batchElapsed.TotalMilliseconds);
        }

        var elapsed = DateTimeOffset.UtcNow - startTime;
        _logger.LogInformation(
            "Retrieved {Count} work items for root IDs [{RootIds}] in {ElapsedMs}ms. " +
            "Hierarchy stats: Relations={RelationsCount}, ReverseLinks={ReverseLinksCount}, ParentIDs={ParentIdsCount}",
            results.Count, string.Join(", ", rootWorkItemIds), elapsed.TotalMilliseconds,
            relationsPresent, reverseLinksPresent, parentIdsExtracted);

        return results;
    }

    /// <summary>
    /// Retrieves work items starting from specified root work item IDs with detailed structured progress reporting.
    /// This is a wrapper around GetWorkItemsByRootIdsAsync that provides enhanced progress callbacks.
    /// </summary>
    public async Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsWithDetailedProgressAsync(
        int[] rootWorkItemIds,
        DateTimeOffset? since = null,
        Action<SyncProgressDto>? detailedProgressCallback = null,
        CancellationToken cancellationToken = default)
    {
        // For now, wrap the existing method with basic progress translation
        // In the future, this can be enhanced to emit detailed batch-level progress
        return await GetWorkItemsByRootIdsAsync(
            rootWorkItemIds,
            since,
            (step, total, label) =>
            {
                detailedProgressCallback?.Invoke(new SyncProgressDto
                {
                    Status = "InProgress",
                    Message = label,
                    MajorStep = step,
                    MajorStepTotal = total,
                    MajorStepLabel = label
                });
            },
            cancellationToken);
    }

    /// <summary>
    /// Extracts the parent work item ID from the relations array.
    /// Parent relationship is stored with rel == "System.LinkTypes.Hierarchy-Reverse".
    /// </summary>
    private static int? ExtractParentIdFromRelations(JsonElement item)
    {
        if (!item.TryGetProperty("relations", out var relations) || relations.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var relation in relations.EnumerateArray())
        {
            if (!relation.TryGetProperty("rel", out var rel))
                continue;

            var relType = rel.GetString();
            // Case-insensitive match for relation type
            if (!string.Equals(relType, "System.LinkTypes.Hierarchy-Reverse", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!relation.TryGetProperty("url", out var urlProp))
                continue;

            var url = urlProp.GetString();
            if (string.IsNullOrEmpty(url))
                continue;

            // Robust URL parsing: strip querystring and trim trailing slash
            // Example URL: https://dev.azure.com/org/project/_apis/wit/workItems/123?api-version=7.0
            var queryIndex = url.IndexOf('?');
            var urlWithoutQuery = queryIndex >= 0 ? url.Substring(0, queryIndex).TrimEnd('/') : url.TrimEnd('/');
            var segments = urlWithoutQuery.Split('/');
            if (segments.Length > 0 && int.TryParse(segments[^1], out var parsedId))
            {
                return parsedId;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses effort from work item fields with robust type handling.
    /// Handles int, double, and string values safely (requirement #5).
    /// </summary>
    private static int? ParseEffortField(JsonElement fields)
    {
        // Try Microsoft.VSTS.Scheduling.Effort first
        if (fields.TryGetProperty(TfsFieldEffort, out var effortField))
        {
            var parsed = ParseNumericValue(effortField);
            if (parsed.HasValue)
                return parsed;
        }

        // Fall back to Microsoft.VSTS.Scheduling.StoryPoints
        if (fields.TryGetProperty(TfsFieldStoryPoints, out var storyPoints))
        {
            return ParseNumericValue(storyPoints);
        }

        return null;
    }

    /// <summary>
    /// Parses a JSON element as a numeric value, handling int, double, and string types.
    /// </summary>
    private static int? ParseNumericValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                // TryGetInt32 handles integer values
                if (element.TryGetInt32(out var intValue))
                    return intValue;
                // Fall back to double for decimal values
                if (element.TryGetDouble(out var doubleValue))
                    return (int)Math.Round(doubleValue);
                break;

            case JsonValueKind.String:
                var strValue = element.GetString();
                if (!string.IsNullOrEmpty(strValue))
                {
                    // Try parsing as int first
                    if (int.TryParse(strValue, out var parsedInt))
                        return parsedInt;
                    // Try parsing as double and round
                    if (double.TryParse(strValue, out var parsedDouble))
                        return (int)Math.Round(parsedDouble);
                }
                break;
        }

        return null;
    }

    // Pull Request methods - Phase 2 implementation with Phase 4 enhancements
    public async Task<IEnumerable<PullRequestDto>> GetPullRequestsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient();

        var startTime = DateTimeOffset.UtcNow; // Phase 4: Performance metrics

        return await ExecuteWithRetryAsync(async () =>
        {
            // Get all repositories or specific one
            var repositories = await GetRepositoriesInternalAsync(config, httpClient, repositoryName, cancellationToken);
            var allPRs = new List<PullRequestDto>();

            _logger.LogDebug("Querying {RepoCount} repositories for pull requests", repositories.Count);

            foreach (var repo in repositories)
            {
                // Build query parameters
                var queryParams = new List<string>
                {
                    "searchCriteria.status=all" // Get all PRs (active, completed, abandoned)
                };

                if (fromDate.HasValue)
                {
                    // Azure DevOps API uses minTime for filtering
                    queryParams.Add($"searchCriteria.minTime={fromDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }

                if (toDate.HasValue)
                {
                    queryParams.Add($"searchCriteria.maxTime={toDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                }

                // Git PRs are project-scoped (requirement #1)
                var encodedRepoName = Uri.EscapeDataString(repo.Name);
                var url = ProjectUrl(config, $"_apis/git/repositories/{encodedRepoName}/pullrequests?{string.Join("&", queryParams)}");

                _logger.LogDebug("Fetching PRs from repository {Repository}", repo.Name);

                var response = await httpClient.GetAsync(url, cancellationToken);
                await HandleHttpErrorsAsync(response, cancellationToken);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                    continue;

                foreach (var pr in valueArray.EnumerateArray())
                {
                    var prId = pr.GetProperty("pullRequestId").GetInt32();
                    var title = pr.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var status = pr.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                    var sourceBranch = pr.TryGetProperty("sourceRefName", out var src) ? src.GetString() ?? "" : "";
                    var targetBranch = pr.TryGetProperty("targetRefName", out var tgt) ? tgt.GetString() ?? "" : "";

                    var createdBy = "";
                    if (pr.TryGetProperty("createdBy", out var creator))
                    {
                        createdBy = creator.TryGetProperty("displayName", out var name) ? name.GetString() ?? "" : "";
                    }

                    var createdDate = pr.TryGetProperty("creationDate", out var cd) ? cd.GetDateTimeOffset() : DateTimeOffset.UtcNow;
                    var completedDate = pr.TryGetProperty("closedDate", out var cld) && cld.ValueKind != JsonValueKind.Null
                        ? (DateTimeOffset?)cld.GetDateTimeOffset()
                        : null;

                    // Determine iteration path from work items or use default
                    var iterationPath = config.Project; // Default to project name

                    allPRs.Add(new PullRequestDto(
                        Id: prId,
                        RepositoryName: repo.Name,
                        Title: title,
                        CreatedBy: createdBy,
                        CreatedDate: createdDate,
                        CompletedDate: completedDate,
                        Status: status,
                        IterationPath: iterationPath,
                        SourceBranch: sourceBranch,
                        TargetBranch: targetBranch,
                        RetrievedAt: DateTimeOffset.UtcNow
                    ));
                }
            }

            // Phase 4: Performance metrics
            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogInformation("Retrieved {Count} pull requests across {RepoCount} repositories in {ElapsedMs}ms",
                allPRs.Count, repositories.Count, elapsed.TotalMilliseconds);

            return allPRs;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestIterationDto>> GetPullRequestIterationsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!; // Non-null after validation

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // Git PRs are project-scoped (requirement #1)
            var encodedRepoName = Uri.EscapeDataString(repositoryName);
            var url = ProjectUrl(config, $"_apis/git/repositories/{encodedRepoName}/pullrequests/{pullRequestId}/iterations");

            var response = await httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var iterations = new List<PullRequestIterationDto>();

            if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                return iterations;

            foreach (var iteration in valueArray.EnumerateArray())
            {
                var iterationId = iteration.GetProperty("id").GetInt32();
                var createdDate = iteration.TryGetProperty("createdDate", out var cd) ? cd.GetDateTimeOffset() : DateTimeOffset.UtcNow;
                var updatedDate = iteration.TryGetProperty("updatedDate", out var ud) ? ud.GetDateTimeOffset() : createdDate;

                // Count commits and changes
                var commitCount = 0;
                if (iteration.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Array)
                {
                    commitCount = commits.GetArrayLength();
                }

                var changeCount = 0;
                if (iteration.TryGetProperty("changeList", out var changes) && changes.ValueKind == JsonValueKind.Array)
                {
                    changeCount = changes.GetArrayLength();
                }

                iterations.Add(new PullRequestIterationDto(
                    PullRequestId: pullRequestId,
                    IterationNumber: iterationId,
                    CreatedDate: createdDate,
                    UpdatedDate: updatedDate,
                    CommitCount: commitCount,
                    ChangeCount: changeCount
                ));
            }

            _logger.LogInformation("Retrieved {Count} iterations for PR {PullRequestId}", iterations.Count, pullRequestId);
            return iterations;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestCommentDto>> GetPullRequestCommentsAsync(
        int pullRequestId,
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!; // Non-null after validation

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // Git PRs are project-scoped (requirement #1)
            var encodedRepoName = Uri.EscapeDataString(repositoryName);
            var url = ProjectUrl(config, $"_apis/git/repositories/{encodedRepoName}/pullrequests/{pullRequestId}/threads");

            var response = await httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var comments = new List<PullRequestCommentDto>();

            if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                return comments;

            foreach (var thread in valueArray.EnumerateArray())
            {
                var threadId = thread.GetProperty("id").GetInt32();
                var threadStatus = thread.TryGetProperty("status", out var ts) ? ts.GetString() ?? "" : "";
                var isResolved = threadStatus.Equals("fixed", StringComparison.OrdinalIgnoreCase) ||
                                threadStatus.Equals("closed", StringComparison.OrdinalIgnoreCase);

                if (!thread.TryGetProperty("comments", out var threadComments) || threadComments.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var comment in threadComments.EnumerateArray())
                {
                    var commentId = comment.GetProperty("id").GetInt32();
                    var content = comment.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                    var createdDate = comment.TryGetProperty("publishedDate", out var cd) ? cd.GetDateTimeOffset() : DateTimeOffset.UtcNow;
                    var updatedDate = comment.TryGetProperty("lastUpdatedDate", out var ud) && ud.ValueKind != JsonValueKind.Null
                        ? (DateTimeOffset?)ud.GetDateTimeOffset()
                        : null;

                    var author = "";
                    if (comment.TryGetProperty("author", out var auth))
                    {
                        author = auth.TryGetProperty("displayName", out var name) ? name.GetString() ?? "" : "";
                    }

                    // Check if this comment resolved the thread
                    string? resolvedBy = null;
                    DateTimeOffset? resolvedDate = null;
                    if (isResolved && thread.TryGetProperty("lastUpdatedDate", out var threadUpdated))
                    {
                        resolvedDate = threadUpdated.GetDateTimeOffset();
                        // The author of the last comment in a resolved thread is typically the resolver
                        resolvedBy = author;
                    }

                    comments.Add(new PullRequestCommentDto(
                        Id: commentId,
                        PullRequestId: pullRequestId,
                        ThreadId: threadId,
                        Author: author,
                        Content: content,
                        CreatedDate: createdDate,
                        UpdatedDate: updatedDate,
                        IsResolved: isResolved,
                        ResolvedDate: resolvedDate,
                        ResolvedBy: resolvedBy
                    ));
                }
            }

            _logger.LogInformation("Retrieved {Count} comments for PR {PullRequestId}", comments.Count, pullRequestId);
            return comments;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PullRequestFileChangeDto>> GetPullRequestFileChangesAsync(
        int pullRequestId,
        string repositoryName,
        int iterationId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!; // Non-null after validation

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // Git PRs are project-scoped (requirement #1)
            var encodedRepoName = Uri.EscapeDataString(repositoryName);
            var url = ProjectUrl(config, $"_apis/git/repositories/{encodedRepoName}/pullrequests/{pullRequestId}/iterations/{iterationId}/changes");

            var response = await httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var fileChanges = new List<PullRequestFileChangeDto>();

            if (!doc.RootElement.TryGetProperty("changeEntries", out var changeEntries))
                return fileChanges;

            foreach (var change in changeEntries.EnumerateArray())
            {
                var filePath = "";
                if (change.TryGetProperty("item", out var item))
                {
                    filePath = item.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                }

                var changeType = change.TryGetProperty("changeType", out var ct) ? ct.GetString() ?? "" : "";

                // Note: Line-level statistics require additional API call to get diff
                // For now, we'll set counts to 0 and can enhance later with diff API
                var linesAdded = 0;
                var linesDeleted = 0;
                var linesModified = 0;

                fileChanges.Add(new PullRequestFileChangeDto(
                    PullRequestId: pullRequestId,
                    IterationId: iterationId,
                    FilePath: filePath,
                    ChangeType: changeType,
                    LinesAdded: linesAdded,
                    LinesDeleted: linesDeleted,
                    LinesModified: linesModified
                ));
            }

            _logger.LogInformation("Retrieved {Count} file changes for PR {PullRequestId} iteration {IterationId}",
                fileChanges.Count, pullRequestId, iterationId);
            return fileChanges;
        }, cancellationToken);
    }

    public async Task<IEnumerable<WorkItemRevisionDto>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!; // Non-null after validation

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // Work item revisions are collection-scoped (work item IDs are unique across collection)
            var url = CollectionUrl(config, $"_apis/wit/workitems/{workItemId}/revisions");

            var response = await httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var revisions = new List<WorkItemRevisionDto>();

            if (!doc.RootElement.TryGetProperty("value", out var revisionsArray))
                return revisions;

            // Store previous revision fields for comparison
            Dictionary<string, JsonElement>? previousRevisionFields = null;

            foreach (var revision in revisionsArray.EnumerateArray())
            {
                var revNumber = revision.TryGetProperty("rev", out var rev) ? rev.GetInt32() : 0;

                var changedBy = "";
                if (revision.TryGetProperty("fields", out var fields))
                {
                    if (fields.TryGetProperty("System.ChangedBy", out var cb))
                    {
                        if (cb.ValueKind == JsonValueKind.Object && cb.TryGetProperty("displayName", out var displayName))
                        {
                            changedBy = displayName.GetString() ?? "";
                        }
                        else if (cb.ValueKind == JsonValueKind.String)
                        {
                            changedBy = cb.GetString() ?? "";
                        }
                    }
                }

                var changedDate = DateTimeOffset.UtcNow;
                if (revision.TryGetProperty("fields", out var fieldsForDate))
                {
                    if (fieldsForDate.TryGetProperty("System.ChangedDate", out var cd) && cd.ValueKind != JsonValueKind.Null)
                    {
                        changedDate = cd.GetDateTimeOffset();
                    }
                }

                var comment = "";
                if (revision.TryGetProperty("fields", out var fieldsForComment))
                {
                    if (fieldsForComment.TryGetProperty("System.History", out var hist) && hist.ValueKind == JsonValueKind.String)
                    {
                        comment = hist.GetString();
                    }
                }

                // Calculate field changes by comparing with previous revision
                var fieldChanges = new Dictionary<string, WorkItemFieldChange>();

                if (previousRevisionFields != null && revision.TryGetProperty("fields", out var currentFields))
                {
                    // Get all fields from current revision
                    foreach (var field in currentFields.EnumerateObject())
                    {
                        var fieldName = field.Name;
                        var newValue = GetFieldValueAsString(field.Value);

                        // Skip system fields that are noise in history
                        if (fieldName.StartsWith("System.Watermark") ||
                            fieldName.StartsWith("System.Rev") ||
                            fieldName == "System.ChangedDate" ||
                            fieldName == "System.ChangedBy" ||
                            fieldName == "System.RevisedDate")
                        {
                            continue;
                        }

                        // Find the old value from previous revision
                        string? oldValue = null;
                        if (previousRevisionFields.TryGetValue(fieldName, out var previousFieldElement))
                        {
                            oldValue = GetFieldValueAsString(previousFieldElement);
                        }

                        // Only add if value actually changed
                        if (oldValue != newValue)
                        {
                            fieldChanges[fieldName] = new WorkItemFieldChange(
                                FieldName: fieldName,
                                OldValue: oldValue,
                                NewValue: newValue
                            );
                        }
                    }

                    // Also check for removed fields (present in previous but not in current)
                    foreach (var previousField in previousRevisionFields)
                    {
                        var fieldName = previousField.Key;

                        // Skip noise fields
                        if (fieldName.StartsWith("System.Watermark") ||
                            fieldName.StartsWith("System.Rev") ||
                            fieldName == "System.ChangedDate" ||
                            fieldName == "System.ChangedBy" ||
                            fieldName == "System.RevisedDate")
                        {
                            continue;
                        }

                        // If field not in current revision, it was removed
                        if (currentFields.TryGetProperty(fieldName, out _) == false)
                        {
                            var oldValue = GetFieldValueAsString(previousField.Value);
                            fieldChanges[fieldName] = new WorkItemFieldChange(
                                FieldName: fieldName,
                                OldValue: oldValue,
                                NewValue: null
                            );
                        }
                    }
                }

                var revisionDto = new WorkItemRevisionDto(
                    RevisionNumber: revNumber,
                    WorkItemId: workItemId,
                    ChangedBy: changedBy,
                    ChangedDate: changedDate,
                    FieldChanges: fieldChanges,
                    Comment: comment
                );

                revisions.Add(revisionDto);

                // Store current revision fields for next iteration comparison
                if (revision.TryGetProperty("fields", out var fieldsToStore))
                {
                    previousRevisionFields = new Dictionary<string, JsonElement>();
                    foreach (var field in fieldsToStore.EnumerateObject())
                    {
                        previousRevisionFields[field.Name] = field.Value.Clone();
                    }
                }
            }

            _logger.LogInformation("Retrieved {Count} revisions for work item {WorkItemId}",
                revisions.Count, workItemId);
            return revisions;
        }, cancellationToken);
    }

    // Private helper methods

    private static string? GetFieldValueAsString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "True",
            JsonValueKind.False => "False",
            JsonValueKind.Null => null,
            JsonValueKind.Object => element.GetRawText(), // For complex objects, return JSON
            JsonValueKind.Array => element.GetRawText(),
            _ => null
        };
    }

    /// <summary>
    /// DEPRECATED: This method is kept for backward compatibility but is no longer used.
    /// All TFS calls now use GetAuthenticatedHttpClient() which creates properly configured
    /// HttpClient instances from IHttpClientFactory.
    /// 
    /// The old approach of mutating _httpClient state was problematic because:
    /// - NTLM authentication must be handled in HttpClientHandler, not headers
    /// - Mixing PAT headers with NTLM handlers causes conflicts
    /// - The same HttpClient instance shouldn't be reused across auth modes
    /// </summary>
    [Obsolete("Use GetAuthenticatedHttpClient() instead. This method is kept only for backward compatibility.")]
    private Task ConfigureAuthenticationAsync(TfsConfigEntity entity, CancellationToken cancellationToken)
    {
        // This method is no longer called but kept for backward compatibility
        _logger.LogWarning("ConfigureAuthenticationAsync is deprecated. Use GetAuthenticatedHttpClient() instead.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets repositories in the project using the provided authenticated HttpClient.
    /// Git repositories are project-scoped.
    /// </summary>
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

    public async Task<bool> UpdateWorkItemStateAsync(int workItemId, string newState, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} to state '{NewState}'", workItemId, newState);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item");
                return false;
            }

            // Use auth-mode-specific HttpClient (requirement #2)
            var httpClient = GetAuthenticatedHttpClient();

            // Create per-request timeout token for write operation
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(entity.TimeoutSeconds));

            // Build JSON Patch document
            var patchDocument = new[]
            {
                new
                {
                    op = "add",
                    path = $"/fields/{TfsFieldState}",
                    value = newState
                }
            };

            // Work item PATCH is collection-scoped (work item IDs are unique across collection)
            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");
            using var content = new StringContent(
                JsonSerializer.Serialize(patchDocument),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogDebug("Sending PATCH request to update work item {WorkItemId}", workItemId);

            // PATCH operations are NOT retried - they are non-idempotent
            var response = await httpClient.PatchAsync(updateUrl, content, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} to state '{NewState}'", workItemId, newState);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId}. Status: {StatusCode}, Response: {Response}",
                    workItemId, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Update work item {WorkItemId} timed out", workItemId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} state to '{NewState}'", workItemId, newState);
            return false;
        }
    }

    public async Task<bool> UpdateWorkItemEffortAsync(int workItemId, int effort, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} effort to {Effort}", workItemId, effort);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item effort");
                return false;
            }

            // Use auth-mode-specific HttpClient (requirement #2)
            var httpClient = GetAuthenticatedHttpClient();

            // Create per-request timeout token for write operation
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(entity.TimeoutSeconds));

            // Build JSON Patch document for effort (Microsoft.VSTS.Scheduling.Effort)
            var patchDocument = new[]
            {
                new
                {
                    op = "add",
                    path = $"/fields/{TfsFieldEffort}",
                    value = effort
                }
            };

            // Work item PATCH is collection-scoped (work item IDs are unique across collection)
            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");
            using var content = new StringContent(
                JsonSerializer.Serialize(patchDocument),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogDebug("Sending PATCH request to update work item {WorkItemId} effort", workItemId);

            // PATCH operations are NOT retried - they are non-idempotent
            var response = await httpClient.PatchAsync(updateUrl, content, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} effort to {Effort}", workItemId, effort);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId} effort. Status: {StatusCode}, Response: {Response}",
                    workItemId, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Update work item {WorkItemId} effort timed out", workItemId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} effort to {Effort}", workItemId, effort);
            return false;
        }
    }

    public async Task<TfsVerificationReport> VerifyCapabilitiesAsync(
        bool includeWriteChecks = false,
        int? workItemIdForWriteCheck = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!; // Non-null after validation

        _logger.LogInformation("Starting TFS API verification. WriteChecks: {IncludeWriteChecks}, WorkItemId: {WorkItemId}",
            includeWriteChecks, workItemIdForWriteCheck);

        var checks = new List<TfsCapabilityCheckResult>();

        // Run read-only checks
        // Step 1: Server & authentication validation
        checks.Add(await VerifyServerReachabilityAsync(config, cancellationToken));
        // Step 2: Project validation
        checks.Add(await VerifyProjectAccessAsync(config, cancellationToken));
        // Step 3: Work item query (WIQL)
        checks.Add(await VerifyWorkItemQueryAsync(config, cancellationToken));
        // Step 4: Work item hierarchy chain retrieval
        checks.Add(await VerifyWorkItemHierarchyAsync(config, cancellationToken));
        // Step 5: Work item fields
        checks.Add(await VerifyWorkItemFieldsAsync(config, cancellationToken));
        // Step 6: Batch read
        checks.Add(await VerifyBatchReadAsync(config, cancellationToken));
        // Step 7: Work item revisions
        checks.Add(await VerifyWorkItemRevisionsAsync(config, cancellationToken));
        // Step 8: Pull requests
        checks.Add(await VerifyPullRequestsAsync(config, cancellationToken));
        // Step 9: Pipelines (build + release)
        checks.Add(await VerifyPipelinesAsync(config, cancellationToken));

        // Run write checks if requested
        if (includeWriteChecks)
        {
            if (workItemIdForWriteCheck.HasValue)
            {
                checks.Add(await VerifyWorkItemUpdateAsync(config, workItemIdForWriteCheck.Value, cancellationToken));
            }
            else
            {
                _logger.LogWarning("Write checks requested but no work item ID provided, skipping write verification");
            }
        }

        var report = new TfsVerificationReport
        {
            VerifiedAt = DateTimeOffset.UtcNow,
            ServerUrl = config.Url,
            ProjectName = config.Project,
            ApiVersion = config.ApiVersion,
            IncludedWriteChecks = includeWriteChecks,
            Success = checks.All(c => c.Success),
            Checks = checks
        };

        _logger.LogInformation("TFS verification completed. Success: {Success}, Passed: {Passed}/{Total}",
            report.Success, checks.Count(c => c.Success), checks.Count);

        return report;
    }

    private async Task<TfsCapabilityCheckResult> VerifyServerReachabilityAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient();

            // Collection-scoped endpoint for server reachability
            var url = CollectionUrl(config, "_apis/projects");
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "server-reachability",
                    Success = true,
                    ImpactedFunctionality = "All TFS integration features",
                    ExpectedBehavior = "Server responds to API requests with valid authentication",
                    ObservedBehavior = $"Server reachable, authentication successful (HTTP {(int)response.StatusCode})"
                };
            }

            return CreateFailureResult(
                "server-reachability",
                "All TFS integration features",
                "Server responds to API requests with valid authentication",
                $"Server returned HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "server-reachability",
                "All TFS integration features",
                "Server responds to API requests with valid authentication",
                $"Exception: {ex.GetType().Name}",
                ex is TfsAuthenticationException ? FailureCategory.Authentication : FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyProjectAccessAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient();

            // Project access check is collection-scoped with project name in path
            var encodedProject = Uri.EscapeDataString(config.Project);
            var url = CollectionUrl(config, $"_apis/projects/{encodedProject}");
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var projectName = doc.RootElement.GetProperty("name").GetString();

                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "project-access",
                    Success = true,
                    ImpactedFunctionality = "Work item retrieval, project-specific operations",
                    ExpectedBehavior = $"Project '{config.Project}' exists and is accessible",
                    ObservedBehavior = $"Project found: {projectName}"
                };
            }

            return CreateFailureResult(
                "project-access",
                "Work item retrieval, project-specific operations",
                $"Project '{config.Project}' exists and is accessible",
                $"HTTP {(int)response.StatusCode}",
                response.StatusCode == HttpStatusCode.NotFound ? FailureCategory.Authorization : CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "project-access",
                "Work item retrieval, project-specific operations",
                $"Project '{config.Project}' exists and is accessible",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyWorkItemQueryAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient();

            var wiql = new
            {
                query = "Select [System.Id] From WorkItems Where [System.WorkItemType] <> ''"
            };

            // WIQL is project-scoped (requirement #1)
            var url = ProjectUrl(config, "_apis/wit/wiql");
            using var content = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(url, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "work-item-query",
                    Success = true,
                    ImpactedFunctionality = "Work item search and filtering",
                    ExpectedBehavior = "WIQL queries execute successfully",
                    ObservedBehavior = "WIQL query executed successfully"
                };
            }

            return CreateFailureResult(
                "work-item-query",
                "Work item search and filtering",
                "WIQL queries execute successfully",
                $"HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "work-item-query",
                "Work item search and filtering",
                "WIQL queries execute successfully",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.QueryRestriction,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyWorkItemHierarchyAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient();

            // Step 1: Run WIQL query to get a few work items
            var wiql = new
            {
                query = $"Select [System.Id] From WorkItems Where [System.AreaPath] UNDER '{EscapeWiql(config.DefaultAreaPath)}' ORDER BY [System.Id] DESC"
            };

            var wiqlUrl = ProjectUrl(config, "_apis/wit/wiql");
            using var wiqlContent = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");

            var wiqlResponse = await httpClient.PostAsync(wiqlUrl, wiqlContent, cancellationToken);

            if (!wiqlResponse.IsSuccessStatusCode)
            {
                return CreateFailureResult(
                    "work-item-hierarchy",
                    "Work item hierarchy display (Goal → Task chain)",
                    "Work item relationships can be resolved",
                    $"WIQL query failed: HTTP {(int)wiqlResponse.StatusCode}",
                    CategorizeHttpError(wiqlResponse.StatusCode),
                    await wiqlResponse.Content.ReadAsStringAsync(cancellationToken));
            }

            using var wiqlStream = await wiqlResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var wiqlDoc = await JsonDocument.ParseAsync(wiqlStream, cancellationToken: cancellationToken);

            var workItemIds = wiqlDoc.RootElement.GetProperty("workItems").EnumerateArray()
                .Take(10) // Sample first 10 items
                .Select(e => e.GetProperty("id").GetInt32())
                .ToArray();

            if (workItemIds.Length == 0)
            {
                // No work items found - this is acceptable (not a failure)
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "work-item-hierarchy",
                    Success = true,
                    ImpactedFunctionality = "Work item hierarchy display (Goal → Task chain)",
                    ExpectedBehavior = "Work item relationships can be resolved",
                    ObservedBehavior = "No work items found in configured area path (hierarchy verification skipped)"
                };
            }

            // Step 2: Fetch work items with relations to verify hierarchy resolution
            // Use two-phase retrieval to avoid TFS Server 2022 limitation
            // Phase 1: Fetch relations only (no fields)
            var relationsRequest = new WorkItemBatchRequest
            {
                Ids = workItemIds,
                Expand = "relations"
            };

            var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");
            using var relationsContent = new StringContent(
                JsonSerializer.Serialize(relationsRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            var relationsResponse = await httpClient.PostAsync(batchUrl, relationsContent, cancellationToken);

            if (!relationsResponse.IsSuccessStatusCode)
            {
                return CreateFailureResult(
                    "work-item-hierarchy",
                    "Work item hierarchy display (Goal → Task chain)",
                    "Work item relationships can be resolved",
                    $"Batch work item fetch (relations) failed: HTTP {(int)relationsResponse.StatusCode}",
                    CategorizeHttpError(relationsResponse.StatusCode),
                    await relationsResponse.Content.ReadAsStringAsync(cancellationToken));
            }

            using var relationsStream = await relationsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var relationsDoc = await JsonDocument.ParseAsync(relationsStream, cancellationToken: cancellationToken);

            // Step 3: Verify hierarchy resolution - check for parent relationships
            var itemsWithParent = 0;
            var totalItems = 0;
            var maxDepthFound = 0;
            var workItemsWithRelations = new Dictionary<int, int?>(); // id -> parentId

            foreach (var item in relationsDoc.RootElement.GetProperty("value").EnumerateArray())
            {
                totalItems++;
                var itemId = item.GetProperty("id").GetInt32();
                var parentId = ExtractParentIdFromRelations(item);
                workItemsWithRelations[itemId] = parentId;

                if (parentId.HasValue)
                {
                    itemsWithParent++;
                }
            }

            // Try to follow parent chain to find max depth
            foreach (var (itemId, parentId) in workItemsWithRelations)
            {
                if (!parentId.HasValue) continue;

                var depth = 1;
                var currentParentId = parentId;
                var visitedIds = new HashSet<int> { itemId };

                while (currentParentId.HasValue && depth < 10 && !visitedIds.Contains(currentParentId.Value))
                {
                    visitedIds.Add(currentParentId.Value);
                    if (workItemsWithRelations.TryGetValue(currentParentId.Value, out var nextParent))
                    {
                        currentParentId = nextParent;
                        depth++;
                    }
                    else
                    {
                        break;
                    }
                }

                maxDepthFound = Math.Max(maxDepthFound, depth);
            }

            return new TfsCapabilityCheckResult
            {
                CapabilityId = "work-item-hierarchy",
                Success = true,
                ImpactedFunctionality = "Work item hierarchy display (Goal → Task chain)",
                ExpectedBehavior = "Work item relationships can be resolved",
                ObservedBehavior = $"Hierarchy resolution working. {itemsWithParent}/{totalItems} items have parent links. Max depth traced: {maxDepthFound}"
            };
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "work-item-hierarchy",
                "Work item hierarchy display (Goal → Task chain)",
                "Work item relationships can be resolved",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyWorkItemFieldsAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient();

            // Work item fields are collection-scoped (requirement #1)
            var url = CollectionUrl(config, "_apis/wit/fields");
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                var fields = doc.RootElement.GetProperty("value").EnumerateArray()
                    .Select(f => f.GetProperty("referenceName").GetString())
                    .ToList();

                // Check for required fields
                var requiredFields = new[] { "System.Id", "System.Title", "System.State", "System.WorkItemType" };
                var missingFields = requiredFields.Where(rf => !fields.Contains(rf)).ToList();

                if (missingFields.Any())
                {
                    return CreateFailureResult(
                        "work-item-fields",
                        "Work item display and processing",
                        "Required work item fields are accessible",
                        $"Missing fields: {string.Join(", ", missingFields)}",
                        FailureCategory.MissingField,
                        $"Found {fields.Count} fields but missing required fields");
                }

                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "work-item-fields",
                    Success = true,
                    ImpactedFunctionality = "Work item display and processing",
                    ExpectedBehavior = "Required work item fields are accessible",
                    ObservedBehavior = $"All required fields present ({fields.Count} total fields)"
                };
            }

            return CreateFailureResult(
                "work-item-fields",
                "Work item display and processing",
                "Required work item fields are accessible",
                $"HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "work-item-fields",
                "Work item display and processing",
                "Required work item fields are accessible",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyBatchReadAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient();

            // Test Work Items Batch API (POST) which is the recommended approach
            // This is collection-scoped (work item IDs are unique across collection)
            var batchRequest = new WorkItemBatchRequest
            {
                Ids = new[] { 1, 2, 3 }
            };

            var url = CollectionUrl(config, "_apis/wit/workitemsbatch");
            using var content = new StringContent(
                JsonSerializer.Serialize(batchRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(url, content, cancellationToken);

            // We expect 200 (with items) or 404 (no items found), both are acceptable
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "batch-read",
                    Success = true,
                    ImpactedFunctionality = "Efficient work item synchronization",
                    ExpectedBehavior = "Batch work item retrieval is supported",
                    ObservedBehavior = "Work Items Batch API endpoint responded successfully"
                };
            }

            return CreateFailureResult(
                "batch-read",
                "Efficient work item synchronization",
                "Batch work item retrieval is supported",
                $"HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "batch-read",
                "Efficient work item synchronization",
                "Batch work item retrieval is supported",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyWorkItemRevisionsAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient();

            // Work item revisions are collection-scoped (work item IDs are unique across collection)
            var url = CollectionUrl(config, "_apis/wit/workitems/1/revisions");
            var response = await httpClient.GetAsync(url, cancellationToken);

            // We expect either 200 (work item exists with revisions) or 404 (work item doesn't exist)
            // Both indicate the API endpoint is available
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "work-item-revisions",
                    Success = true,
                    ImpactedFunctionality = "Work item history and change tracking",
                    ExpectedBehavior = "Work item revision history API is accessible",
                    ObservedBehavior = "Revision history endpoint responded successfully"
                };
            }

            return CreateFailureResult(
                "work-item-revisions",
                "Work item history and change tracking",
                "Work item revision history API is accessible",
                $"HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "work-item-revisions",
                "Work item history and change tracking",
                "Work item revision history API is accessible",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyPullRequestsAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient();

            // Git repositories are project-scoped (requirement #1)
            var url = ProjectUrl(config, "_apis/git/repositories");
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                var repoCount = 0;
                if (doc.RootElement.TryGetProperty("value", out var repos))
                {
                    repoCount = repos.GetArrayLength();
                }

                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "pull-requests",
                    Success = true,
                    ImpactedFunctionality = "Pull request retrieval and analysis",
                    ExpectedBehavior = "Git repositories and pull request API are accessible",
                    ObservedBehavior = $"Git repositories API accessible ({repoCount} repositories found)"
                };
            }

            return CreateFailureResult(
                "pull-requests",
                "Pull request retrieval and analysis",
                "Git repositories and pull request API are accessible",
                $"HTTP {(int)response.StatusCode}",
                CategorizeHttpError(response.StatusCode),
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "pull-requests",
                "Pull request retrieval and analysis",
                "Git repositories and pull request API are accessible",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyPipelinesAsync(
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient();

            var buildDefinitionsFound = 0;
            var buildRunsFound = 0;
            var releaseDefinitionsFound = 0;
            var releaseApiAvailable = false;
            string? releaseApiError = null;

            // Step 1: Verify build definitions API
            var buildDefsUrl = ProjectUrl(config, "_apis/build/definitions");
            var buildDefsResponse = await httpClient.GetAsync(buildDefsUrl, cancellationToken);

            if (buildDefsResponse.IsSuccessStatusCode)
            {
                using var stream = await buildDefsResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (doc.RootElement.TryGetProperty("value", out var defs))
                {
                    buildDefinitionsFound = defs.GetArrayLength();
                }
            }
            else
            {
                return CreateFailureResult(
                    "pipelines",
                    "Pipeline and build status monitoring",
                    "Build/release definitions and runs are accessible",
                    $"Build definitions API failed: HTTP {(int)buildDefsResponse.StatusCode}",
                    CategorizeHttpError(buildDefsResponse.StatusCode),
                    await buildDefsResponse.Content.ReadAsStringAsync(cancellationToken));
            }

            // Step 2: Verify build runs API (if definitions exist)
            if (buildDefinitionsFound > 0)
            {
                var buildRunsUrl = ProjectUrl(config, "_apis/build/builds?$top=5");
                var buildRunsResponse = await httpClient.GetAsync(buildRunsUrl, cancellationToken);

                if (buildRunsResponse.IsSuccessStatusCode)
                {
                    using var stream = await buildRunsResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var runs))
                    {
                        buildRunsFound = runs.GetArrayLength();
                    }
                }
            }

            // Step 3: Try release definitions API (soft failure - may not be available)
            try
            {
                var releaseDefsUrl = ProjectUrl(config, "_apis/release/definitions");
                var releaseDefsResponse = await httpClient.GetAsync(releaseDefsUrl, cancellationToken);

                if (releaseDefsResponse.IsSuccessStatusCode)
                {
                    releaseApiAvailable = true;
                    using var stream = await releaseDefsResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var defs))
                    {
                        releaseDefinitionsFound = defs.GetArrayLength();
                    }
                }
                else
                {
                    releaseApiError = $"HTTP {(int)releaseDefsResponse.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                // Release API failure is a soft failure (on-prem variance)
                releaseApiError = ex.Message;
                _logger.LogWarning("Release definitions API not available: {Error}", ex.Message);
            }

            // Build the observed behavior string
            var observedParts = new List<string>
            {
                $"Build definitions: {buildDefinitionsFound}",
                $"Build runs: {buildRunsFound}"
            };

            if (releaseApiAvailable)
            {
                observedParts.Add($"Release definitions: {releaseDefinitionsFound}");
            }
            else
            {
                observedParts.Add($"Release API: Not available ({releaseApiError ?? "unknown"})");
            }

            return new TfsCapabilityCheckResult
            {
                CapabilityId = "pipelines",
                Success = true,
                ImpactedFunctionality = "Pipeline and build status monitoring",
                ExpectedBehavior = "Build/release definitions and runs are accessible",
                ObservedBehavior = string.Join(". ", observedParts)
            };
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "pipelines",
                "Pipeline and build status monitoring",
                "Build/release definitions and runs are accessible",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message));
        }
    }

    private async Task<TfsCapabilityCheckResult> VerifyWorkItemUpdateAsync(
        TfsConfigEntity config,
        int workItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use auth-mode-specific HttpClient (requirement #2, #8)
            var httpClient = GetAuthenticatedHttpClient();

            _logger.LogInformation("Verifying work item update capability using work item {WorkItemId}", workItemId);

            // Work item GET is collection-scoped (work item IDs are unique across collection)
            var getUrl = CollectionUrl(config, $"_apis/wit/workitems/{workItemId}");
            var getResponse = await httpClient.GetAsync(getUrl, cancellationToken);

            if (!getResponse.IsSuccessStatusCode)
            {
                return CreateFailureResult(
                    "work-item-update",
                    "Work item modifications (state changes, effort updates)",
                    "Can update work item fields",
                    $"Work item {workItemId} not found or not accessible",
                    getResponse.StatusCode == HttpStatusCode.NotFound ? FailureCategory.Authorization : CategorizeHttpError(getResponse.StatusCode),
                    await getResponse.Content.ReadAsStringAsync(cancellationToken),
                    targetScope: $"Work Item #{workItemId}",
                    mutationType: MutationType.Update,
                    cleanupStatus: CleanupStatus.Skipped);
            }

            // Perform a reversible write check by adding and then removing a verification tag
            // This ensures we truly test write permissions without leaving artifacts
            var verificationTag = $"PoToolVerification_{Guid.NewGuid():N}";
            var addTagPatch = new[]
            {
                new
                {
                    op = "add",
                    path = "/fields/System.Tags",
                    value = verificationTag
                }
            };

            // Work item PATCH is collection-scoped
            var updateUrl = CollectionUrl(config, $"_apis/wit/workitems/{workItemId}");
            using var addContent = new StringContent(
                JsonSerializer.Serialize(addTagPatch),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            var addResponse = await httpClient.PatchAsync(updateUrl, addContent, cancellationToken);

            if (!addResponse.IsSuccessStatusCode)
            {
                return CreateFailureResult(
                    "work-item-update",
                    "Work item modifications (state changes, effort updates)",
                    "Can update work item fields",
                    $"HTTP {(int)addResponse.StatusCode} - Cannot add tag",
                    CategorizeHttpError(addResponse.StatusCode),
                    await addResponse.Content.ReadAsStringAsync(cancellationToken),
                    targetScope: $"Work Item #{workItemId}",
                    mutationType: MutationType.Update,
                    cleanupStatus: CleanupStatus.Skipped);
            }

            // Successfully added tag - now remove it to clean up
            // Get current tags to construct remove operation
            using var addResponseStream = await addResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var addDoc = await JsonDocument.ParseAsync(addResponseStream, cancellationToken: cancellationToken);

            var currentTags = "";
            if (addDoc.RootElement.TryGetProperty("fields", out var fields) &&
                fields.TryGetProperty("System.Tags", out var tagsElement))
            {
                currentTags = tagsElement.GetString() ?? "";
            }

            // Remove the verification tag
            var tagsWithoutVerification = string.Join("; ",
                currentTags.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => t != verificationTag));

            var removeTagPatch = new[]
            {
                new
                {
                    op = "add",
                    path = "/fields/System.Tags",
                    value = tagsWithoutVerification
                }
            };

            using var removeContent = new StringContent(
                JsonSerializer.Serialize(removeTagPatch),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            var removeResponse = await httpClient.PatchAsync(updateUrl, removeContent, cancellationToken);

            var cleanupSuccess = removeResponse.IsSuccessStatusCode;

            return new TfsCapabilityCheckResult
            {
                CapabilityId = "work-item-update",
                Success = true,
                ImpactedFunctionality = "Work item modifications (state changes, effort updates)",
                ExpectedBehavior = "Can update work item fields",
                ObservedBehavior = $"Work item {workItemId} is accessible and writable (verification tag added and removed)",
                TargetScope = $"Work Item #{workItemId}",
                MutationType = MutationType.Update,
                CleanupStatus = cleanupSuccess ? CleanupStatus.CleanedUp : CleanupStatus.Failed
            };
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                "work-item-update",
                "Work item modifications (state changes, effort updates)",
                "Can update work item fields",
                $"Exception: {ex.GetType().Name}",
                FailureCategory.EndpointUnavailable,
                SanitizeErrorMessage(ex.Message),
                targetScope: $"Work Item #{workItemId}",
                mutationType: MutationType.Update,
                cleanupStatus: CleanupStatus.NotRequired);
        }
    }

    private TfsCapabilityCheckResult CreateFailureResult(
        string capabilityId,
        string impactedFunctionality,
        string expectedBehavior,
        string observedBehavior,
        FailureCategory failureCategory,
        string rawEvidence,
        string? targetScope = null,
        MutationType? mutationType = null,
        CleanupStatus? cleanupStatus = null)
    {
        var (causes, guidance) = GetFailureGuidance(failureCategory, rawEvidence);

        return new TfsCapabilityCheckResult
        {
            CapabilityId = capabilityId,
            Success = false,
            ImpactedFunctionality = impactedFunctionality,
            ExpectedBehavior = expectedBehavior,
            ObservedBehavior = observedBehavior,
            FailureCategory = failureCategory,
            RawEvidence = TruncateEvidence(rawEvidence),
            LikelyCauses = causes,
            ResolutionGuidance = guidance,
            TargetScope = targetScope,
            MutationType = mutationType,
            CleanupStatus = cleanupStatus
        };
    }

    private FailureCategory CategorizeHttpError(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => FailureCategory.Authentication,
            HttpStatusCode.Forbidden => FailureCategory.Authorization,
            HttpStatusCode.NotFound => FailureCategory.EndpointUnavailable,
            HttpStatusCode.BadRequest => FailureCategory.QueryRestriction,
            HttpStatusCode.TooManyRequests => FailureCategory.RateLimit,
            _ when (int)statusCode >= 500 => FailureCategory.EndpointUnavailable,
            _ => FailureCategory.Unknown
        };
    }

    private (List<string> Causes, List<string> Guidance) GetFailureGuidance(
        FailureCategory category,
        string rawEvidence)
    {
        return category switch
        {
            FailureCategory.Authentication => (
                new List<string>
                {
                    "Windows authentication (NTLM) failed",
                    "TFS server does not have Windows authentication enabled",
                    "Current Windows user does not have access to the TFS server",
                    "Network connectivity issue preventing authentication"
                },
                new List<string>
                {
                    "Verify TFS server has Windows authentication enabled",
                    "Confirm your Windows account has access to the TFS project",
                    "Check if you can access the TFS server URL in a web browser",
                    "Contact TFS administrator to grant your Windows account access"
                }),

            FailureCategory.Authorization => (
                new List<string>
                {
                    "Insufficient permissions for the operation",
                    "Project does not exist or user has no access",
                    "Area Path or Iteration Path access is restricted"
                },
                new List<string>
                {
                    "Verify project name is correct",
                    "Check that your Windows account has appropriate permissions in TFS",
                    "Contact project administrator to grant access to required areas"
                }),

            FailureCategory.EndpointUnavailable => (
                new List<string>
                {
                    "TFS server is unreachable",
                    "Network connectivity issue",
                    "Server URL is incorrect",
                    "API endpoint not supported by server version"
                },
                new List<string>
                {
                    "Verify server URL is correct and accessible",
                    "Check network connectivity and firewall settings",
                    "Confirm TFS/Azure DevOps Server version is 2019 or later (API 5.1+)",
                    "Try increasing timeout settings"
                }),

            FailureCategory.MissingField => (
                new List<string>
                {
                    "Process template does not include required field",
                    "Custom field configuration is incompatible",
                    "Work item type does not support the field"
                },
                new List<string>
                {
                    "Verify process template includes required fields",
                    "Check if custom fields are properly configured",
                    "Review work item type definitions"
                }),

            FailureCategory.RateLimit => (
                new List<string>
                {
                    "Too many requests in short time period",
                    "Server throttling active"
                },
                new List<string>
                {
                    "Wait a few minutes before retrying",
                    "Reduce sync frequency",
                    "Contact administrator about rate limit increases"
                }),

            _ => (
                new List<string> { "Unexpected error occurred" },
                new List<string> { "Review error details", "Contact support if issue persists" })
        };
    }

    private string SanitizeErrorMessage(string message)
    {
        // Remove any potential sensitive information
        // This is a simple implementation - could be more sophisticated
        return message
            .Replace("Authorization", "Auth***")
            .Replace("Bearer", "***")
            .Replace("token", "***");
    }

    private string TruncateEvidence(string evidence)
    {
        const int maxLength = 500;
        if (evidence.Length <= maxLength)
            return evidence;

        return evidence.Substring(0, maxLength) + "... (truncated)";
    }

    /// <summary>
    /// Validates that TFS configuration is complete and throws TfsConfigurationException if not.
    /// This method is called when UseMockClient is false (TFS mode) to ensure that the
    /// real TFS data source can be used.
    /// </summary>
    private void ValidateTfsConfiguration(TfsConfigEntity? entity)
    {
        if (entity == null)
        {
            _logger.LogError("TFS data source is enabled but TFS configuration is not set. " +
                "Configure TFS settings via the API or use mock data source.");
            throw new TfsConfigurationException(
                "TFS data source is enabled but TFS configuration is not set. " +
                "Please configure TFS settings (URL, Project, DefaultAreaPath) before using the application, " +
                "or switch to mock data source by setting TfsIntegration:UseMockClient to true.",
                new[] { "Url", "Project", "DefaultAreaPath" });
        }

        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(entity.Url))
        {
            missingFields.Add("Url");
        }

        if (string.IsNullOrWhiteSpace(entity.Project))
        {
            missingFields.Add("Project");
        }

        if (string.IsNullOrWhiteSpace(entity.DefaultAreaPath))
        {
            missingFields.Add("DefaultAreaPath");
        }

        if (missingFields.Count > 0)
        {
            _logger.LogError("TFS configuration is incomplete. Missing fields: {MissingFields}",
                string.Join(", ", missingFields));
            throw new TfsConfigurationException(
                $"TFS configuration is incomplete. Missing required fields: {string.Join(", ", missingFields)}. " +
                "Please configure all TFS settings before using the application.",
                missingFields);
        }
    }

    // ============================================
    // BULK METHODS - Prevent N+1 query patterns
    // These methods fetch or update multiple items in optimized batch operations.
    // ============================================

    /// <summary>
    /// Returns all PR data in a single logical call. For real TFS, this still makes
    /// multiple API calls but batches them efficiently rather than per-item.
    /// Reduces call count from 1 + 3*N + Sum(iterations) to approximately 4 calls.
    /// </summary>
    public async Task<PullRequestSyncResult> GetPullRequestsWithDetailsAsync(
        string? repositoryName = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var tfsCallCount = 0;

        _logger.LogInformation("Starting bulk PR fetch with details");

        // Step 1: Get all PRs (1 call per repository, or 1 call if specific repo)
        var pullRequests = (await GetPullRequestsAsync(repositoryName, fromDate, toDate, cancellationToken)).ToList();
        tfsCallCount++; // Count as 1 logical call even though it may hit multiple repos

        if (pullRequests.Count == 0)
        {
            return new PullRequestSyncResult(
                PullRequests: pullRequests,
                Iterations: new List<PullRequestIterationDto>(),
                Comments: new List<PullRequestCommentDto>(),
                FileChanges: new List<PullRequestFileChangeDto>(),
                TfsCallCount: tfsCallCount
            );
        }

        // Group PRs by repository for efficient batching
        var prsByRepo = pullRequests.GroupBy(pr => pr.RepositoryName);

        var allIterations = new List<PullRequestIterationDto>();
        var allComments = new List<PullRequestCommentDto>();
        var allFileChanges = new List<PullRequestFileChangeDto>();

        // Step 2: For each PR, fetch iterations and comments with throttling
        // Azure DevOps doesn't have a true bulk API, but we can parallelize with bounded concurrency
        var prTasks = new List<Task<(List<PullRequestIterationDto> Iterations, List<PullRequestCommentDto> Comments, int PrId, string Repo)>>();

        foreach (var repoGroup in prsByRepo)
        {
            var repo = repoGroup.Key;
            foreach (var pr in repoGroup)
            {
                // Apply read throttling to PR details fetching
                prTasks.Add(_throttler.ExecuteReadAsync(
                    () => FetchPrDetailsAsync(pr.Id, repo, cancellationToken),
                    cancellationToken));
            }
        }

        var prResults = await Task.WhenAll(prTasks);

        // Aggregate results and count calls
        foreach (var prResult in prResults)
        {
            Interlocked.Add(ref tfsCallCount, 2); // 1 for iterations, 1 for comments
            lock (allIterations)
            {
                allIterations.AddRange(prResult.Iterations);
            }
            lock (allComments)
            {
                allComments.AddRange(prResult.Comments);
            }
        }

        // Step 3: Fetch file changes for all iterations with throttling
        var fileChangeTasks = new List<Task<IEnumerable<PullRequestFileChangeDto>>>();
        foreach (var prResult in prResults)
        {
            var repo = prResult.Repo;
            var prId = prResult.PrId;
            foreach (var iteration in prResult.Iterations)
            {
                // Apply read throttling to file changes fetching
                fileChangeTasks.Add(_throttler.ExecuteReadAsync(
                    () => GetPullRequestFileChangesAsync(prId, repo, iteration.IterationNumber, cancellationToken),
                    cancellationToken));
            }
        }

        var fileChangeResults = await Task.WhenAll(fileChangeTasks);
        tfsCallCount += fileChangeResults.Length;

        foreach (var fileChanges in fileChangeResults)
        {
            lock (allFileChanges)
            {
                allFileChanges.AddRange(fileChanges);
            }
        }

        var elapsed = DateTimeOffset.UtcNow - startTime;
        _logger.LogInformation(
            "Bulk PR fetch completed: {PrCount} PRs, {IterCount} iterations, {CommentCount} comments, {FileCount} file changes in {ElapsedMs}ms ({CallCount} TFS calls)",
            pullRequests.Count, allIterations.Count, allComments.Count, allFileChanges.Count,
            elapsed.TotalMilliseconds, tfsCallCount);

        return new PullRequestSyncResult(
            PullRequests: pullRequests,
            Iterations: allIterations,
            Comments: allComments,
            FileChanges: allFileChanges,
            TfsCallCount: tfsCallCount
        );
    }

    /// <summary>
    /// Helper method to fetch PR details (iterations and comments) in parallel.
    /// </summary>
    private async Task<(List<PullRequestIterationDto> Iterations, List<PullRequestCommentDto> Comments, int PrId, string Repo)> FetchPrDetailsAsync(
        int prId, string repo, CancellationToken cancellationToken)
    {
        // Fetch iterations and comments concurrently
        var iterationsTask = GetPullRequestIterationsAsync(prId, repo, cancellationToken);
        var commentsTask = GetPullRequestCommentsAsync(prId, repo, cancellationToken);

        await Task.WhenAll(iterationsTask, commentsTask);

        return (iterationsTask.Result.ToList(), commentsTask.Result.ToList(), prId, repo);
    }

    /// <summary>
    /// Updates effort for multiple work items in a batch. 
    /// Azure DevOps doesn't have a true bulk update API, but we can batch the requests
    /// and track them as a single logical operation for performance monitoring.
    /// </summary>
    public async Task<BulkUpdateResult> UpdateWorkItemsEffortAsync(
        IEnumerable<WorkItemEffortUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        var updatesList = updates.ToList();
        var tfsCallCount = 0;

        _logger.LogInformation("Starting bulk effort update for {Count} work items", updatesList.Count);

        var results = new List<BulkUpdateItemResult>();
        var successCount = 0;
        var failedCount = 0;

        // Process updates with write throttling - Azure DevOps requires individual PATCH calls per work item
        // Apply bounded concurrency to prevent overwhelming the server
        var updateTasks = updatesList.Select(async update =>
        {
            try
            {
                if (update.EffortValue < 0)
                {
                    return new BulkUpdateItemResult(update.WorkItemId, false, $"Invalid effort value {update.EffortValue} (must be >= 0)");
                }

                // Apply write throttling to effort updates (PATCH operations)
                var success = await _throttler.ExecuteWriteAsync(
                    () => UpdateWorkItemEffortAsync(update.WorkItemId, update.EffortValue, cancellationToken),
                    cancellationToken);
                Interlocked.Increment(ref tfsCallCount);

                return new BulkUpdateItemResult(update.WorkItemId, success,
                    success ? null : $"TFS update failed for work item {update.WorkItemId}");
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref tfsCallCount);
                return new BulkUpdateItemResult(update.WorkItemId, false, ex.Message);
            }
        });

        var taskResults = await Task.WhenAll(updateTasks);
        results.AddRange(taskResults);
        successCount = results.Count(r => r.Success);
        failedCount = results.Count(r => !r.Success);

        _logger.LogInformation("Bulk effort update completed: {Success}/{Total} succeeded ({CallCount} TFS calls)",
            successCount, updatesList.Count, tfsCallCount);

        return new BulkUpdateResult(
            TotalRequested: updatesList.Count,
            SuccessfulUpdates: successCount,
            FailedUpdates: failedCount,
            Results: results,
            TfsCallCount: tfsCallCount
        );
    }

    /// <summary>
    /// Updates state for multiple work items in a batch.
    /// Azure DevOps doesn't have a true bulk update API, but we batch and parallelize.
    /// </summary>
    public async Task<BulkUpdateResult> UpdateWorkItemsStateAsync(
        IEnumerable<WorkItemStateUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        var updatesList = updates.ToList();
        var tfsCallCount = 0;

        _logger.LogInformation("Starting bulk state update for {Count} work items", updatesList.Count);

        var results = new List<BulkUpdateItemResult>();
        var successCount = 0;
        var failedCount = 0;

        // Process updates with write throttling to prevent overwhelming the server
        var updateTasks = updatesList.Select(async update =>
        {
            try
            {
                // Apply write throttling to state updates (PATCH operations)
                var success = await _throttler.ExecuteWriteAsync(
                    () => UpdateWorkItemStateAsync(update.WorkItemId, update.NewState, cancellationToken),
                    cancellationToken);
                Interlocked.Increment(ref tfsCallCount);

                return new BulkUpdateItemResult(update.WorkItemId, success,
                    success ? null : $"TFS update failed for work item {update.WorkItemId}");
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref tfsCallCount);
                return new BulkUpdateItemResult(update.WorkItemId, false, ex.Message);
            }
        });

        var taskResults = await Task.WhenAll(updateTasks);
        results.AddRange(taskResults);
        successCount = results.Count(r => r.Success);
        failedCount = results.Count(r => !r.Success);

        _logger.LogInformation("Bulk state update completed: {Success}/{Total} succeeded ({CallCount} TFS calls)",
            successCount, updatesList.Count, tfsCallCount);

        return new BulkUpdateResult(
            TotalRequested: updatesList.Count,
            SuccessfulUpdates: successCount,
            FailedUpdates: failedCount,
            Results: results,
            TfsCallCount: tfsCallCount
        );
    }

    /// <summary>
    /// Gets revision history for multiple work items in a batch.
    /// Uses parallel requests to TFS for improved performance.
    /// </summary>
    public async Task<IDictionary<int, IEnumerable<WorkItemRevisionDto>>> GetWorkItemRevisionsBatchAsync(
        IEnumerable<int> workItemIds,
        CancellationToken cancellationToken = default)
    {
        var idsList = workItemIds.ToList();
        var tfsCallCount = 0;

        _logger.LogInformation("Starting bulk revision fetch for {Count} work items", idsList.Count);

        var results = new Dictionary<int, IEnumerable<WorkItemRevisionDto>>();
        var lockObj = new object();

        // Fetch revisions with read throttling to prevent overwhelming the server
        var fetchTasks = idsList.Select(async workItemId =>
        {
            try
            {
                // Apply read throttling to revision fetching (GET operations)
                var revisions = await _throttler.ExecuteReadAsync(
                    () => GetWorkItemRevisionsAsync(workItemId, cancellationToken),
                    cancellationToken);
                Interlocked.Increment(ref tfsCallCount);

                lock (lockObj)
                {
                    results[workItemId] = revisions;
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref tfsCallCount);
                _logger.LogWarning(ex, "Failed to fetch revisions for work item {WorkItemId}", workItemId);
                lock (lockObj)
                {
                    results[workItemId] = Enumerable.Empty<WorkItemRevisionDto>();
                }
            }
        });

        await Task.WhenAll(fetchTasks);

        _logger.LogInformation("Bulk revision fetch completed: {Count} work items ({CallCount} TFS calls)",
            results.Count, tfsCallCount);

        return results;
    }

    // ============================================
    // WORK ITEM CREATION
    // ============================================

    public async Task<WorkItemCreateResult> CreateWorkItemAsync(
        WorkItemCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating {WorkItemType} work item with title '{Title}'",
                request.WorkItemType, request.Title);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for creating work item");
                return new WorkItemCreateResult
                {
                    Success = false,
                    ErrorMessage = "TFS configuration not found"
                };
            }

            // Use auth-mode-specific HttpClient (requirement #2)
            var httpClient = GetAuthenticatedHttpClient();

            // Build JSON Patch document for work item creation
            // Note: AreaPath must be provided in the request since it's not stored in TfsConfigEntity
            var patchOperations = new List<object>
            {
                new { op = "add", path = "/fields/System.Title", value = request.Title }
            };

            if (!string.IsNullOrEmpty(request.AreaPath))
            {
                patchOperations.Add(new { op = "add", path = "/fields/System.AreaPath", value = request.AreaPath });
            }

            if (!string.IsNullOrEmpty(request.IterationPath))
            {
                patchOperations.Add(new { op = "add", path = "/fields/System.IterationPath", value = request.IterationPath });
            }

            if (request.Effort.HasValue)
            {
                patchOperations.Add(new { op = "add", path = $"/fields/{TfsFieldEffort}", value = request.Effort.Value });
            }

            if (!string.IsNullOrEmpty(request.Description))
            {
                patchOperations.Add(new { op = "add", path = "/fields/System.Description", value = request.Description });
            }

            // Add parent link if specified
            if (request.ParentId.HasValue)
            {
                patchOperations.Add(new
                {
                    op = "add",
                    path = "/relations/-",
                    value = new
                    {
                        rel = "System.LinkTypes.Hierarchy-Reverse",
                        url = $"{entity.Url.TrimEnd('/')}/_apis/wit/workItems/{request.ParentId.Value}"
                    }
                });
            }

            // URL encode the work item type for the API call
            // Work item creation IS project-scoped per Azure DevOps REST API:
            // POST https://dev.azure.com/{organization}/{project}/_apis/wit/workitems/${type}
            // This is different from batch read which is collection-scoped.
            var encodedType = Uri.EscapeDataString(request.WorkItemType);
            var createUrl = ProjectUrl(entity, $"_apis/wit/workitems/${encodedType}");

            using var content = new StringContent(
                JsonSerializer.Serialize(patchOperations),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogDebug("Sending POST request to create work item at {Url}", createUrl);

            var response = await httpClient.PostAsync(createUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = JsonDocument.Parse(responseBody);
                var workItemId = jsonDoc.RootElement.GetProperty("id").GetInt32();

                _logger.LogInformation("Successfully created work item {WorkItemId} of type {WorkItemType}",
                    workItemId, request.WorkItemType);

                return new WorkItemCreateResult
                {
                    Success = true,
                    WorkItemId = workItemId
                };
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to create work item. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseBody);

                return new WorkItemCreateResult
                {
                    Success = false,
                    ErrorMessage = $"TFS API returned {response.StatusCode}: {responseBody}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work item of type {WorkItemType}", request.WorkItemType);
            return new WorkItemCreateResult
            {
                Success = false,
                ErrorMessage = $"Error creating work item: {ex.Message}"
            };
        }
    }

    public async Task<bool> UpdateWorkItemParentAsync(
        int workItemId,
        int newParentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} parent to {NewParentId}",
                workItemId, newParentId);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item parent");
                return false;
            }

            // Use auth-mode-specific HttpClient (requirement #2)
            var httpClient = GetAuthenticatedHttpClient();

            // Create per-request timeout token for write operation
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(entity.TimeoutSeconds));

            // Step 1: GET the child work item with $expand=relations to find existing parent
            var getUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}?$expand=relations");
            var getResponse = await httpClient.GetAsync(getUrl, timeoutCts.Token);

            if (!getResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get work item {WorkItemId} for parent update. Status: {StatusCode}",
                    workItemId, getResponse.StatusCode);
                return false;
            }

            using var getStream = await getResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var getDoc = await JsonDocument.ParseAsync(getStream, cancellationToken: cancellationToken);

            // Step 2: Find and remove existing parent relations
            var patchOperations = new List<object>();

            if (getDoc.RootElement.TryGetProperty("relations", out var relations))
            {
                var relationsList = relations.EnumerateArray().ToList();

                // Find all parent relations (System.LinkTypes.Hierarchy-Reverse)
                for (int i = 0; i < relationsList.Count; i++)
                {
                    var relation = relationsList[i];
                    if (relation.TryGetProperty("rel", out var rel) &&
                        rel.GetString() == "System.LinkTypes.Hierarchy-Reverse")
                    {
                        // Remove existing parent relation by index
                        patchOperations.Add(new
                        {
                            op = "remove",
                            path = $"/relations/{i}"
                        });

                        _logger.LogDebug("Removing existing parent relation at index {Index} for work item {WorkItemId}",
                            i, workItemId);
                    }
                }
            }

            // Step 3: Add the new parent relation
            patchOperations.Add(new
            {
                op = "add",
                path = "/relations/-",
                value = new
                {
                    rel = "System.LinkTypes.Hierarchy-Reverse",
                    url = $"{entity.Url.TrimEnd('/')}/_apis/wit/workItems/{newParentId}"
                }
            });

            // Work item PATCH is collection-scoped
            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");

            using var content = new StringContent(
                JsonSerializer.Serialize(patchOperations),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogDebug("Sending PATCH request to update work item {WorkItemId} parent with {OpCount} operations",
                workItemId, patchOperations.Count);

            // PATCH operations are NOT retried - they are non-idempotent
            var response = await httpClient.PatchAsync(updateUrl, content, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} parent to {NewParentId}",
                    workItemId, newParentId);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId} parent. Status: {StatusCode}, Response: {Response}",
                    workItemId, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Update work item {WorkItemId} parent timed out", workItemId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} parent to {NewParentId}",
                workItemId, newParentId);
            return false;
        }
    }

    // ============================================
    // PIPELINE METHODS
    // ============================================

    public async Task<IEnumerable<PipelineDto>> GetPipelinesAsync(
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // Build definitions are project-scoped (requirement #1)
            var buildUrl = ProjectUrl(config, "_apis/build/definitions");
            var buildResponse = await httpClient.GetAsync(buildUrl, cancellationToken);

            var pipelines = new List<PipelineDto>();

            if (buildResponse.IsSuccessStatusCode)
            {
                using var stream = await buildResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (doc.RootElement.TryGetProperty("value", out var valueArray))
                {
                    foreach (var def in valueArray.EnumerateArray())
                    {
                        var id = def.GetProperty("id").GetInt32();
                        var name = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var path = def.TryGetProperty("path", out var p) ? p.GetString() : null;

                        pipelines.Add(new PipelineDto(
                            Id: id,
                            Name: name,
                            Type: PipelineType.Build,
                            Path: path,
                            RetrievedAt: DateTimeOffset.UtcNow
                        ));
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to get build definitions: {StatusCode}", buildResponse.StatusCode);
            }

            // Try to get release definitions (may not be available in all TFS versions)
            // Release definitions are project-scoped (requirement #1)
            try
            {
                var releaseUrl = ProjectUrl(config, "_apis/release/definitions");
                var releaseResponse = await httpClient.GetAsync(releaseUrl, cancellationToken);

                if (releaseResponse.IsSuccessStatusCode)
                {
                    using var stream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var def in valueArray.EnumerateArray())
                        {
                            var id = def.GetProperty("id").GetInt32();
                            var name = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            var path = def.TryGetProperty("path", out var p) ? p.GetString() : null;

                            // Offset release IDs to avoid collision with build IDs
                            pipelines.Add(new PipelineDto(
                                Id: id + ReleaseIdOffset,
                                Name: name,
                                Type: PipelineType.Release,
                                Path: path,
                                RetrievedAt: DateTimeOffset.UtcNow
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Release definitions API not available, skipping release pipelines");
            }

            _logger.LogInformation("Retrieved {Count} pipeline definitions", pipelines.Count);
            return pipelines;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PipelineRunDto>> GetPipelineRunsAsync(
        int pipelineId,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        // Use auth-mode-specific HttpClient (requirement #2)
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            var runs = new List<PipelineRunDto>();
            var isRelease = pipelineId >= ReleaseIdOffset;
            var actualId = isRelease ? pipelineId - ReleaseIdOffset : pipelineId;

            if (isRelease)
            {
                // Release runs are project-scoped (requirement #1)
                var url = ProjectUrl(config, $"_apis/release/releases?definitionId={actualId}&$top={top}");
                var response = await httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var run in valueArray.EnumerateArray())
                        {
                            runs.Add(ParseReleaseRun(run, pipelineId));
                        }
                    }
                }
            }
            else
            {
                // Build runs are project-scoped (requirement #1)
                var url = ProjectUrl(config, $"_apis/build/builds?definitions={pipelineId}&$top={top}");
                var response = await httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                    if (doc.RootElement.TryGetProperty("value", out var valueArray))
                    {
                        foreach (var run in valueArray.EnumerateArray())
                        {
                            runs.Add(ParseBuildRun(run, pipelineId));
                        }
                    }
                }
            }

            _logger.LogInformation("Retrieved {Count} runs for pipeline {PipelineId}", runs.Count, pipelineId);
            return runs;
        }, cancellationToken);
    }

    public async Task<PipelineSyncResult> GetPipelinesWithRunsAsync(
        int runsPerPipeline = 50,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var tfsCallCount = 0;

        _logger.LogInformation("Starting bulk pipeline fetch with runs");

        // Step 1: Get all pipelines
        var pipelines = (await GetPipelinesAsync(cancellationToken)).ToList();
        tfsCallCount++;

        if (pipelines.Count == 0)
        {
            return new PipelineSyncResult(
                Pipelines: pipelines,
                Runs: new List<PipelineRunDto>(),
                TfsCallCount: tfsCallCount,
                SyncedAt: DateTimeOffset.UtcNow
            );
        }

        // Step 2: Fetch runs for each pipeline with read throttling
        var allRuns = new List<PipelineRunDto>();
        var lockObj = new object();

        var fetchTasks = pipelines.Select(async pipeline =>
        {
            // Apply read throttling to pipeline run fetching (GET operations)
            var runs = await _throttler.ExecuteReadAsync(
                () => GetPipelineRunsAsync(pipeline.Id, runsPerPipeline, cancellationToken),
                cancellationToken);
            Interlocked.Increment(ref tfsCallCount);

            lock (lockObj)
            {
                allRuns.AddRange(runs);
            }
        });

        await Task.WhenAll(fetchTasks);

        var elapsed = DateTimeOffset.UtcNow - startTime;
        _logger.LogInformation(
            "Bulk pipeline fetch completed: {PipelineCount} pipelines, {RunCount} runs in {ElapsedMs}ms ({CallCount} TFS calls)",
            pipelines.Count, allRuns.Count, elapsed.TotalMilliseconds, tfsCallCount);

        return new PipelineSyncResult(
            Pipelines: pipelines,
            Runs: allRuns,
            TfsCallCount: tfsCallCount,
            SyncedAt: DateTimeOffset.UtcNow
        );
    }

    private PipelineRunDto ParseBuildRun(JsonElement run, int pipelineId)
    {
        var runId = run.GetProperty("id").GetInt32();
        var pipelineName = "";
        if (run.TryGetProperty("definition", out var def))
        {
            pipelineName = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        }

        DateTimeOffset? startTime = null;
        if (run.TryGetProperty("startTime", out var st) && st.ValueKind != JsonValueKind.Null)
        {
            startTime = st.GetDateTimeOffset();
        }

        DateTimeOffset? finishTime = null;
        if (run.TryGetProperty("finishTime", out var ft) && ft.ValueKind != JsonValueKind.Null)
        {
            finishTime = ft.GetDateTimeOffset();
        }

        var duration = (startTime.HasValue && finishTime.HasValue)
            ? (TimeSpan?)(finishTime.Value - startTime.Value)
            : null;

        var resultStr = run.TryGetProperty("result", out var r) ? r.GetString() ?? "" : "";
        var result = ParseBuildResult(resultStr);

        var reasonStr = run.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : "";
        var trigger = ParseBuildTrigger(reasonStr);

        var branch = run.TryGetProperty("sourceBranch", out var b) ? b.GetString() : null;

        var requestedFor = "";
        if (run.TryGetProperty("requestedFor", out var req))
        {
            requestedFor = req.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
        }

        return new PipelineRunDto(
            RunId: runId,
            PipelineId: pipelineId,
            PipelineName: pipelineName,
            StartTime: startTime,
            FinishTime: finishTime,
            Duration: duration,
            Result: result,
            Trigger: trigger,
            TriggerInfo: reasonStr,
            Branch: branch,
            RequestedFor: requestedFor,
            RetrievedAt: DateTimeOffset.UtcNow
        );
    }

    private PipelineRunDto ParseReleaseRun(JsonElement run, int pipelineId)
    {
        var runId = run.GetProperty("id").GetInt32();
        var pipelineName = "";
        if (run.TryGetProperty("releaseDefinition", out var def))
        {
            pipelineName = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        }

        DateTimeOffset? startTime = null;
        if (run.TryGetProperty("createdOn", out var st))
        {
            startTime = st.GetDateTimeOffset();
        }

        DateTimeOffset? finishTime = null;
        if (run.TryGetProperty("modifiedOn", out var ft))
        {
            finishTime = ft.GetDateTimeOffset();
        }

        var duration = (startTime.HasValue && finishTime.HasValue)
            ? (TimeSpan?)(finishTime.Value - startTime.Value)
            : null;

        var statusStr = run.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
        var result = ParseReleaseResult(statusStr);

        var reasonStr = run.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : "";
        var trigger = ParseReleaseTrigger(reasonStr);

        var requestedFor = "";
        if (run.TryGetProperty("createdBy", out var req))
        {
            requestedFor = req.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
        }

        return new PipelineRunDto(
            RunId: runId + ReleaseIdOffset, // Offset to avoid collision
            PipelineId: pipelineId,
            PipelineName: pipelineName,
            StartTime: startTime,
            FinishTime: finishTime,
            Duration: duration,
            Result: result,
            Trigger: trigger,
            TriggerInfo: reasonStr,
            Branch: null,
            RequestedFor: requestedFor,
            RetrievedAt: DateTimeOffset.UtcNow
        );
    }

    private static PipelineRunResult ParseBuildResult(string result)
    {
        return result.ToLowerInvariant() switch
        {
            "succeeded" => PipelineRunResult.Succeeded,
            "failed" => PipelineRunResult.Failed,
            "partiallysucceeded" => PipelineRunResult.PartiallySucceeded,
            "canceled" => PipelineRunResult.Canceled,
            "none" => PipelineRunResult.None,
            _ => PipelineRunResult.Unknown
        };
    }

    private static PipelineRunResult ParseReleaseResult(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "succeeded" or "active" => PipelineRunResult.Succeeded,
            "failed" or "rejected" => PipelineRunResult.Failed,
            "abandoned" or "canceled" => PipelineRunResult.Canceled,
            "undefined" or "draft" => PipelineRunResult.None,
            _ => PipelineRunResult.Unknown
        };
    }

    private static PipelineRunTrigger ParseBuildTrigger(string reason)
    {
        // Requirement #7: Fix case-sensitive enum parsing
        // All literals must be lowercase since we apply ToLowerInvariant()
        return reason.ToLowerInvariant() switch
        {
            "manual" or "usercreated" => PipelineRunTrigger.Manual,
            "individualci" or "batchedci" => PipelineRunTrigger.ContinuousIntegration,
            "schedule" => PipelineRunTrigger.Schedule,
            "pullrequest" => PipelineRunTrigger.PullRequest,
            "buildcompletion" => PipelineRunTrigger.BuildCompletion,
            "resourcetrigger" => PipelineRunTrigger.ResourceTrigger,
            _ => PipelineRunTrigger.Unknown
        };
    }

    private static PipelineRunTrigger ParseReleaseTrigger(string reason)
    {
        // Requirement #7: Fix case-sensitive enum parsing
        // All literals must be lowercase since we apply ToLowerInvariant()
        return reason.ToLowerInvariant() switch
        {
            "manual" => PipelineRunTrigger.Manual,
            "continuousintegration" => PipelineRunTrigger.ContinuousIntegration,
            "schedule" => PipelineRunTrigger.Schedule,
            "pullrequest" => PipelineRunTrigger.PullRequest,
            _ => PipelineRunTrigger.Unknown
        };
    }

    /// <summary>
    /// Completes the work item hierarchy by fetching missing ancestors (parents).
    /// After discovering descendants, some items may reference parents that aren't in the fetched set.
    /// This method walks UP the hierarchy to fetch those missing parents and their relations.
    /// </summary>
    /// <param name="config">TFS configuration.</param>
    /// <param name="httpClient">HTTP client for TFS requests.</param>
    /// <param name="allWorkItemIds">The set of all work item IDs (will be modified to add ancestors).</param>
    /// <param name="relationsMap">The map of work item ID to parent ID (will be modified with new relations).</param>
    /// <param name="progressCallback">Optional progress callback.</param>
    /// <param name="updateHeartbeat">Callback to update activity heartbeat.</param>
    /// <param name="checkInactivity">Callback to check for inactivity timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of ancestor work items added.</returns>
    private async Task<int> CompleteAncestorsAsync(
        TfsConfigEntity config,
        HttpClient httpClient,
        HashSet<int> allWorkItemIds,
        Dictionary<int, int?> relationsMap,
        Action<int, int, string>? progressCallback,
        Action updateHeartbeat,
        Action checkInactivity,
        CancellationToken cancellationToken)
    {
        var ancestorsAdded = 0;
        var visitedAncestors = new HashSet<int>();
        var missingParentIds = new HashSet<int>();
        
        // Step 1: Find all parent IDs that are not in our current set
        foreach (var (childId, parentId) in relationsMap)
        {
            if (parentId.HasValue && !allWorkItemIds.Contains(parentId.Value))
            {
                missingParentIds.Add(parentId.Value);
            }
        }

        if (missingParentIds.Count == 0)
        {
            _logger.LogInformation("No missing parent IDs found - hierarchy is complete");
            return 0;
        }

        _logger.LogInformation("Found {Count} missing parent IDs, walking up hierarchy to fetch ancestors", 
            missingParentIds.Count);

        progressCallback?.Invoke(2, 3, $"Completing ancestors ({missingParentIds.Count} missing parents)...");

        var currentDepth = 0;
        var parentsToFetch = new Queue<int>(missingParentIds);
        var hasMoreParents = parentsToFetch.Count > 0;

        // Step 2: Walk up the hierarchy iteratively
        while (hasMoreParents && currentDepth < MaxAncestorDepth && ancestorsAdded < MaxAncestorCount)
        {
            checkInactivity();
            currentDepth++;

            var batchToFetch = new List<int>();
            while (parentsToFetch.Count > 0 && batchToFetch.Count < WorkItemBatchSize)
            {
                var parentId = parentsToFetch.Dequeue();
                if (!visitedAncestors.Contains(parentId) && !allWorkItemIds.Contains(parentId))
                {
                    batchToFetch.Add(parentId);
                    visitedAncestors.Add(parentId);
                }
            }

            if (batchToFetch.Count == 0)
            {
                break;
            }

            _logger.LogDebug("Ancestor completion depth {Depth}: Fetching {Count} parent IDs", 
                currentDepth, batchToFetch.Count);

            progressCallback?.Invoke(2, 3, $"Fetching ancestors (depth {currentDepth}, {batchToFetch.Count} items)...");

            // Fetch relations for this batch of parents
            var relationsRequest = new WorkItemBatchRequest
            {
                Ids = batchToFetch.ToArray(),
                Expand = "relations"
            };

            var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");
            using var relationsContent = new StringContent(
                JsonSerializer.Serialize(relationsRequest),
                System.Text.Encoding.UTF8,
                "application/json");

            try
            {
                var relationsResponse = await httpClient.PostAsync(batchUrl, relationsContent, cancellationToken);
                updateHeartbeat();
                await HandleHttpErrorsAsync(relationsResponse, cancellationToken);

                using var relationsStream = await relationsResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var relationsDoc = await JsonDocument.ParseAsync(relationsStream, cancellationToken: cancellationToken);

                // Process the fetched ancestors
                foreach (var item in relationsDoc.RootElement.GetProperty("value").EnumerateArray())
                {
                    var id = item.GetProperty("id").GetInt32();
                    
                    // Add to our set of IDs
                    if (allWorkItemIds.Add(id))
                    {
                        ancestorsAdded++;
                        
                        // Extract parent ID from this ancestor
                        var parentId = ExtractParentIdFromRelations(item);
                        relationsMap[id] = parentId;
                        
                        // If this ancestor has a parent that we don't have yet, queue it
                        if (parentId.HasValue && 
                            !allWorkItemIds.Contains(parentId.Value) && 
                            !visitedAncestors.Contains(parentId.Value))
                        {
                            parentsToFetch.Enqueue(parentId.Value);
                        }
                    }
                }

                _logger.LogDebug("Ancestor completion depth {Depth}: Added {Count} ancestors, {Remaining} parents queued", 
                    currentDepth, batchToFetch.Count, parentsToFetch.Count);
                
                // Update loop condition tracker
                hasMoreParents = parentsToFetch.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching ancestors at depth {Depth}, continuing with partial hierarchy", currentDepth);
                // Continue with partial results rather than failing completely
                hasMoreParents = false;
                break;
            }
        }

        if (currentDepth >= MaxAncestorDepth)
        {
            _logger.LogWarning("Ancestor completion reached max depth {MaxDepth}, stopping (possible cycle or very deep hierarchy)", 
                MaxAncestorDepth);
        }

        if (ancestorsAdded >= MaxAncestorCount)
        {
            _logger.LogWarning("Ancestor completion reached max total ancestors {MaxTotal}, stopping", 
                MaxAncestorCount);
        }

        return ancestorsAdded;
    }

    // ============================================
    // PIPELINE DEFINITION METHODS (YAML) - API 7.0
    // ============================================

    public async Task<string?> GetRepositoryIdByNameAsync(
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // GET {ServerUri}/{Project}/_apis/git/repositories?api-version=7.0
            var url = ProjectUrl(config, "_apis/git/repositories");
            _logger.LogDebug("Fetching Git repositories from: {Url}", url);

            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get Git repositories: {StatusCode}", response.StatusCode);
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("value", out var valueArray))
            {
                _logger.LogWarning("Git repositories response missing 'value' array");
                return null;
            }

            // Find repository by name (case-insensitive)
            foreach (var repo in valueArray.EnumerateArray())
            {
                if (repo.TryGetProperty("name", out var nameElement))
                {
                    var name = nameElement.GetString();
                    if (string.Equals(name, repositoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (repo.TryGetProperty("id", out var idElement))
                        {
                            var repoId = idElement.GetString();
                            _logger.LogInformation("Found repository '{RepoName}' with ID: {RepoId}", repositoryName, repoId);
                            return repoId;
                        }
                    }
                }
            }

            _logger.LogWarning("Repository '{RepoName}' not found in project '{Project}'", repositoryName, config.Project);
            return null;
        }, cancellationToken);
    }

    public async Task<IEnumerable<PipelineDefinitionDto>> GetPipelineDefinitionsForRepositoryAsync(
        string repositoryName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            var definitions = new List<PipelineDefinitionDto>();

            // Step 1: Get repository ID
            var repoId = await GetRepositoryIdByNameAsync(repositoryName, cancellationToken);
            if (string.IsNullOrEmpty(repoId))
            {
                _logger.LogWarning("Cannot fetch pipeline definitions for repository '{RepoName}' - repository ID not found", repositoryName);
                return definitions;
            }

            // Step 2: Get all build definitions with full properties
            // GET {ServerUri}/{Project}/_apis/build/definitions?api-version=7.0&includeAllProperties=true
            var url = ProjectUrl(config, "_apis/build/definitions?includeAllProperties=true");
            _logger.LogDebug("Fetching build definitions from: {Url}", url);

            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get build definitions: {StatusCode}", response.StatusCode);
                return definitions;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("value", out var valueArray))
            {
                _logger.LogDebug("Build definitions response missing 'value' array");
                return definitions;
            }

            var syncTime = DateTimeOffset.UtcNow;
            int processedCount = 0;
            int filteredCount = 0;

            foreach (var def in valueArray.EnumerateArray())
            {
                processedCount++;

                // Filter by repository: check definition.repository.id or definition.repository.name
                if (!def.TryGetProperty("repository", out var repository))
                {
                    _logger.LogDebug("Build definition {DefId} has no 'repository' property, skipping", 
                        def.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : -1);
                    continue;
                }

                // Preferred: match by repository.id (GUID)
                bool matchesRepo = false;
                if (repository.TryGetProperty("id", out var repoIdElement))
                {
                    var defRepoId = repoIdElement.GetString();
                    if (string.Equals(defRepoId, repoId, StringComparison.OrdinalIgnoreCase))
                    {
                        matchesRepo = true;
                    }
                }

                // Fallback: match by repository.name
                if (!matchesRepo && repository.TryGetProperty("name", out var repoNameElement))
                {
                    var defRepoName = repoNameElement.GetString();
                    if (string.Equals(defRepoName, repositoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchesRepo = true;
                    }
                }

                if (!matchesRepo)
                {
                    continue;
                }

                filteredCount++;

                // Extract definition properties
                var pipelineId = def.GetProperty("id").GetInt32();
                var name = def.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                
                // Extract YAML path from process.yamlFilename
                string? yamlPath = null;
                if (def.TryGetProperty("process", out var process))
                {
                    if (process.TryGetProperty("yamlFilename", out var yamlFileElement))
                    {
                        var rawPath = yamlFileElement.GetString();
                        if (!string.IsNullOrEmpty(rawPath))
                        {
                            // Normalize: ensure leading /
                            yamlPath = rawPath.StartsWith("/") ? rawPath : $"/{rawPath}";
                        }
                    }
                }

                // Extract folder/path
                var folder = def.TryGetProperty("path", out var p) ? p.GetString() : null;

                // Extract web URL
                string? url = null;
                if (def.TryGetProperty("_links", out var links))
                {
                    if (links.TryGetProperty("web", out var web))
                    {
                        if (web.TryGetProperty("href", out var href))
                        {
                            url = href.GetString();
                        }
                    }
                }

                var dto = new PipelineDefinitionDto
                {
                    PipelineDefinitionId = pipelineId,
                    RepoId = repoId,
                    RepoName = repositoryName,
                    Name = name,
                    YamlPath = yamlPath,
                    Folder = folder,
                    Url = url,
                    LastSyncedUtc = syncTime
                };

                definitions.Add(dto);

                _logger.LogDebug(
                    "Mapped pipeline definition: ID={Id}, Name={Name}, YamlPath={YamlPath}",
                    pipelineId, name, yamlPath ?? "(none)");
            }

            _logger.LogInformation(
                "Retrieved {Count} pipeline definitions for repository '{RepoName}' (processed {Processed}, filtered {Filtered})",
                definitions.Count, repositoryName, processedCount, filteredCount);

            return (IEnumerable<PipelineDefinitionDto>)definitions;
        }, cancellationToken);
    }
}
