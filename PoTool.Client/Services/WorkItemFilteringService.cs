using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

/// <summary>
/// UI service for filtering work items with validation and text-based criteria.
/// Business logic is delegated to API layer via HTTP calls.
/// </summary>
public class WorkItemFilteringService
{
    private readonly ITreeBuilderService _treeBuilderService;
    private readonly IFilteringClient _filteringClient;

    public WorkItemFilteringService(ITreeBuilderService treeBuilderService, IFilteringClient filteringClient)
    {
        _treeBuilderService = treeBuilderService ?? throw new ArgumentNullException(nameof(treeBuilderService));
        _filteringClient = filteringClient ?? throw new ArgumentNullException(nameof(filteringClient));
    }

    /// <summary>
    /// Filters work items by validation issues, including their ancestors for hierarchy visibility.
    /// </summary>
    /// <param name="items">Work items to filter.</param>
    /// <param name="targetIds">IDs of work items with validation issues to include.</param>
    /// <returns>Filtered work items including ancestors.</returns>
    public async Task<IEnumerable<WorkItemWithValidationDto>> FilterByValidationWithAncestorsAsync(
        IEnumerable<WorkItemWithValidationDto> items,
        HashSet<int> targetIds)
    {
        // Call API to get filtered IDs
        var request = new FilterByValidationRequest { TargetIds = targetIds };
        var response = await _filteringClient.FilterByValidationWithAncestorsAsync(request);
        var payload = GeneratedCacheEnvelopeHelper.GetDataOrDefault<FilterByValidationResponse>(response);
        if (payload is null)
        {
            return Enumerable.Empty<WorkItemWithValidationDto>();
        }

        // Return work items matching the filtered IDs
        var filteredIds = new HashSet<int>(payload.WorkItemIds);
        return items.Where(item => filteredIds.Contains(item.TfsId));
    }

    /// <summary>
    /// Extracts work item IDs that match the given validation filter.
    /// </summary>
    /// <param name="workItems">Work items to search.</param>
    /// <param name="filterId">Filter identifier (e.g., "parentProgress", "missingEffort").</param>
    /// <returns>IDs of work items matching the filter.</returns>
    public async Task<IEnumerable<int>> GetWorkItemIdsByValidationFilterAsync(
        IEnumerable<WorkItemWithValidationDto> workItems,
        string filterId)
    {
        // Call API to get work item IDs by filter
        var request = new GetWorkItemIdsByValidationFilterRequest { FilterId = filterId };
        var response = await _filteringClient.GetWorkItemIdsByValidationFilterAsync(request);
        return GeneratedCacheEnvelopeHelper.GetDataOrDefault<GetWorkItemIdsByValidationFilterResponse>(response)?.WorkItemIds
            ?? Enumerable.Empty<int>();
    }

    /// <summary>
    /// Counts work items matching the given validation filter.
    /// </summary>
    /// <param name="workItems">Work items to count.</param>
    /// <param name="filterId">Filter identifier.</param>
    /// <returns>Count of matching work items.</returns>
    public async Task<int> CountWorkItemsByValidationFilterAsync(
        IEnumerable<WorkItemWithValidationDto> workItems,
        string filterId)
    {
        // Call API to count work items by filter
        var request = new CountWorkItemsByValidationFilterRequest { FilterId = filterId };
        var response = await _filteringClient.CountWorkItemsByValidationFilterAsync(request);
        return GeneratedCacheEnvelopeHelper.GetDataOrDefault<CountWorkItemsByValidationFilterResponse>(response)?.Count ?? 0;
    }

    /// <summary>
    /// Applies combined text and validation filters to work items, preserving hierarchy.
    /// </summary>
    /// <param name="workItems">Work items to filter.</param>
    /// <param name="textFilter">Optional text filter.</param>
    /// <param name="enabledValidationFilters">List of enabled validation filter IDs.</param>
    /// <returns>Filtered work items with ancestors included.</returns>
    public async Task<IEnumerable<WorkItemWithValidationDto>> ApplyCombinedFilterAsync(
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
                var filterInvalidIds = await GetWorkItemIdsByValidationFilterAsync(workItems, filterId);
                foreach (var id in filterInvalidIds)
                {
                    invalidIds.Add(id);
                }
            }

            // Include invalid items and their ancestors
            filteredItems = await FilterByValidationWithAncestorsAsync(filteredItems, invalidIds);
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
    public async Task<bool> IsDescendantOfGoalsAsync(
        WorkItemWithValidationDto item,
        List<int> goalIds,
        IEnumerable<WorkItemWithValidationDto> allWorkItems)
    {
        // Call API to check descendant status
        var request = new IsDescendantOfGoalsRequest
        {
            WorkItemId = item.TfsId,
            GoalIds = goalIds
        };
        var response = await _filteringClient.IsDescendantOfGoalsAsync(request);
        return GeneratedCacheEnvelopeHelper.GetDataOrDefault<IsDescendantOfGoalsResponse>(response)?.IsDescendant ?? false;
    }

    /// <summary>
    /// Filters work items to include only goals and their descendants in a single batch operation.
    /// This is more efficient than calling IsDescendantOfGoalsAsync for each work item individually.
    /// </summary>
    /// <param name="goalIds">List of goal IDs to filter by.</param>
    /// <returns>Set of work item IDs that are goals or descendants of goals.</returns>
    public async Task<HashSet<int>> FilterByGoalsAsync(List<int> goalIds)
    {
        // Call API to get filtered IDs in a single batch operation
        var request = new FilterByGoalsRequest { GoalIds = goalIds };
        var response = await _filteringClient.FilterByGoalsAsync(request);
        return new HashSet<int>(
            GeneratedCacheEnvelopeHelper.GetDataOrDefault<FilterByGoalsResponse>(response)?.WorkItemIds
            ?? Enumerable.Empty<int>());
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
            RetrievedAt = item.RetrievedAt,
            Effort = item.Effort
        };
    }
}
