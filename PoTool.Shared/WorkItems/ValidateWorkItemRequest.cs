namespace PoTool.Shared.WorkItems;

/// <summary>
/// Request DTO for work item validation.
/// </summary>
public sealed record ValidateWorkItemRequest
{
    /// <summary>
    /// The work item ID to validate.
    /// </summary>
    public required int WorkItemId { get; init; }
}
