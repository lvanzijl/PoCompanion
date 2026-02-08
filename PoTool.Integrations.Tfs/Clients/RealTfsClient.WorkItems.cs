using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Integrations.Tfs.Models.Internal;
using PoTool.Shared.Settings;
using PoTool.Core.WorkItems;
using PoTool.Shared.Exceptions;
using PoTool.Shared.WorkItems;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Real Azure DevOps/TFS REST client implementation - Work Items operations.
/// Contains methods for validating connections, fetching area paths, and retrieving work items.
/// </summary>
public partial class RealTfsClient
{
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

            // Step 1: Validate server connectivity using collection-scoped projects endpoint
            var projectsUrl = CollectionUrl(entity, "_apis/projects");
            _logger.LogInformation("Validating TFS connection: GET {Url} (using NTLM authentication)", projectsUrl);

            var resp = await SendGetAsync(httpClient, entity, projectsUrl, cancellationToken, handleErrors: false);

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

            var projectResp = await SendGetAsync(httpClient, entity, projectUrl, cancellationToken, handleErrors: false);

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
        catch (TimeoutException)
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

            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);
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

            var relationsResponse = await SendPostAsync(httpClient, config, batchUrl, relationsContent, cancellationToken, handleErrors: false);
            
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

            // Extract parent ID and all relations from relations immediately (before document disposal)
            var relationsItem = relationsItems[0];
            var parentId = ExtractParentIdFromRelations(relationsItem);
            var relations = ExtractAllRelations(relationsItem);

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

            var fieldsResponse = await SendPostAsync(httpClient, config, batchUrl, fieldsContent, cancellationToken, handleErrors: false);
            
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

            // Extract created date from TFS (System.CreatedDate)
            DateTimeOffset? createdDate = ParseDateTimeField(fields, "System.CreatedDate");

            // Extract changed date from TFS (System.ChangedDate)
            DateTimeOffset? changedDate = ParseDateTimeField(fields, "System.ChangedDate");

            // Extract closed date from TFS (Microsoft.VSTS.Common.ClosedDate)
            DateTimeOffset? closedDate = ParseDateTimeField(fields, "Microsoft.VSTS.Common.ClosedDate");

            // Extract severity from TFS (Microsoft.VSTS.Common.Severity)
            string? severity = ParseSeverityField(fields);

            // Extract tags from TFS (System.Tags)
            string? tags = ParseTagsField(fields);

            // Extract blocked status from TFS (Microsoft.VSTS.CMMI.Blocked)
            bool? isBlocked = ParseBlockedField(fields);

            var workItem = new WorkItemDto(
                TfsId: id,
                Type: type,
                Title: title,
                ParentTfsId: parentId, // Use parent ID from Phase 1
                AreaPath: area,
                IterationPath: iteration,
                State: state,
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: effort,
                Description: description,
                CreatedDate: createdDate,
                ClosedDate: closedDate,
                Severity: severity,
                Tags: tags,
                ChangedDate: changedDate,
                IsBlocked: isBlocked,
                Relations: relations // Use relations from Phase 1
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
                ? $" AND [System.ChangedDate] >= '{FormatUtcTimestamp(since.Value)}'"
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

            var wiqlResponse = await SendPostAsync(httpClient, config, wiqlUrl, content, cancellationToken, handleErrors: false);
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

                var relationsResponse = await SendPostAsync(httpClient, config, batchUrl, relationsContent, cancellationToken, handleErrors: false);
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

                var fieldsResponse = await SendPostAsync(httpClient, config, batchUrl, fieldsContent, cancellationToken, handleErrors: false);
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

                    // Extract created date from TFS (System.CreatedDate)
                    DateTimeOffset? createdDate = ParseDateTimeField(fields, "System.CreatedDate");

                    // Extract closed date from TFS (Microsoft.VSTS.Common.ClosedDate)
                    DateTimeOffset? closedDate = ParseDateTimeField(fields, "Microsoft.VSTS.Common.ClosedDate");

                    // Extract severity from TFS (Microsoft.VSTS.Common.Severity)
                    string? severity = ParseSeverityField(fields);

                    // Extract tags from TFS (System.Tags)
                    string? tags = ParseTagsField(fields);

                    var changedDate = ParseDateTimeField(fields, "System.ChangedDate");

                    results.Add(new WorkItemDto(
                        TfsId: id,
                        Type: type,
                        Title: title,
                        ParentTfsId: parentId,
                        AreaPath: area,
                        IterationPath: iteration,
                        State: state,
                        RetrievedAt: DateTimeOffset.UtcNow,
                        Effort: effort,
                        Description: description,
                        CreatedDate: createdDate,
                        ClosedDate: closedDate,
                        Severity: severity,
                        Tags: tags,
                        ChangedDate: changedDate
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
    /// Parses the tags field from work item fields.
    /// Returns the semicolon-separated tags string if present, null otherwise.
    /// </summary>
    private static string? ParseTagsField(JsonElement fields)
    {
        if (fields.TryGetProperty("System.Tags", out var tagsField))
        {
            return tagsField.ValueKind == JsonValueKind.String ? tagsField.GetString() : null;
        }
        return null;
    }

    /// <summary>
    /// Parses the severity field from work item fields.
    /// Returns the severity string value if present, null otherwise.
    /// </summary>
    private static string? ParseSeverityField(JsonElement fields)
    {
        if (fields.TryGetProperty(TfsFieldSeverity, out var severityField))
        {
            return severityField.ValueKind == JsonValueKind.String ? severityField.GetString() : null;
        }
        return null;
    }

    /// <summary>
    /// Parses the blocked field from work item fields.
    /// Returns true if blocked = "Yes", false if "No", null if not present.
    /// </summary>
    private static bool? ParseBlockedField(JsonElement fields)
    {
        if (fields.TryGetProperty(TfsFieldBlocked, out var blockedField))
        {
            var value = blockedField.ValueKind == JsonValueKind.String ? blockedField.GetString() : null;
            if (!string.IsNullOrEmpty(value))
            {
                return value.Equals("Yes", StringComparison.OrdinalIgnoreCase);
            }
        }
        return null;
    }
}
