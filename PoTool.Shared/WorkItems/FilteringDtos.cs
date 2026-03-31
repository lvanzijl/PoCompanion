namespace PoTool.Shared.WorkItems;

/// <summary>
/// Request to filter work items by validation issues with ancestors.
/// </summary>
public class FilterByValidationRequest
{
    /// <summary>
    /// IDs of work items with validation issues to include (along with their ancestors).
    /// </summary>
    public HashSet<int>? TargetIds { get; set; }
}

/// <summary>
/// Response containing filtered work item IDs.
/// </summary>
public class FilterByValidationResponse
{
    /// <summary>
    /// IDs of work items that match the filter (including ancestors).
    /// </summary>
    public required List<int> WorkItemIds { get; set; }
}

/// <summary>
/// Request to get work item IDs by validation filter.
/// </summary>
public class GetWorkItemIdsByValidationFilterRequest
{
    /// <summary>
    /// Filter identifier (e.g., "parentProgress", "missingEffort").
    /// </summary>
    public required string FilterId { get; set; }
}

/// <summary>
/// Response containing work item IDs matching a validation filter.
/// </summary>
public class GetWorkItemIdsByValidationFilterResponse
{
    /// <summary>
    /// IDs of work items matching the filter.
    /// </summary>
    public required List<int> WorkItemIds { get; set; }
}

/// <summary>
/// Request to count work items by validation filter.
/// </summary>
public class CountWorkItemsByValidationFilterRequest
{
    /// <summary>
    /// Filter identifier.
    /// </summary>
    public required string FilterId { get; set; }
}

/// <summary>
/// Response containing count of work items matching a validation filter.
/// </summary>
public class CountWorkItemsByValidationFilterResponse
{
    /// <summary>
    /// Count of work items matching the filter.
    /// </summary>
    public required int Count { get; set; }
}

/// <summary>
/// Request to check if a work item is a descendant of goals.
/// </summary>
public class IsDescendantOfGoalsRequest
{
    /// <summary>
    /// Work item TFS ID to check.
    /// </summary>
    public required int WorkItemId { get; set; }

    /// <summary>
    /// List of goal IDs.
    /// </summary>
    public required List<int> GoalIds { get; set; }
}

/// <summary>
/// Response indicating if a work item is a descendant of goals.
/// </summary>
public class IsDescendantOfGoalsResponse
{
    /// <summary>
    /// True if item is a goal or descendant of a goal.
    /// </summary>
    public required bool IsDescendant { get; set; }
}

/// <summary>
/// Request to filter work items by goals in a batch operation.
/// </summary>
public class FilterByGoalsRequest
{
    /// <summary>
    /// List of goal IDs to filter by.
    /// </summary>
    public required List<int> GoalIds { get; set; }
}

/// <summary>
/// Response containing work item IDs that are goals or descendants of goals.
/// </summary>
public class FilterByGoalsResponse
{
    /// <summary>
    /// IDs of work items that are goals or descendants of goals.
    /// </summary>
    public required List<int> WorkItemIds { get; set; }
}

/// <summary>
/// Request to apply combined text and validation filters.
/// </summary>
public class ApplyCombinedFilterRequest
{
    /// <summary>
    /// Optional text filter.
    /// </summary>
    public string? TextFilter { get; set; }

    /// <summary>
    /// List of enabled validation filter IDs.
    /// </summary>
    public required List<string> EnabledValidationFilters { get; set; }
}

/// <summary>
/// Response containing filtered work item IDs.
/// </summary>
public class ApplyCombinedFilterResponse
{
    /// <summary>
    /// IDs of work items matching the combined filter.
    /// </summary>
    public required List<int> WorkItemIds { get; set; }
}
