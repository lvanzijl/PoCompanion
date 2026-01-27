using System.Text.Json.Serialization;

namespace PoTool.Integrations.Tfs.Models.Internal;

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
    [JsonPropertyName("ids")]
    public required int[] Ids { get; init; }

    /// <summary>
    /// Optional array of field reference names to retrieve.
    /// If not specified, all fields are returned.
    /// </summary>
    [JsonPropertyName("fields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Fields { get; init; }

    /// <summary>
    /// Optional expansion for additional data (e.g., "relations").
    /// </summary>
    [JsonPropertyName("$expand")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expand { get; init; }
}
