using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Integrations.Tfs.Models.Internal;
using PoTool.Shared.Contracts.TfsVerification;
using PoTool.Shared.Exceptions;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Real Azure DevOps/TFS REST client implementation - Verification Methods
/// </summary>
internal partial class RealTfsClient
{
    private const int VerificationSampleWorkItemCount = 5;
    private const string VerificationWorkItemTypeField = "System.WorkItemType";

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
            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);

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
            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);

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

            var response = await SendPostAsync(httpClient, config, url, content, cancellationToken, handleErrors: false);

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

            var wiqlResponse = await SendPostAsync(httpClient, config, wiqlUrl, wiqlContent, cancellationToken, handleErrors: false);

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

            var relationsResponse = await SendPostAsync(httpClient, config, batchUrl, relationsContent, cancellationToken, handleErrors: false);

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
            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);

            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                var fields = doc.RootElement.GetProperty("value").EnumerateArray()
                    .Select(f => f.GetProperty("referenceName").GetString())
                    .OfType<string>()
                    .ToHashSet(StringComparer.Ordinal);

                var missingFields = RequiredWorkItemFields
                    .Where(requiredField => !fields.Contains(requiredField))
                    .ToList();

                if (missingFields.Any())
                {
                    return CreateFailureResult(
                        "work-item-fields",
                        "Work item display and processing",
                        "Required runtime and analytics work item fields are accessible",
                        $"Missing fields: {string.Join(", ", missingFields)}",
                        FailureCategory.MissingField,
                        $"Found {fields.Count} fields but missing required runtime fields");
                }

                var sampleValidation = await ValidateWorkItemFieldPayloadAsync(httpClient, config, cancellationToken);
                if (!sampleValidation.Success)
                {
                    return sampleValidation;
                }

                return new TfsCapabilityCheckResult
                {
                    CapabilityId = "work-item-fields",
                    Success = true,
                    ImpactedFunctionality = "Work item display and processing",
                    ExpectedBehavior = "Required runtime and analytics work item fields are accessible",
                    ObservedBehavior = $"All required runtime fields present ({fields.Count} total fields). {sampleValidation.ObservedBehavior}"
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

    private async Task<TfsCapabilityCheckResult> ValidateWorkItemFieldPayloadAsync(
        HttpClient httpClient,
        TfsConfigEntity config,
        CancellationToken cancellationToken)
    {
        var wiql = new
        {
            query = $"Select Top {VerificationSampleWorkItemCount} [System.Id] From WorkItems Order By [System.Id] Desc"
        };

        var wiqlUrl = ProjectUrl(config, "_apis/wit/wiql");
        using var wiqlContent = new StringContent(JsonSerializer.Serialize(wiql), System.Text.Encoding.UTF8, "application/json");
        var wiqlResponse = await SendPostAsync(httpClient, config, wiqlUrl, wiqlContent, cancellationToken, handleErrors: false);
        if (!wiqlResponse.IsSuccessStatusCode)
        {
            return CreateFailureResult(
                "work-item-fields",
                "Work item display and processing",
                "Required runtime and analytics work item fields are accessible",
                $"Sample WIQL query failed: HTTP {(int)wiqlResponse.StatusCode}",
                CategorizeHttpError(wiqlResponse.StatusCode),
                await wiqlResponse.Content.ReadAsStringAsync(cancellationToken));
        }

        using var wiqlStream = await wiqlResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var wiqlDoc = await JsonDocument.ParseAsync(wiqlStream, cancellationToken: cancellationToken);
        var sampleIds = wiqlDoc.RootElement.TryGetProperty("workItems", out var workItems)
            ? workItems.EnumerateArray()
                .Where(item => item.TryGetProperty("id", out _))
                .Select(item => item.GetProperty("id").GetInt32())
                .Where(id => id > 0)
                .Take(VerificationSampleWorkItemCount)
                .ToArray()
            : Array.Empty<int>();

        if (sampleIds.Length == 0)
        {
            return new TfsCapabilityCheckResult
            {
                CapabilityId = "work-item-fields",
                Success = true,
                ImpactedFunctionality = "Work item display and processing",
                ExpectedBehavior = "Required runtime and analytics work item fields are accessible",
                ObservedBehavior = "Schema validated. Sample payload validation skipped because no work items were returned by WIQL."
            };
        }

        var batchRequest = new WorkItemBatchRequest
        {
            Ids = sampleIds,
            Fields = [TfsFieldProjectNumber, TfsFieldProjectElement, TfsFieldTimeCriticality, VerificationWorkItemTypeField]
        };

        var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");
        using var batchContent = new StringContent(JsonSerializer.Serialize(batchRequest), System.Text.Encoding.UTF8, "application/json");
        var batchResponse = await SendPostAsync(httpClient, config, batchUrl, batchContent, cancellationToken, handleErrors: false);
        if (!batchResponse.IsSuccessStatusCode)
        {
            return CreateFailureResult(
                "work-item-fields",
                "Work item display and processing",
                "Required runtime and analytics work item fields are accessible",
                $"Sample batch query failed: HTTP {(int)batchResponse.StatusCode}",
                CategorizeHttpError(batchResponse.StatusCode),
                await batchResponse.Content.ReadAsStringAsync(cancellationToken));
        }

        using var batchStream = await batchResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var batchDoc = await JsonDocument.ParseAsync(batchStream, cancellationToken: cancellationToken);
        var items = batchDoc.RootElement.TryGetProperty("value", out var value)
            ? value.EnumerateArray().Select(item => item.Clone()).ToList()
            : [];

        var missingPayloadFields = new HashSet<string>(StringComparer.Ordinal);
        var typeMismatches = new List<string>();
        var nullOnlyFields = new HashSet<string>(StringComparer.Ordinal)
        {
            TfsFieldProjectNumber,
            TfsFieldProjectElement,
            TfsFieldTimeCriticality
        };

        foreach (var item in items)
        {
            if (!item.TryGetProperty("fields", out var payloadFields) || payloadFields.ValueKind != JsonValueKind.Object)
            {
                missingPayloadFields.UnionWith(nullOnlyFields);
                continue;
            }

            ValidatePayloadField(payloadFields, TfsFieldProjectNumber, JsonValueKind.String, missingPayloadFields, nullOnlyFields, typeMismatches);
            ValidatePayloadField(payloadFields, TfsFieldProjectElement, JsonValueKind.String, missingPayloadFields, nullOnlyFields, typeMismatches);
            ValidatePayloadField(payloadFields, TfsFieldTimeCriticality, JsonValueKind.Number, missingPayloadFields, nullOnlyFields, typeMismatches);
        }

        if (missingPayloadFields.Count > 0 || typeMismatches.Count > 0)
        {
            var problems = new List<string>();
            if (missingPayloadFields.Count > 0)
            {
                problems.Add($"Missing in payload: {string.Join(", ", missingPayloadFields)}");
            }

            if (typeMismatches.Count > 0)
            {
                problems.Add($"Type mismatch: {string.Join("; ", typeMismatches)}");
            }

            _logger.LogWarning("TFS payload validation failed for analytics fields. {Problems}", string.Join(". ", problems));

            return CreateFailureResult(
                "work-item-fields",
                "Work item display and processing",
                "Required runtime and analytics work item fields are accessible",
                string.Join(". ", problems),
                FailureCategory.MissingField,
                $"Validated {items.Count} sampled work items");
        }

        if (nullOnlyFields.Count > 0)
        {
            _logger.LogWarning(
                "TFS payload validation sampled {WorkItemCount} work items. Fields present but null/empty in all samples: {Fields}",
                items.Count,
                string.Join(", ", nullOnlyFields));
        }

        return new TfsCapabilityCheckResult
        {
            CapabilityId = "work-item-fields",
            Success = true,
            ImpactedFunctionality = "Work item display and processing",
            ExpectedBehavior = "Required runtime and analytics work item fields are accessible",
            ObservedBehavior = nullOnlyFields.Count > 0
                ? $"Sample payload validated on {items.Count} work items. Null-only sampled fields: {string.Join(", ", nullOnlyFields)}"
                : $"Sample payload validated on {items.Count} work items with expected field types."
        };
    }

    private static void ValidatePayloadField(
        JsonElement payloadFields,
        string fieldRefName,
        JsonValueKind expectedKind,
        ISet<string> missingPayloadFields,
        ISet<string> nullOnlyFields,
        ICollection<string> typeMismatches)
    {
        if (!payloadFields.TryGetProperty(fieldRefName, out var fieldValue))
        {
            missingPayloadFields.Add(fieldRefName);
            nullOnlyFields.Remove(fieldRefName);
            return;
        }

        if (fieldValue.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        if (expectedKind == JsonValueKind.String)
        {
            if (fieldValue.ValueKind == JsonValueKind.String)
            {
                if (!string.IsNullOrWhiteSpace(fieldValue.GetString()))
                {
                    nullOnlyFields.Remove(fieldRefName);
                }

                return;
            }

            typeMismatches.Add($"{fieldRefName} expected string but was {fieldValue.ValueKind}");
            nullOnlyFields.Remove(fieldRefName);
            return;
        }

        if (expectedKind == JsonValueKind.Number)
        {
            if (fieldValue.ValueKind == JsonValueKind.Number)
            {
                nullOnlyFields.Remove(fieldRefName);
                return;
            }

            typeMismatches.Add($"{fieldRefName} expected numeric but was {fieldValue.ValueKind}");
            nullOnlyFields.Remove(fieldRefName);
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

            var response = await SendPostAsync(httpClient, config, url, content, cancellationToken, handleErrors: false);

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
            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);

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
            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);

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
            var buildDefsResponse = await SendGetAsync(httpClient, config, buildDefsUrl, cancellationToken, handleErrors: false);

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
                var buildRunsResponse = await SendGetAsync(httpClient, config, buildRunsUrl, cancellationToken, handleErrors: false);

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
                var releaseDefsResponse = await SendGetAsync(httpClient, config, releaseDefsUrl, cancellationToken, handleErrors: false);

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
            var getResponse = await SendGetAsync(httpClient, config, getUrl, cancellationToken, handleErrors: false);

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
}
