using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Core.Contracts;
using PoTool.Shared.Exceptions;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Real Azure DevOps/TFS REST client implementation for work item revisions.
/// Uses the reporting work item revisions API for efficient bulk retrieval.
/// This client is separate from RealTfsClient to maintain strict separation of concerns.
/// </summary>
public class RealRevisionTfsClient : IRevisionTfsClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITfsConfigurationService _configService;
    private readonly ILogger<RealRevisionTfsClient> _logger;
    private readonly TfsRequestThrottler _throttler;

    private const int MaxRetries = 3;

    /// <summary>
    /// Field whitelist for revision API.
    /// Only these fields will be requested and stored.
    /// </summary>
    private static readonly string[] FieldWhitelist = new[]
    {
        "System.Id",
        "System.WorkItemType",
        "System.Title",
        "System.State",
        "System.Reason",
        "System.IterationPath",
        "System.AreaPath",
        "System.CreatedDate",
        "System.ChangedDate",
        "Microsoft.VSTS.Common.ClosedDate",
        "Microsoft.VSTS.Scheduling.Effort",
        "System.Tags",
        "Microsoft.VSTS.Common.Severity"
    };

    public RealRevisionTfsClient(
        IHttpClientFactory httpClientFactory,
        ITfsConfigurationService configService,
        ILogger<RealRevisionTfsClient> logger,
        TfsRequestThrottler throttler)
    {
        _httpClientFactory = httpClientFactory;
        _configService = configService;
        _logger = logger;
        _throttler = throttler;
    }

    /// <inheritdoc />
    public async Task<ReportingRevisionsResult> GetReportingRevisionsAsync(
        DateTimeOffset? startDateTime = null,
        string? continuationToken = null,
        ReportingExpandMode expandMode = ReportingExpandMode.None,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // Build URL for reporting work item revisions API
            // /_apis/wit/reporting/workitemrevisions
            var url = BuildReportingRevisionsUrl(config, startDateTime, continuationToken, expandMode);

            _logger.LogDebug("Calling reporting revisions API: {Url}", url);

            var response = await httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var revisions = new List<WorkItemRevision>();
            string? nextContinuationToken = null;

            // Parse continuation token from response headers or body
            if (doc.RootElement.TryGetProperty("continuationToken", out var tokenElement))
            {
                nextContinuationToken = tokenElement.GetString();
            }

            // Check if there's a continuation URL in headers (alternative paging mechanism)
            if (response.Headers.TryGetValues("x-ms-continuationtoken", out var headerTokens))
            {
                nextContinuationToken = headerTokens.FirstOrDefault() ?? nextContinuationToken;
            }

            // Parse revisions from response
            if (doc.RootElement.TryGetProperty("values", out var valuesArray))
            {
                foreach (var revision in valuesArray.EnumerateArray())
                {
                    var workItemRevision = ParseWorkItemRevision(revision);
                    if (workItemRevision != null)
                    {
                        revisions.Add(workItemRevision);
                    }
                }
            }

            _logger.LogInformation(
                "Retrieved {Count} revisions from reporting API. HasMoreResults: {HasMore}",
                revisions.Count,
                !string.IsNullOrEmpty(nextContinuationToken));

            return new ReportingRevisionsResult
            {
                Revisions = revisions,
                ContinuationToken = nextContinuationToken
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkItemRevision>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;

        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            // Per-item revisions endpoint: /_apis/wit/workItems/{id}/revisions
            var url = $"{config.Url.TrimEnd('/')}/_apis/wit/workItems/{workItemId}/revisions?api-version={config.ApiVersion}";

            _logger.LogDebug("Calling per-item revisions API for work item {WorkItemId}", workItemId);

            var response = await httpClient.GetAsync(url, cancellationToken);
            await HandleHttpErrorsAsync(response, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var revisions = new List<WorkItemRevision>();

            if (!doc.RootElement.TryGetProperty("value", out var revisionsArray))
            {
                return revisions;
            }

            // Store previous revision fields for delta calculation
            Dictionary<string, string?>? previousFields = null;
            List<RelationInfo>? previousRelations = null;

            foreach (var revision in revisionsArray.EnumerateArray())
            {
                var workItemRevision = ParseWorkItemRevisionFromPerItem(revision, workItemId, previousFields, previousRelations);
                if (workItemRevision != null)
                {
                    revisions.Add(workItemRevision.Value.Revision);
                    previousFields = workItemRevision.Value.CurrentFields;
                    previousRelations = workItemRevision.Value.CurrentRelations;
                }
            }

            _logger.LogInformation(
                "Retrieved {Count} revisions for work item {WorkItemId}",
                revisions.Count, workItemId);

            return revisions;
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _configService.GetConfigEntityAsync(cancellationToken);
            if (entity == null)
            {
                return false;
            }

            var httpClient = GetAuthenticatedHttpClient();
            var url = $"{entity.Url.TrimEnd('/')}/_apis/projects?api-version={entity.ApiVersion}&$top=1";

            var response = await httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection validation failed");
            return false;
        }
    }

    private string BuildReportingRevisionsUrl(
        TfsConfigEntity config,
        DateTimeOffset? startDateTime,
        string? continuationToken,
        ReportingExpandMode expandMode)
    {
        // Build URL: {collection}/_apis/wit/reporting/workitemrevisions
        var baseUrl = $"{config.Url.TrimEnd('/')}/_apis/wit/reporting/workitemrevisions";

        var queryParams = new List<string>
        {
            $"api-version={config.ApiVersion}"
        };

        // Add field whitelist
        queryParams.Add($"fields={string.Join(",", FieldWhitelist)}");

        // Parameter conflict validation: prefer continuation token over startDateTime
        // When a continuation token is present, the API ignores startDateTime
        if (!string.IsNullOrEmpty(continuationToken))
        {
            // Add continuation token for paging
            queryParams.Add($"continuationToken={Uri.EscapeDataString(continuationToken)}");
            // Do NOT add startDateTime when continuation token is present
        }
        else if (startDateTime.HasValue)
        {
            // Add startDateTime for incremental sync (only when no continuation token)
            // Uses ISO 8601 round-trip format ("O") which Azure DevOps/TFS reporting API accepts
            // Format example: "2024-01-15T10:30:00.0000000+00:00"
            queryParams.Add($"startDateTime={startDateTime.Value:O}");
        }

        // Add expand parameter if requested
        // IMPORTANT: The reporting endpoint does NOT support $expand=relations
        // Only $expand=fields is allowed (for long text fields)
        if (expandMode == ReportingExpandMode.Fields)
        {
            queryParams.Add("$expand=fields");
        }

        return $"{baseUrl}?{string.Join("&", queryParams)}";
    }

    private WorkItemRevision? ParseWorkItemRevision(JsonElement revision)
    {
        try
        {
            if (!revision.TryGetProperty("id", out var idElement))
            {
                return null;
            }

            var workItemId = idElement.GetInt32();
            var revisionNumber = revision.TryGetProperty("rev", out var revEl) ? revEl.GetInt32() : 0;

            // Parse fields
            if (!revision.TryGetProperty("fields", out var fields))
            {
                return null;
            }

            var workItemType = GetStringField(fields, "System.WorkItemType") ?? "Unknown";
            var title = GetStringField(fields, "System.Title") ?? "";
            var state = GetStringField(fields, "System.State") ?? "Unknown";
            var reason = GetStringField(fields, "System.Reason");
            var iterationPath = GetStringField(fields, "System.IterationPath") ?? "";
            var areaPath = GetStringField(fields, "System.AreaPath") ?? "";
            var tags = GetStringField(fields, "System.Tags");
            var severity = GetStringField(fields, "Microsoft.VSTS.Common.Severity");

            var createdDate = GetDateTimeField(fields, "System.CreatedDate");
            var changedDate = GetDateTimeField(fields, "System.ChangedDate");
            var closedDate = GetDateTimeField(fields, "Microsoft.VSTS.Common.ClosedDate");
            var effort = GetIntField(fields, "Microsoft.VSTS.Scheduling.Effort");

            // If ChangedDate is missing, log warning and skip this revision - timestamp is critical for ordering
            if (!changedDate.HasValue)
            {
                _logger.LogWarning(
                    "Skipping work item {WorkItemId} revision {RevisionNumber}: Missing System.ChangedDate field",
                    workItemId, revisionNumber);
                return null;
            }

            var changedBy = GetStringField(fields, "System.ChangedBy");

            // Parse relation deltas if present
            var relationDeltas = ParseRelationDeltas(revision);

            return new WorkItemRevision
            {
                WorkItemId = workItemId,
                RevisionNumber = revisionNumber,
                WorkItemType = workItemType,
                Title = title,
                State = state,
                Reason = reason,
                IterationPath = iterationPath,
                AreaPath = areaPath,
                CreatedDate = createdDate,
                ChangedDate = changedDate.Value,
                ClosedDate = closedDate,
                Effort = effort,
                Tags = tags,
                Severity = severity,
                ChangedBy = changedBy,
                RelationDeltas = relationDeltas
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse work item revision");
            return null;
        }
    }

    private (WorkItemRevision Revision, Dictionary<string, string?> CurrentFields, List<RelationInfo> CurrentRelations)? ParseWorkItemRevisionFromPerItem(
        JsonElement revision,
        int workItemId,
        Dictionary<string, string?>? previousFields,
        List<RelationInfo>? previousRelations)
    {
        try
        {
            var revisionNumber = revision.TryGetProperty("rev", out var revEl) ? revEl.GetInt32() : 0;

            if (!revision.TryGetProperty("fields", out var fields))
            {
                return null;
            }

            // Build current field values
            var currentFields = new Dictionary<string, string?>();
            foreach (var fieldName in FieldWhitelist)
            {
                currentFields[fieldName] = GetStringField(fields, fieldName);
            }

            // Calculate field deltas
            var fieldDeltas = new Dictionary<string, FieldDelta>();
            if (previousFields != null)
            {
                foreach (var (fieldName, newValue) in currentFields)
                {
                    var oldValue = previousFields.GetValueOrDefault(fieldName);
                    if (oldValue != newValue)
                    {
                        fieldDeltas[fieldName] = new FieldDelta
                        {
                            FieldName = fieldName,
                            OldValue = oldValue,
                            NewValue = newValue
                        };
                    }
                }
            }

            // Parse current relations
            var currentRelations = ParseRelations(revision);

            // Calculate relation deltas
            var relationDeltas = new List<RelationDelta>();
            if (previousRelations != null)
            {
                // Find added relations
                foreach (var rel in currentRelations)
                {
                    if (!previousRelations.Any(p => p.RelationType == rel.RelationType && p.TargetId == rel.TargetId))
                    {
                        relationDeltas.Add(new RelationDelta
                        {
                            ChangeType = Core.Contracts.RelationChangeType.Added,
                            RelationType = rel.RelationType,
                            TargetWorkItemId = rel.TargetId
                        });
                    }
                }

                // Find removed relations
                foreach (var rel in previousRelations)
                {
                    if (!currentRelations.Any(c => c.RelationType == rel.RelationType && c.TargetId == rel.TargetId))
                    {
                        relationDeltas.Add(new RelationDelta
                        {
                            ChangeType = Core.Contracts.RelationChangeType.Removed,
                            RelationType = rel.RelationType,
                            TargetWorkItemId = rel.TargetId
                        });
                    }
                }
            }
            else if (currentRelations.Count > 0)
            {
                // First revision - all relations are "added"
                foreach (var rel in currentRelations)
                {
                    relationDeltas.Add(new RelationDelta
                    {
                        ChangeType = Core.Contracts.RelationChangeType.Added,
                        RelationType = rel.RelationType,
                        TargetWorkItemId = rel.TargetId
                    });
                }
            }

            var workItemType = currentFields.GetValueOrDefault("System.WorkItemType") ?? "Unknown";
            var title = currentFields.GetValueOrDefault("System.Title") ?? "";
            var state = currentFields.GetValueOrDefault("System.State") ?? "Unknown";
            var iterationPath = currentFields.GetValueOrDefault("System.IterationPath") ?? "";
            var areaPath = currentFields.GetValueOrDefault("System.AreaPath") ?? "";

            var changedDate = GetDateTimeField(fields, "System.ChangedDate") ?? DateTimeOffset.UtcNow;
            var createdDate = GetDateTimeField(fields, "System.CreatedDate");
            var closedDate = GetDateTimeField(fields, "Microsoft.VSTS.Common.ClosedDate");
            var effort = GetIntField(fields, "Microsoft.VSTS.Scheduling.Effort");

            var changedBy = GetStringField(fields, "System.ChangedBy");

            var workItemRevision = new WorkItemRevision
            {
                WorkItemId = workItemId,
                RevisionNumber = revisionNumber,
                WorkItemType = workItemType,
                Title = title,
                State = state,
                Reason = currentFields.GetValueOrDefault("System.Reason"),
                IterationPath = iterationPath,
                AreaPath = areaPath,
                CreatedDate = createdDate,
                ChangedDate = changedDate,
                ClosedDate = closedDate,
                Effort = effort,
                Tags = currentFields.GetValueOrDefault("System.Tags"),
                Severity = currentFields.GetValueOrDefault("Microsoft.VSTS.Common.Severity"),
                ChangedBy = changedBy,
                FieldDeltas = fieldDeltas.Count > 0 ? fieldDeltas : null,
                RelationDeltas = relationDeltas.Count > 0 ? relationDeltas : null
            };

            return (workItemRevision, currentFields, currentRelations);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse work item revision for work item {WorkItemId}", workItemId);
            return null;
        }
    }

    private List<RelationDelta> ParseRelationDeltas(JsonElement revision)
    {
        var deltas = new List<RelationDelta>();

        // The reporting API may include relation changes directly
        // Check for "relations" property with added/removed indicators
        if (!revision.TryGetProperty("relations", out var relations))
        {
            return deltas;
        }

        foreach (var relation in relations.EnumerateArray())
        {
            var relationType = relation.TryGetProperty("rel", out var relEl) ? relEl.GetString() : null;
            var url = relation.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;

            if (string.IsNullOrEmpty(relationType) || string.IsNullOrEmpty(url))
            {
                continue;
            }

            // Extract work item ID from URL
            var targetId = ExtractWorkItemIdFromUrl(url);
            if (targetId <= 0)
            {
                continue;
            }

            // Check if this is an added or removed relation
            var isAdded = relation.TryGetProperty("isNew", out var isNewEl) && isNewEl.GetBoolean();
            var isRemoved = relation.TryGetProperty("isRemoved", out var isRemovedEl) && isRemovedEl.GetBoolean();

            if (isAdded || isRemoved)
            {
                deltas.Add(new RelationDelta
                {
                    ChangeType = isAdded ? Core.Contracts.RelationChangeType.Added : Core.Contracts.RelationChangeType.Removed,
                    RelationType = relationType,
                    TargetWorkItemId = targetId
                });
            }
        }

        return deltas;
    }

    private List<RelationInfo> ParseRelations(JsonElement revision)
    {
        var relations = new List<RelationInfo>();

        if (!revision.TryGetProperty("relations", out var relationsArray))
        {
            return relations;
        }

        foreach (var relation in relationsArray.EnumerateArray())
        {
            var relationType = relation.TryGetProperty("rel", out var relEl) ? relEl.GetString() : null;
            var url = relation.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;

            if (string.IsNullOrEmpty(relationType) || string.IsNullOrEmpty(url))
            {
                continue;
            }

            var targetId = ExtractWorkItemIdFromUrl(url);
            if (targetId > 0)
            {
                relations.Add(new RelationInfo(relationType, targetId));
            }
        }

        return relations;
    }

    private static int ExtractWorkItemIdFromUrl(string url)
    {
        // URL format: https://server/collection/_apis/wit/workItems/{id}
        try
        {
            var lastSlash = url.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < url.Length - 1)
            {
                var idPart = url[(lastSlash + 1)..];
                if (int.TryParse(idPart, out var id))
                {
                    return id;
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return 0;
    }

    private static string? GetStringField(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object when element.TryGetProperty("displayName", out var dn) => dn.GetString(),
            JsonValueKind.Object when element.TryGetProperty("name", out var n) => n.GetString(),
            _ => element.GetRawText()
        };
    }

    private static DateTimeOffset? GetDateTimeField(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            if (DateTimeOffset.TryParse(element.GetString(), out var dto))
            {
                return dto;
            }
        }

        try
        {
            return element.GetDateTimeOffset();
        }
        catch
        {
            return null;
        }
    }

    private static int? GetIntField(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetInt32();
        }

        if (element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), out var intValue))
        {
            return intValue;
        }

        return null;
    }

    private HttpClient GetAuthenticatedHttpClient()
    {
        var client = _httpClientFactory.CreateClient("TfsClient.NTLM");
        _logger.LogDebug("Using NTLM-authenticated HttpClient for revision TFS request");
        return client;
    }

    private void ValidateTfsConfiguration(TfsConfigEntity? config)
    {
        if (config == null)
        {
            throw new TfsConfigurationException("TFS is not configured. Please configure TFS settings first.");
        }

        if (string.IsNullOrWhiteSpace(config.Url))
        {
            throw new TfsConfigurationException("TFS URL is not configured.");
        }
    }

    private async Task HandleHttpErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var statusCode = (int)response.StatusCode;

        _logger.LogError(
            "TFS API error: {StatusCode} {ReasonPhrase}. Response: {Content}",
            statusCode, response.ReasonPhrase, content);

        throw response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => new TfsAuthenticationException("TFS authentication failed. Check credentials.", content),
            System.Net.HttpStatusCode.Forbidden => new TfsAuthorizationException("TFS access denied. Check permissions.", content),
            System.Net.HttpStatusCode.NotFound => new TfsResourceNotFoundException($"TFS resource not found: {response.RequestMessage?.RequestUri}", content),
            _ => new TfsException($"TFS API error: {statusCode} {response.ReasonPhrase}", statusCode, content)
        };
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var baseDelay = TimeSpan.FromSeconds(1);

        while (true)
        {
            try
            {
                return await _throttler.ExecuteReadAsync(operation, cancellationToken);
            }
            catch (TfsAuthenticationException)
            {
                throw; // Don't retry auth errors
            }
            catch (TfsAuthorizationException)
            {
                throw; // Don't retry authorization errors
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (retryCount < MaxRetries)
            {
                retryCount++;
                var delay = baseDelay * Math.Pow(2, retryCount - 1);

                _logger.LogWarning(
                    ex,
                    "TFS revision API call failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms",
                    retryCount, MaxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private record RelationInfo(string RelationType, int TargetId);
}
