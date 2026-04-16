using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Filtering;
using Mediator;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for work item filtering operations.
/// Exposes filtering business logic from Core layer via HTTP endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[DataSourceMode(RouteIntent.CacheOnlyAnalyticalRead)]
public class FilteringController : ControllerBase
{
    private readonly WorkItemFilterer _filterer;
    private readonly IMediator _mediator;
    private readonly ILogger<FilteringController> _logger;

    public FilteringController(
        WorkItemFilterer filterer,
        IMediator mediator,
        ILogger<FilteringController> logger)
    {
        _filterer = filterer ?? throw new ArgumentNullException(nameof(filterer));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Filters work items by validation issues, including their ancestors for hierarchy visibility.
    /// </summary>
    /// <param name="request">Filter request containing target IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered work item IDs including ancestors.</returns>
    [HttpPost("by-validation-with-ancestors")]
    public async Task<ActionResult<FilterByValidationResponse>> FilterByValidationWithAncestors(
        [FromBody] FilterByValidationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all work items with validation
            var allWorkItems = await _mediator.Send(new GetAllWorkItemsWithValidationQuery(), cancellationToken);

            // Apply filtering logic
            var wrapped = allWorkItems.Select(dto => new FilterableWorkItemAdapter(dto));
            var filtered = _filterer.FilterByValidationWithAncestors(wrapped, request.TargetIds);

            return Ok(new FilterByValidationResponse
            {
                WorkItemIds = filtered.Select(w => w.TfsId).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering work items by validation with ancestors");
            return StatusCode(500, "Error filtering work items");
        }
    }

    /// <summary>
    /// Extracts work item IDs that match the given validation filter.
    /// </summary>
    /// <param name="request">Filter request containing filter ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>IDs of work items matching the filter.</returns>
    [HttpPost("ids-by-validation-filter")]
    public async Task<ActionResult<GetWorkItemIdsByValidationFilterResponse>> GetWorkItemIdsByValidationFilter(
        [FromBody] GetWorkItemIdsByValidationFilterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all work items with validation
            var allWorkItems = await _mediator.Send(new GetAllWorkItemsWithValidationQuery(), cancellationToken);

            // Apply filtering logic
            var wrapped = allWorkItems.Select(dto => new FilterableWorkItemAdapter(dto));
            var filteredIds = _filterer.GetWorkItemIdsByValidationFilter(wrapped, request.FilterId);

            return Ok(new GetWorkItemIdsByValidationFilterResponse
            {
                WorkItemIds = filteredIds.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work item IDs by validation filter: {FilterId}", request.FilterId);
            return StatusCode(500, "Error getting work item IDs by validation filter");
        }
    }

    /// <summary>
    /// Counts work items matching the given validation filter.
    /// </summary>
    /// <param name="request">Filter request containing filter ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of matching work items.</returns>
    [HttpPost("count-by-validation-filter")]
    public async Task<ActionResult<CountWorkItemsByValidationFilterResponse>> CountWorkItemsByValidationFilter(
        [FromBody] CountWorkItemsByValidationFilterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all work items with validation
            var allWorkItems = await _mediator.Send(new GetAllWorkItemsWithValidationQuery(), cancellationToken);

            // Apply filtering logic
            var wrapped = allWorkItems.Select(dto => new FilterableWorkItemAdapter(dto));
            var count = _filterer.CountWorkItemsByValidationFilter(wrapped, request.FilterId);

            return Ok(new CountWorkItemsByValidationFilterResponse
            {
                Count = count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting work items by validation filter: {FilterId}", request.FilterId);
            return StatusCode(500, "Error counting work items by validation filter");
        }
    }

    /// <summary>
    /// Checks if a work item is a descendant of any configured goals.
    /// </summary>
    /// <param name="request">Request containing work item ID and goal IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if item is a goal or descendant of a goal.</returns>
    [HttpPost("is-descendant-of-goals")]
    public async Task<ActionResult<IsDescendantOfGoalsResponse>> IsDescendantOfGoals(
        [FromBody] IsDescendantOfGoalsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all work items with validation
            var allWorkItems = await _mediator.Send(new GetAllWorkItemsWithValidationQuery(), cancellationToken);

            // Find the specific work item
            var targetItem = allWorkItems.FirstOrDefault(wi => wi.TfsId == request.WorkItemId);
            if (targetItem == null)
            {
                return NotFound($"Work item with ID {request.WorkItemId} not found");
            }

            // Apply filtering logic
            var wrapped = new FilterableWorkItemAdapter(targetItem);
            var wrappedAll = allWorkItems.Select(wi => new FilterableWorkItemAdapter(wi));
            var isDescendant = _filterer.IsDescendantOfGoals(wrapped, request.GoalIds, wrappedAll);

            return Ok(new IsDescendantOfGoalsResponse
            {
                IsDescendant = isDescendant
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if work item {WorkItemId} is descendant of goals", request.WorkItemId);
            return StatusCode(500, "Error checking descendant status");
        }
    }

    /// <summary>
    /// Filters work items to include only goals and their descendants in a single batch operation.
    /// This is more efficient than calling is-descendant-of-goals for each work item individually.
    /// </summary>
    /// <param name="request">Request containing goal IDs to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of work item IDs that are goals or descendants of goals.</returns>
    [HttpPost("filter-by-goals")]
    public async Task<ActionResult<FilterByGoalsResponse>> FilterByGoals(
        [FromBody] FilterByGoalsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all work items with validation
            var allWorkItems = await _mediator.Send(new GetAllWorkItemsWithValidationQuery(), cancellationToken);

            // Wrap all work items for filtering
            var wrappedAll = allWorkItems.Select(wi => new FilterableWorkItemAdapter(wi)).ToList();

            // Convert goal IDs to HashSet for O(1) lookup performance
            var goalIdsSet = new HashSet<int>(request.GoalIds);

            // Filter to include only goals and their descendants
            var filteredIds = new HashSet<int>();
            foreach (var workItem in wrappedAll)
            {
                if (goalIdsSet.Contains(workItem.TfsId) ||
                    _filterer.IsDescendantOfGoals(workItem, request.GoalIds, wrappedAll))
                {
                    filteredIds.Add(workItem.TfsId);
                }
            }

            return Ok(new FilterByGoalsResponse
            {
                WorkItemIds = filteredIds.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering work items by goals");
            return StatusCode(500, "Error filtering work items by goals");
        }
    }

    /// <summary>
    /// Adapter to make API DTOs compatible with Core filtering interfaces.
    /// </summary>
    private class FilterableWorkItemAdapter : WorkItemFilterer.IFilterableWorkItem
    {
        public WorkItemWithValidationDto WorkItem { get; }

        public FilterableWorkItemAdapter(WorkItemWithValidationDto workItem)
        {
            WorkItem = workItem;
        }

        public int TfsId => WorkItem.TfsId;
        public int? ParentTfsId => WorkItem.ParentTfsId;
        public IEnumerable<WorkItemFilterer.IValidationIssue> ValidationIssues =>
            WorkItem.ValidationIssues.Select(vi => new ValidationIssueAdapter(vi));

        private class ValidationIssueAdapter : WorkItemFilterer.IValidationIssue
        {
            private readonly ValidationIssue _issue;

            public ValidationIssueAdapter(ValidationIssue issue)
            {
                _issue = issue;
            }

            public string Message => _issue.Message;
            public string? RuleId => _issue.RuleId;
        }
    }
}
