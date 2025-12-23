using PoTool.Client.ApiClient;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// Service for filtering work items with validation and text-based criteria.
/// Handles ancestor inclusion logic to maintain hierarchy visibility.
/// </summary>
public class WorkItemFilteringService
{
    private readonly ITreeBuilderService _treeBuilderService;

    public WorkItemFilteringService(ITreeBuilderService treeBuilderService)
    {
        _treeBuilderService = treeBuilderService ?? throw new ArgumentNullException(nameof(treeBuilderService));
    }

    /// <summary>
    /// Filters work items by validation issues, including their ancestors for hierarchy visibility.
    /// </summary>
    /// <param name="items">Work items to filter.</param>
    /// <param name="targetIds">IDs of work items with validation issues to include.</param>
    /// <returns>Filtered work items including ancestors.</returns>
    public IEnumerable<WorkItemWithValidationDto> FilterByValidationWithAncestors(
        IEnumerable<WorkItemWithValidationDto> items,
        HashSet<int> targetIds)
    {
        var itemsList = items.ToList();
        var itemLookup = itemsList.ToDictionary(w => w.TfsId);
        var toInclude = new Dictionary<int, WorkItemWithValidationDto>();

        foreach (var targetId in targetIds)
        {
            if (!itemLookup.TryGetValue(targetId, out var item))
                continue;

            // Include the target item
            if (!toInclude.ContainsKey(item.TfsId))
            {
                toInclude[item.TfsId] = item;
            }

            // Include all ancestors
            var current = item;
            while (current.ParentTfsId.HasValue)
            {
                var parentId = current.ParentTfsId.Value;
                if (itemLookup.TryGetValue(parentId, out var parent))
                {
                    if (!toInclude.ContainsKey(parent.TfsId))
                    {
                        toInclude[parent.TfsId] = parent;
                    }
                    current = parent;
                }
                else
                {
                    break;
                }
            }
        }

        return toInclude.Values.ToList();
    }

    /// <summary>
    /// Extracts work item IDs that match the given validation filter.
    /// </summary>
    /// <param name="workItems">Work items to search.</param>
    /// <param name="filterId">Filter identifier (e.g., "parentProgress", "missingEffort").</param>
    /// <returns>IDs of work items matching the filter.</returns>
    public IEnumerable<int> GetWorkItemIdsByValidationFilter(
        IEnumerable<WorkItemWithValidationDto> workItems,
        string filterId)
    {
        return filterId switch
        {
            "parentProgress" => workItems
                .Where(wi => wi.ValidationIssues.Any(issue =>
                    issue.Message.Contains("Parent") || issue.Message.Contains("Ancestor")))
                .Select(wi => wi.TfsId),
            "missingEffort" => workItems
                .Where(wi => wi.ValidationIssues.Any(issue =>
                    issue.Message.Contains("effort")))
                .Select(wi => wi.TfsId),
            _ => Enumerable.Empty<int>()
        };
    }

    /// <summary>
    /// Counts work items matching the given validation filter.
    /// </summary>
    /// <param name="workItems">Work items to count.</param>
    /// <param name="filterId">Filter identifier.</param>
    /// <returns>Count of matching work items.</returns>
    public int CountWorkItemsByValidationFilter(
        IEnumerable<WorkItemWithValidationDto> workItems,
        string filterId)
    {
        return GetWorkItemIdsByValidationFilter(workItems, filterId).Count();
    }

    /// <summary>
    /// Applies combined text and validation filters to work items, preserving hierarchy.
    /// </summary>
    /// <param name="workItems">Work items to filter.</param>
    /// <param name="textFilter">Optional text filter.</param>
    /// <param name="enabledValidationFilters">List of enabled validation filter IDs.</param>
    /// <returns>Filtered work items with ancestors included.</returns>
    public IEnumerable<WorkItemWithValidationDto> ApplyCombinedFilter(
        IEnumerable<WorkItemWithValidationDto> workItems,
        string? textFilter,
        IEnumerable<string> enabledValidationFilters)
    {
        var filteredItems = workItems;

        // Apply validation filters first (OR logic between filters)
        var validationFilterList = enabledValidationFilters?.ToList() ?? new List<string>();
        if (validationFilterList.Any())
        {
            var invalidIds = new HashSet<int>();

            foreach (var filterId in validationFilterList)
            {
                var filterInvalidIds = GetWorkItemIdsByValidationFilter(workItems, filterId);
                foreach (var id in filterInvalidIds)
                {
                    invalidIds.Add(id);
                }
            }

            // Include invalid items and their ancestors
            filteredItems = FilterByValidationWithAncestors(filteredItems, invalidIds);
        }

        // Then apply text filter if present
        if (!string.IsNullOrWhiteSpace(textFilter))
        {
            var workItemsLookup = workItems.ToDictionary(wi => wi.TfsId);
            filteredItems = _treeBuilderService.FilterWithAncestors(
                filteredItems.Select(ConvertToWorkItemDto).ToList(),
                textFilter
            ).Select(dto => workItemsLookup[dto.TfsId]);
        }

        return filteredItems;
    }

    /// <summary>
    /// Checks if a work item is a descendant of any configured goals.
    /// </summary>
    /// <param name="item">Work item to check.</param>
    /// <param name="goalIds">List of goal IDs.</param>
    /// <param name="allWorkItems">All work items for lookup.</param>
    /// <returns>True if item is a goal or descendant of a goal.</returns>
    public bool IsDescendantOfGoals(
        WorkItemWithValidationDto item,
        List<int> goalIds,
        IEnumerable<WorkItemWithValidationDto> allWorkItems)
    {
        if (goalIds == null || goalIds.Count == 0)
            return true;

        // Check if this item is itself a goal
        if (goalIds.Contains(item.TfsId))
            return true;

        // Create lookup dictionary for efficient parent traversal
        var itemLookup = allWorkItems.ToDictionary(wi => wi.TfsId);

        // Traverse up the parent chain to see if any ancestor is a configured goal
        var current = item;
        while (current.ParentTfsId.HasValue)
        {
            if (goalIds.Contains(current.ParentTfsId.Value))
                return true;

            // Find parent in lookup dictionary
            if (!itemLookup.TryGetValue(current.ParentTfsId.Value, out current))
                break;
        }

        return false;
    }

    private static WorkItemDto ConvertToWorkItemDto(WorkItemWithValidationDto item)
    {
        return new WorkItemDto
        {
            TfsId = item.TfsId,
            Type = item.Type,
            Title = item.Title,
            ParentTfsId = item.ParentTfsId,
            AreaPath = item.AreaPath,
            IterationPath = item.IterationPath,
            State = item.State,
            JsonPayload = item.JsonPayload,
            RetrievedAt = item.RetrievedAt,
            Effort = item.Effort
        };
    }
}
