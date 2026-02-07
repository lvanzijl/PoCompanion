using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Integrations.Tfs.Models.Internal;
using PoTool.Shared.Settings;
using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Real Azure DevOps/TFS REST client implementation - Work Items Hierarchy operations.
/// Contains methods for retrieving work items by root IDs with hierarchy traversal and ancestor completion.
/// </summary>
public partial class RealTfsClient
{
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
      const int inactivityTimeoutSeconds = 300;

      _logger.LogInformation(
          "Starting hierarchy sync for {Count} root work items: [{Ids}], incremental={Incremental}",
          rootWorkItemIds.Length, string.Join(", ", rootWorkItemIds), since.HasValue);

      void UpdateHeartbeat() => lastActivity = DateTimeOffset.UtcNow;

      void CheckInactivity()
      {
         var elapsed = DateTimeOffset.UtcNow - lastActivity;
         if (elapsed.TotalSeconds > inactivityTimeoutSeconds)
         {
            _logger.LogError("Sync stalled: No progress for {Seconds} seconds", elapsed.TotalSeconds);
            throw new TimeoutException($"Sync cancelled due to inactivity (no progress for {elapsed.TotalSeconds:F0} seconds)");
         }
      }

      // =========================================
      // Step 1: Single recursive WorkItemLinks WIQL
      // =========================================
      progressCallback?.Invoke(1, 3, "Querying work item hierarchy (recursive links)...");
      UpdateHeartbeat();

      var allWorkItemIds = new HashSet<int>(rootWorkItemIds);
      var relationsMap = new Dictionary<int, int?>(); // child -> parent (null for roots)

      foreach (var rid in rootWorkItemIds)
         relationsMap[rid] = null;

      // One query for all roots (instead of BFS frontier batches)
      var rootIdList = string.Join(",", rootWorkItemIds.Distinct().OrderBy(x => x));

      // NOTE: Discovery phase NEVER filters by date/since.
      var wiql = new
      {
         query =
              "SELECT [System.Id] FROM WorkItemLinks WHERE " +
              $"([Source].[System.Id] IN ({rootIdList})) AND " +
              "([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward') " +
              "MODE (Recursive)"
      };

      try
      {
         var wiqlUrl = ProjectUrl(config, "_apis/wit/wiql");
         using var content = new StringContent(
             JsonSerializer.Serialize(wiql),
             Encoding.UTF8,
             "application/json");

         var wiqlResponse = await SendPostAsync(httpClient, config, wiqlUrl, content, cancellationToken, handleErrors: false);
         UpdateHeartbeat();
         await HandleHttpErrorsAsync(wiqlResponse, cancellationToken);

         using var stream = await wiqlResponse.Content.ReadAsStreamAsync(cancellationToken);
         using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

         if (doc.RootElement.TryGetProperty("workItemRelations", out var relations)
             && relations.ValueKind == JsonValueKind.Array)
         {
            var edgeCount = 0;
            var multiParentCount = 0;

            foreach (var relation in relations.EnumerateArray())
            {
               if (!relation.TryGetProperty("rel", out var relProp) ||
                   relProp.ValueKind != JsonValueKind.String ||
                   !string.Equals(
                       relProp.GetString(),
                       "System.LinkTypes.Hierarchy-Forward",
                       StringComparison.OrdinalIgnoreCase))
               {
                  continue;
               }

               if (!relation.TryGetProperty("source", out var sourceProp) ||
                   sourceProp.ValueKind != JsonValueKind.Object ||
                   !sourceProp.TryGetProperty("id", out var sourceIdProp))
               {
                  continue;
               }

               if (!relation.TryGetProperty("target", out var targetProp) ||
                   targetProp.ValueKind != JsonValueKind.Object ||
                   !targetProp.TryGetProperty("id", out var targetIdProp))
               {
                  continue;
               }

               var parentId = sourceIdProp.GetInt32();
               var childId = targetIdProp.GetInt32();
               if (parentId == childId) continue;

               edgeCount++;

               allWorkItemIds.Add(parentId);
               allWorkItemIds.Add(childId);

               // Derive parent directly from the edge list (child -> parent)
               if (relationsMap.TryGetValue(childId, out var existingParent))
               {
                  // If multiple parents exist (shouldn't in a strict tree), keep first and log.
                  if (existingParent.HasValue && existingParent.Value != parentId)
                  {
                     multiParentCount++;
                  }
               }
               else
               {
                  relationsMap[childId] = parentId;
               }
            }

            if (multiParentCount > 0)
            {
               _logger.LogWarning(
                   "Hierarchy link-set contains {Count} items with multiple parents (keeping first parent encountered).",
                   multiParentCount);
            }

            _logger.LogInformation(
                "Recursive link discovery complete: Roots={RootCount}, TotalWorkItems={TotalCount}, Edges={EdgeCount}",
                rootWorkItemIds.Length, allWorkItemIds.Count, edgeCount);
         }
         else
         {
            _logger.LogWarning("Recursive WIQL returned no 'workItemRelations' array. Returning roots only.");
         }
      }
      catch (OperationCanceledException) { throw; }
      catch (TimeoutException) { throw; }
      catch (Exception ex)
      {
         _logger.LogWarning(ex, "Recursive WIQL hierarchy query failed. Returning roots only.");
         // allWorkItemIds already contains roots
      }

      if (allWorkItemIds.Count == 0)
         return Enumerable.Empty<WorkItemDto>();

