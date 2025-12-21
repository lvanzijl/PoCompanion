using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for work item operations.
/// Uses Mediator pattern for clean separation of concerns.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkItemsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WorkItemsController> _logger;

    public WorkItemsController(
        IMediator mediator,
        ILogger<WorkItemsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets all cached work items.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var workItems = await _mediator.Send(new GetAllWorkItemsQuery(), cancellationToken);
            return Ok(workItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work items");
            return StatusCode(500, "Error retrieving work items");
        }
    }

    /// <summary>
    /// Gets all cached work items with validation results.
    /// </summary>
    [HttpGet("validated")]
    public async Task<ActionResult<IEnumerable<WorkItemWithValidationDto>>> GetAllWithValidation(CancellationToken cancellationToken)
    {
        try
        {
            var workItems = await _mediator.Send(new GetAllWorkItemsWithValidationQuery(), cancellationToken);
            return Ok(workItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work items with validation");
            return StatusCode(500, "Error retrieving work items with validation");
        }
    }

    /// <summary>
    /// Gets work items matching a filter.
    /// </summary>
    [HttpGet("filter/{filter}")]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetFiltered(
        string filter,
        CancellationToken cancellationToken)
    {
        try
        {
            var workItems = await _mediator.Send(new GetFilteredWorkItemsQuery(filter), cancellationToken);
            return Ok(workItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering work items with filter: {Filter}", filter);
            return StatusCode(500, "Error filtering work items");
        }
    }

    /// <summary>
    /// Gets a specific work item by its TFS ID.
    /// </summary>
    [HttpGet("{tfsId:int}")]
    public async Task<ActionResult<WorkItemDto>> GetByTfsId(
        int tfsId,
        CancellationToken cancellationToken)
    {
        try
        {
            var workItem = await _mediator.Send(new GetWorkItemByIdQuery(tfsId), cancellationToken);
            
            if (workItem == null)
            {
                return NotFound();
            }

            return Ok(workItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work item with TFS ID: {TfsId}", tfsId);
            return StatusCode(500, "Error retrieving work item");
        }
    }

    /// <summary>
    /// Gets the revision history for a specific work item.
    /// </summary>
    [HttpGet("{workItemId:int}/revisions")]
    public async Task<ActionResult<IEnumerable<WorkItemRevisionDto>>> GetWorkItemRevisions(
        int workItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            var revisions = await _mediator.Send(new GetWorkItemRevisionsQuery(workItemId), cancellationToken);
            return Ok(revisions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving revisions for work item {WorkItemId}", workItemId);
            return StatusCode(500, "Error retrieving work item revisions");
        }
    }

    /// <summary>
    /// Gets all goals (work items of type Goal).
    /// </summary>
    [HttpGet("goals/all")]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetAllGoals(CancellationToken cancellationToken)
    {
        try
        {
            var goals = await _mediator.Send(new GetAllGoalsQuery(), cancellationToken);
            return Ok(goals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all goals");
            return StatusCode(500, "Error retrieving all goals");
        }
    }

    /// <summary>
    /// Gets work items for specific Goal IDs (full hierarchy from Goals down to Tasks).
    /// </summary>
    [HttpGet("goals")]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetGoalHierarchy(
        [FromQuery] string goalIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var ids = goalIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();

            if (ids.Count == 0)
            {
                return BadRequest("At least one goal ID must be provided");
            }

            // Validate that all IDs are positive
            if (ids.Any(id => id <= 0))
            {
                return BadRequest("All goal IDs must be positive integers");
            }

            var workItems = await _mediator.Send(new GetGoalHierarchyQuery(ids), cancellationToken);
            return Ok(workItems);
        }
        catch (FormatException)
        {
            return BadRequest("Invalid goal ID format");
        }
        catch (OverflowException)
        {
            return BadRequest("Goal ID value is too large");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal hierarchy for goal IDs: {GoalIds}", goalIds);
            return StatusCode(500, "Error retrieving goal hierarchy");
        }
    }
}
