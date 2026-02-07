using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Shared.Exceptions;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Integrations.Tfs.Clients;

public partial class RealTfsClient
{
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

            var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);
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
}
