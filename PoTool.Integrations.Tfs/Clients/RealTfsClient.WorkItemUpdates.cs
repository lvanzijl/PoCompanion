using System.Text.Json;
using PoTool.Core.Contracts;

namespace PoTool.Integrations.Tfs.Clients;

internal partial class RealTfsClient
{
    public async Task<IReadOnlyList<WorkItemUpdate>> GetWorkItemUpdatesAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _configService.GetConfigEntityAsync(cancellationToken);
        ValidateTfsConfiguration(entity);
        var config = entity!;
        var httpClient = GetAuthenticatedHttpClient();

        return await ExecuteWithRetryAsync(async () =>
        {
            var updates = new List<WorkItemUpdate>();
            var baseUrl = CollectionUrl(config, $"_apis/wit/workItems/{workItemId}/updates");
            string? continuationToken = null;

            do
            {
                var url = AddContinuationToken(baseUrl, continuationToken);
                var response = await SendGetAsync(httpClient, config, url, cancellationToken, handleErrors: false);
                await HandleHttpErrorsAsync(response, cancellationToken);

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (doc.RootElement.TryGetProperty("value", out var values) &&
                    values.ValueKind == JsonValueKind.Array)
                {
                    foreach (var updateElement in values.EnumerateArray())
                    {
                        var updateId = updateElement.TryGetProperty("id", out var idElement) &&
                                       idElement.TryGetInt32(out var parsedId)
                            ? parsedId
                            : 0;

                        var revisedDate = updateElement.TryGetProperty("revisedDate", out var revisedDateElement) &&
                                          revisedDateElement.ValueKind == JsonValueKind.String &&
                                          DateTimeOffset.TryParse(revisedDateElement.GetString(), out var parsedDate)
                            ? parsedDate
                            : DateTimeOffset.MinValue;

                        var fieldChanges = new Dictionary<string, WorkItemUpdateFieldChange>(StringComparer.OrdinalIgnoreCase);
                        if (updateElement.TryGetProperty("fields", out var fieldsElement) &&
                            fieldsElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var field in fieldsElement.EnumerateObject())
                            {
                                string? oldValue = null;
                                string? newValue = null;

                                if (field.Value.ValueKind == JsonValueKind.Object)
                                {
                                    if (field.Value.TryGetProperty("oldValue", out var oldValueElement))
                                    {
                                        oldValue = SerializeFieldValue(oldValueElement);
                                    }

                                    if (field.Value.TryGetProperty("newValue", out var newValueElement))
                                    {
                                        newValue = SerializeFieldValue(newValueElement);
                                    }
                                }

                                fieldChanges[field.Name] = new WorkItemUpdateFieldChange(field.Name, oldValue, newValue);
                            }
                        }

                        updates.Add(new WorkItemUpdate
                        {
                            WorkItemId = workItemId,
                            UpdateId = updateId,
                            RevisedDate = revisedDate,
                            FieldChanges = fieldChanges
                        });
                    }
                }

                continuationToken = GetContinuationToken(response, doc);
            } while (!string.IsNullOrWhiteSpace(continuationToken));

            return (IReadOnlyList<WorkItemUpdate>)updates;
        }, cancellationToken);
    }

    private static string? SerializeFieldValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Object => value.GetRawText(),
            JsonValueKind.Array => value.GetRawText(),
            _ => value.GetRawText()
        };
    }
}