      // =========================================
      // Step 1.5: Optional ancestor completion (UP)
      // =========================================
      // Keeps your existing behavior: if some parent IDs exist but weren't included, walk up to fetch them.
      // This still uses workitemsbatch + expand=relations internally.
      try
      {
         CheckInactivity();
         var addedAncestors = await CompleteAncestorsAsync(
             config,
             httpClient,
             allWorkItemIds,
             relationsMap,
             progressCallback,
             UpdateHeartbeat,
             CheckInactivity,
             cancellationToken);

         if (addedAncestors > 0)
         {
            _logger.LogInformation("Ancestor completion added {Count} items. New total: {Total}",
                addedAncestors, allWorkItemIds.Count);
         }
      }
      catch (Exception ex)
      {
         _logger.LogWarning(ex, "Ancestor completion failed; continuing with partial hierarchy.");
      }

      // =========================================
      // Step 2: Fetch fields (batch) and build DTOs
      // =========================================
      progressCallback?.Invoke(2, 3, $"Fetching {allWorkItemIds.Count} work items (fields)...");
      UpdateHeartbeat();

      var ids = allWorkItemIds.ToArray();
      var totalBatches = (int)Math.Ceiling((double)ids.Length / WorkItemBatchSize);
      var results = new List<WorkItemDto>(ids.Length);

      _logger.LogInformation(
          "Phase (fields): Fetching {TotalIds} work items in {BatchCount} batches of {BatchSize}",
          ids.Length, totalBatches, WorkItemBatchSize);

      for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
      {
         CheckInactivity();
         var batchStartTime = DateTimeOffset.UtcNow;

         var batchIds = ids
             .Skip(batchIndex * WorkItemBatchSize)
             .Take(WorkItemBatchSize)
             .ToArray();

         var fieldsRequest = new WorkItemBatchRequest
         {
            Ids = batchIds,
            Fields = RequiredWorkItemFields
         };

         var batchUrl = CollectionUrl(config, "_apis/wit/workitemsbatch");
         using var fieldsContent = new StringContent(
             JsonSerializer.Serialize(fieldsRequest),
             Encoding.UTF8,
             "application/json");

         var fieldsResponse = await SendPostAsync(httpClient, config, batchUrl, fieldsContent, cancellationToken, handleErrors: false);
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
            DateTimeOffset? createdDate = ParseDateTimeField(fields, "System.CreatedDate");
            DateTimeOffset? changedDate = ParseDateTimeField(fields, "System.ChangedDate");
            DateTimeOffset? closedDate = ParseDateTimeField(fields, "Microsoft.VSTS.Common.ClosedDate");
            string? severity = ParseSeverityField(fields);
            string? tags = ParseTagsField(fields);

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
             "Fields batch {BatchIndex}/{TotalBatches} completed: {IdCount} IDs fetched, HTTP {StatusCode}, {ElapsedMs}ms",
             batchIndex + 1, totalBatches, batchIds.Length, (int)fieldsResponse.StatusCode, batchElapsed.TotalMilliseconds);
      }

      var elapsed = DateTimeOffset.UtcNow - startTime;
      _logger.LogInformation(
          "Retrieved {Count} work items for root IDs [{RootIds}] in {ElapsedMs}ms (recursive links).",
          results.Count, string.Join(", ", rootWorkItemIds), elapsed.TotalMilliseconds);

      return results;
   }


   /// <summary>
   /// Retrieves work items starting from specified root work item IDs with detailed structured progress reporting.
   /// This is a wrapper around GetWorkItemsByRootIdsAsync that provides enhanced progress callbacks.
   /// </summary>
   public async Task<IEnumerable<WorkItemDto>> GetWorkItemsByRootIdsWithDetailedProgressAsync(
        int[] rootWorkItemIds,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        // Detailed progress callback removed in Live-only mode
        return await GetWorkItemsByRootIdsAsync(
            rootWorkItemIds,
            since,
            null, // No progress callback
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
    /// Extracts all relations from a work item JSON element.
    /// Returns a list of WorkItemRelation objects.
    /// </summary>
    private static List<WorkItemRelation> ExtractAllRelations(JsonElement item)
    {
        var relationsList = new List<WorkItemRelation>();
        
        if (!item.TryGetProperty("relations", out var relations) || relations.ValueKind != JsonValueKind.Array)
        {
            return relationsList;
        }

        foreach (var relation in relations.EnumerateArray())
        {
            string? linkType = null;
            int? targetId = null;
            string? url = null;

            if (relation.TryGetProperty("rel", out var rel))
            {
                linkType = rel.GetString();
            }

            if (relation.TryGetProperty("url", out var urlProp))
            {
                url = urlProp.GetString();
                if (!string.IsNullOrEmpty(url))
                {
                    // Extract work item ID from URL
                    var queryIndex = url.IndexOf('?');
                    var urlWithoutQuery = queryIndex >= 0 ? url.Substring(0, queryIndex).TrimEnd('/') : url.TrimEnd('/');
                    var segments = urlWithoutQuery.Split('/');
                    if (segments.Length > 0 && int.TryParse(segments[^1], out var parsedId))
                    {
                        targetId = parsedId;
                    }
                }
            }

            if (!string.IsNullOrEmpty(linkType))
            {
                relationsList.Add(new WorkItemRelation(linkType, targetId, url));
            }
        }

        return relationsList;
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

    /// <summary>
    /// Parses a date/time field from work item fields.
    /// Handles ISO 8601 date strings from TFS.
    /// </summary>
    private static DateTimeOffset? ParseDateTimeField(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var dateField))
            return null;

        switch (dateField.ValueKind)
        {
            case JsonValueKind.String:
                var strValue = dateField.GetString();
                if (!string.IsNullOrEmpty(strValue) && DateTimeOffset.TryParse(strValue, out var parsedDate))
                    return parsedDate;
                break;
        }

        return null;
    }

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
                var relationsResponse = await SendPostAsync(httpClient, config, batchUrl, relationsContent, cancellationToken, handleErrors: false);
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
}
