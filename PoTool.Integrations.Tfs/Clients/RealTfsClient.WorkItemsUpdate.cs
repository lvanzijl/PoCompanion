using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Shared.Contracts.TfsVerification;
using PoTool.Shared.Exceptions;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Integrations.Tfs.Clients;

public partial class RealTfsClient
{
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

    public async Task<bool> UpdateWorkItemSeverityAsync(int workItemId, string severity, CancellationToken cancellationToken = default)
    {
        var result = await UpdateWorkItemSeverityAndReturnAsync(workItemId, severity, cancellationToken);
        return result != null;
    }

    public async Task<WorkItemDto?> UpdateWorkItemSeverityAndReturnAsync(int workItemId, string severity, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} severity to '{Severity}'", workItemId, severity);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item severity");
                return null;
            }

            // Use auth-mode-specific HttpClient (requirement #2)
            var httpClient = GetAuthenticatedHttpClient();

            // Create per-request timeout token for write operation
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(entity.TimeoutSeconds));

            // Build JSON Patch document for severity (Microsoft.VSTS.Common.Severity)
            var patchDocument = new[]
            {
                new
                {
                    op = "add",
                    path = $"/fields/{TfsFieldSeverity}",
                    value = severity
                }
            };

            // Work item PATCH is collection-scoped (work item IDs are unique across collection)
            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");
            using var content = new StringContent(
                JsonSerializer.Serialize(patchDocument),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogInformation("Sending PATCH request to update work item {WorkItemId}: Field={FieldReferenceName}, Value={Value}",
                workItemId, TfsFieldSeverity, severity);

            // PATCH operations are NOT retried - they are non-idempotent
            var response = await httpClient.PatchAsync(updateUrl, content, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} severity to '{Severity}'", workItemId, severity);
                
                // Parse the response to get the updated work item
                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var responseDoc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
                
                return ParseWorkItemFromPatchResponse(responseDoc.RootElement);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId} severity. Status: {StatusCode}, Response: {Response}",
                    workItemId, response.StatusCode, responseBody);
                return null;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Update work item {WorkItemId} severity timed out", workItemId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} severity to '{Severity}'", workItemId, severity);
            return null;
        }
    }

    public async Task<bool> UpdateWorkItemTagsAsync(int workItemId, List<string> tags, CancellationToken cancellationToken = default)
    {
        var result = await UpdateWorkItemTagsAndReturnAsync(workItemId, tags, cancellationToken);
        return result != null;
    }

    public async Task<WorkItemDto?> UpdateWorkItemTagsAndReturnAsync(int workItemId, List<string> tags, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} tags to: {Tags}", workItemId, string.Join("; ", tags));

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item tags");
                return null;
            }

            // Use auth-mode-specific HttpClient (requirement #2)
            var httpClient = GetAuthenticatedHttpClient();

            // Create per-request timeout token for write operation
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(entity.TimeoutSeconds));

            // Normalize tags before sending (requirement #1):
            // - Trim whitespace
            // - Remove empty entries
            // - De-duplicate (case-insensitive)
            // - Join with "; " exactly
            var normalizedTags = tags
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Build JSON Patch document for tags (System.Tags)
            // Tags are stored as semicolon-separated string in TFS
            // Use 'replace' operation (requirement #2) instead of 'add'
            // When removing all tags, send empty string (requirement #3) instead of null
            var tagsString = normalizedTags.Count > 0 ? string.Join("; ", normalizedTags) : "";
            var patchDocument = new[]
            {
                new
                {
                    op = "replace",
                    path = "/fields/System.Tags",
                    value = tagsString
                }
            };

            // Work item PATCH is collection-scoped (work item IDs are unique across collection)
            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");
            using var content = new StringContent(
                JsonSerializer.Serialize(patchDocument),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogInformation("Sending PATCH request to update work item {WorkItemId}: Field=System.Tags, Value={Value}",
                workItemId, tagsString);

            // PATCH operations are NOT retried - they are non-idempotent
            var response = await httpClient.PatchAsync(updateUrl, content, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} tags", workItemId);
                
                // Parse the response to get the updated work item
                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var responseDoc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
                
                return ParseWorkItemFromPatchResponse(responseDoc.RootElement);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId} tags. Status: {StatusCode}, Response: {Response}",
                    workItemId, response.StatusCode, responseBody);
                return null;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Update work item {WorkItemId} tags timed out", workItemId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} tags", workItemId);
            return null;
        }
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
            var getResponse = await SendGetAsync(httpClient, entity, getUrl, timeoutCts.Token, handleErrors: false);

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
    
    /// <summary>
    /// Parses a WorkItemDto from a PATCH response JSON element.
    /// PATCH responses have the same structure as GET responses: { id, fields: { ... }, relations: [...] }
    /// However, PATCH responses may not include relations, so we extract parent ID as null.
    /// </summary>
    /// <returns>A WorkItemDto constructed from the PATCH response data.</returns>
    private WorkItemDto ParseWorkItemFromPatchResponse(JsonElement workItemElement)
    {
        var id = workItemElement.GetProperty("id").GetInt32();
        var fields = workItemElement.GetProperty("fields");
        
        var type = fields.TryGetProperty("System.WorkItemType", out var t) ? t.GetString() ?? "" : "";
        var title = fields.TryGetProperty("System.Title", out var ti) ? ti.GetString() ?? "" : "";
        var state = fields.TryGetProperty("System.State", out var s) ? s.GetString() ?? "" : "";
        var area = fields.TryGetProperty("System.AreaPath", out var a) ? a.GetString() ?? "" : "";
        var iteration = fields.TryGetProperty("System.IterationPath", out var ip) ? ip.GetString() ?? "" : "";
        var description = fields.TryGetProperty("System.Description", out var d) ? d.GetString() : null;
        
        // Extract parent ID from relations if present (may not be in PATCH response)
        // Note: ExtractParentIdFromRelations handles missing relations gracefully
        int? parentId = ExtractParentIdFromRelations(workItemElement);
        
        // Extract effort, story points, dates, severity, and tags
        int? effort = ParseEffortField(fields);
        int? storyPoints = ParseStoryPointsField(fields);
        int? businessValue = ParseBusinessValueField(fields);
        DateTimeOffset? createdDate = ParseDateTimeField(fields, "System.CreatedDate");
        DateTimeOffset? changedDate = ParseDateTimeField(fields, "System.ChangedDate");
        DateTimeOffset? closedDate = ParseDateTimeField(fields, "Microsoft.VSTS.Common.ClosedDate");
        string? severity = ParseSeverityField(fields);
        string? tags = ParseTagsField(fields);
        double? backlogPriority = ParseBacklogPriorityField(fields);
        double? timeCriticality = ParseTimeCriticalityField(fields);
        string? projectNumber = ParseStringField(fields, TfsFieldProjectNumber);
        string? projectElement = ParseStringField(fields, TfsFieldProjectElement);
        
        return new WorkItemDto(
            TfsId: id,
            Type: type,
            Title: title,
            ParentTfsId: parentId,
            AreaPath: area,
            IterationPath: iteration,
            State: state,
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: effort,
            BusinessValue: businessValue,
            Description: description,
            CreatedDate: createdDate,
            ClosedDate: closedDate,
            Severity: severity,
            Tags: tags,
            ChangedDate: changedDate,
            BacklogPriority: backlogPriority,
            StoryPoints: storyPoints,
            TimeCriticality: timeCriticality,
            ProjectNumber: projectNumber,
            ProjectElement: projectElement
        );
    }

    public async Task<bool> UpdateWorkItemBacklogPriorityAsync(int workItemId, double priority, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} backlog priority to {Priority}", workItemId, priority);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item backlog priority");
                return false;
            }

            var httpClient = GetAuthenticatedHttpClient();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(entity.TimeoutSeconds));

            var patchDocument = new[]
            {
                new
                {
                    op = "add",
                    path = $"/fields/{TfsFieldBacklogPriority}",
                    value = priority
                }
            };

            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");
            using var content = new StringContent(
                JsonSerializer.Serialize(patchDocument),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogDebug("Sending PATCH request to update work item {WorkItemId} backlog priority", workItemId);

            var response = await httpClient.PatchAsync(updateUrl, content, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} backlog priority to {Priority}", workItemId, priority);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId} backlog priority. Status: {StatusCode}, Response: {Response}",
                    workItemId, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Update work item {WorkItemId} backlog priority timed out", workItemId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} backlog priority to {Priority}", workItemId, priority);
            return false;
        }
    }

    public async Task<bool> UpdateWorkItemIterationPathAsync(int workItemId, string iterationPath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} iteration path to {IterationPath}", workItemId, iterationPath);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item iteration path");
                return false;
            }

            var httpClient = GetAuthenticatedHttpClient();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(entity.TimeoutSeconds));

            var patchDocument = new[]
            {
                new
                {
                    op = "add",
                    path = "/fields/System.IterationPath",
                    value = (object)iterationPath
                }
            };

            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");
            using var content = new StringContent(
                JsonSerializer.Serialize(patchDocument),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogDebug("Sending PATCH request to update work item {WorkItemId} iteration path", workItemId);

            var response = await httpClient.PatchAsync(updateUrl, content, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} iteration path to {IterationPath}", workItemId, iterationPath);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId} iteration path. Status: {StatusCode}, Response: {Response}",
                    workItemId, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Update work item {WorkItemId} iteration path timed out", workItemId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} iteration path to {IterationPath}", workItemId, iterationPath);
            return false;
        }
    }

    public async Task<WorkItemDto?> UpdateWorkItemTitleDescriptionAsync(int workItemId, string? title, string? description, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating work item {WorkItemId} title/description", workItemId);

            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                _logger.LogWarning("No TFS configuration found for updating work item title/description");
                return null;
            }

            var httpClient = GetAuthenticatedHttpClient();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(entity.TimeoutSeconds));

            var patchOperations = new List<object>();
            if (title != null)
            {
                patchOperations.Add(new { op = "replace", path = "/fields/System.Title", value = title });
            }
            if (description != null)
            {
                patchOperations.Add(new { op = "replace", path = "/fields/System.Description", value = description });
            }

            if (patchOperations.Count == 0)
            {
                _logger.LogWarning("No fields to update for work item {WorkItemId}", workItemId);
                return null;
            }

            var updateUrl = CollectionUrl(entity, $"_apis/wit/workitems/{workItemId}");
            using var content = new StringContent(
                JsonSerializer.Serialize(patchOperations),
                System.Text.Encoding.UTF8,
                "application/json-patch+json");

            _logger.LogInformation("Sending PATCH request to update work item {WorkItemId} title/description", workItemId);

            var response = await httpClient.PatchAsync(updateUrl, content, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully updated work item {WorkItemId} title/description", workItemId);

                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var responseDoc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

                return ParseWorkItemFromPatchResponse(responseDoc.RootElement);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to update work item {WorkItemId} title/description. Status: {StatusCode}, Response: {Response}",
                    workItemId, response.StatusCode, responseBody);
                return null;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Update work item {WorkItemId} title/description timed out", workItemId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId} title/description", workItemId);
            return null;
        }
    }
}
