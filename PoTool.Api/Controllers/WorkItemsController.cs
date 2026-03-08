using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.Health;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;
using PoTool.Core.WorkItems.Commands;

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
    /// Gets all distinct area paths from cached work items.
    /// </summary>
    [HttpGet("area-paths")]
    public async Task<ActionResult<IEnumerable<string>>> GetDistinctAreaPaths(CancellationToken cancellationToken)
    {
        try
        {
            var areaPaths = await _mediator.Send(new GetDistinctAreaPathsQuery(), cancellationToken);
            return Ok(areaPaths);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving distinct area paths");
            return StatusCode(500, "Error retrieving distinct area paths");
        }
    }

    /// <summary>
    /// Gets all cached work items with validation results.
    /// Optionally filtered by product IDs for efficient loading.
    /// </summary>
    /// <param name="productIds">Optional comma-separated list of product IDs to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("validated")]
    public async Task<ActionResult<IEnumerable<WorkItemWithValidationDto>>> GetAllWithValidation(
        [FromQuery] string? productIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryParseProductIds(productIds, out var productIdArray, out var badRequest))
                return badRequest!;

            var workItems = await _mediator.Send(new GetAllWorkItemsWithValidationQuery(productIdArray), cancellationToken);
            return Ok(workItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work items with validation");
            return StatusCode(500, "Error retrieving work items with validation");
        }
    }

    /// <summary>
    /// Gets a specific work item with validation by TFS ID.
    /// More efficient than fetching all work items and filtering on the client.
    /// </summary>
    /// <param name="tfsId">The TFS ID of the work item to retrieve</param>
    /// <param name="productIds">Optional comma-separated list of product IDs to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("validated/{tfsId:int}")]
    public async Task<ActionResult<WorkItemWithValidationDto>> GetByIdWithValidation(
        int tfsId,
        [FromQuery] string? productIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryParseProductIds(productIds, out var productIdArray, out var badRequest))
                return badRequest!;

            var workItem = await _mediator.Send(new GetWorkItemByIdWithValidationQuery(tfsId, productIdArray), cancellationToken);
            
            if (workItem == null)
            {
                return NotFound();
            }

            return Ok(workItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work item {TfsId} with validation", tfsId);
            return StatusCode(500, "Error retrieving work item with validation");
        }
    }

    /// <summary>
    /// Gets a grouped validation triage summary for the Validation Triage page.
    /// Returns per-category item counts and top rule groups (SI, RR, RC, EFF).
    /// </summary>
    /// <param name="productIds">Optional comma-separated list of product IDs to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("validation-triage")]
    public async Task<ActionResult<ValidationTriageSummaryDto>> GetValidationTriage(
        [FromQuery] string? productIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryParseProductIds(productIds, out var productIdArray, out var badRequest))
                return badRequest!;

            var summary = await _mediator.Send(new GetValidationTriageSummaryQuery(productIdArray), cancellationToken);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving validation triage summary");
            return StatusCode(500, "Error retrieving validation triage summary");
        }
    }

    /// <summary>
    /// Gets the validation queue for a specific category, listing rule groups sorted by item count.
    /// Used by the Validation Queue page.
    /// </summary>
    /// <param name="category">Category key: SI, RR, RC, or EFF</param>
    /// <param name="productIds">Optional comma-separated list of product IDs to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("validation-queue")]
    public async Task<ActionResult<ValidationQueueDto>> GetValidationQueue(
        [FromQuery] string category,
        [FromQuery] string? productIds = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return BadRequest("category query parameter is required.");
        }

        try
        {
            if (!TryParseProductIds(productIds, out var productIdArray, out var badRequest))
                return badRequest!;

            var queue = await _mediator.Send(new GetValidationQueueQuery(category, productIdArray), cancellationToken);
            return Ok(queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving validation queue for category {Category}", category);
            return StatusCode(500, "Error retrieving validation queue");
        }
    }

    /// <summary>
    /// Gets the validation fix session for a specific rule.
    /// Returns all work items that violate the rule, ordered by TFS ID.
    /// Used by the Validation Fix Session page.
    /// </summary>
    /// <param name="ruleId">Rule identifier (e.g. "SI-1", "RC-2")</param>
    /// <param name="category">Category key: SI, RR, RC, or EFF</param>
    /// <param name="productIds">Optional comma-separated list of product IDs to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("validation-fix")]
    public async Task<ActionResult<ValidationFixSessionDto>> GetValidationFixSession(
        [FromQuery] string ruleId,
        [FromQuery] string category,
        [FromQuery] string? productIds = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            return BadRequest("ruleId query parameter is required.");

        if (string.IsNullOrWhiteSpace(category))
            return BadRequest("category query parameter is required.");

        try
        {
            if (!TryParseProductIds(productIds, out var productIdArray, out var badRequest))
                return badRequest!;

            var session = await _mediator.Send(
                new GetValidationFixSessionQuery(ruleId, category, productIdArray),
                cancellationToken);
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving validation fix session for rule {RuleId}", ruleId);
            return StatusCode(500, "Error retrieving validation fix session");
        }
    }

    /// <summary>
    /// Re-fetches a work item from TFS and updates the local DB cache.
    /// Used by the Fix Session "Refresh from TFS" feature.
    /// </summary>
    /// <param name="tfsId">The TFS work item ID to refresh.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{tfsId:int}/refresh-from-tfs")]
    public async Task<IActionResult> RefreshFromTfs(int tfsId, CancellationToken cancellationToken)
    {
        try
        {
            var found = await _mediator.Send(new RefreshWorkItemFromTfsCommand(tfsId), cancellationToken);
            if (!found)
                return NotFound($"Work item {tfsId} was not found in TFS.");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing work item {TfsId} from TFS", tfsId);
            return StatusCode(500, "Error refreshing work item from TFS");
        }
    }

    /// <summary>
    /// Updates the tags of a work item in TFS and refreshes the local cache.
    /// </summary>
    [HttpPost("{tfsId:int}/tags")]
    public async Task<ActionResult<WorkItemDto>> UpdateTags(int tfsId, [FromBody] UpdateTagsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new UpdateWorkItemTagsCommand(tfsId, request.Tags), cancellationToken);
            if (result == null)
                return BadRequest($"Failed to update tags for work item {tfsId}.");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tags for work item {TfsId}", tfsId);
            return StatusCode(500, "Error updating tags");
        }
    }

    /// <summary>
    /// Updates the title and/or description of a work item in TFS and refreshes the local cache.
    /// </summary>
    [HttpPost("{tfsId:int}/title-description")]
    public async Task<ActionResult<WorkItemDto>> UpdateTitleDescription(int tfsId, [FromBody] UpdateTitleDescriptionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new UpdateWorkItemTitleDescriptionCommand(tfsId, request.Title, request.Description), cancellationToken);
            if (result == null)
                return BadRequest($"Failed to update title/description for work item {tfsId}.");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating title/description for work item {TfsId}", tfsId);
            return StatusCode(500, "Error updating title/description");
        }
    }

    /// <summary>
    /// Updates the BacklogPriority of a work item in TFS and refreshes the local cache.
    /// Used by Product Roadmaps to reorder product lanes (Objectives).
    /// </summary>
    /// <param name="tfsId">The TFS work item ID.</param>
    /// <param name="request">The new backlog priority value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{tfsId:int}/backlog-priority")]
    public async Task<IActionResult> UpdateBacklogPriority(int tfsId, [FromBody] UpdateBacklogPriorityRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _mediator.Send(new UpdateWorkItemBacklogPriorityCommand(tfsId, request.Priority), cancellationToken);
            if (!success)
                return BadRequest($"Failed to update BacklogPriority for work item {tfsId}.");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating BacklogPriority for work item {TfsId}", tfsId);
            return StatusCode(500, "Error updating BacklogPriority");
        }
    }

    /// <summary>
    /// Updates the IterationPath (sprint assignment) of a work item in TFS and refreshes the local cache.
    /// Used by the Plan Board to move features between sprints.
    /// </summary>
    /// <param name="tfsId">The TFS work item ID.</param>
    /// <param name="request">The new iteration path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{tfsId:int}/iteration-path")]
    public async Task<IActionResult> UpdateIterationPath(int tfsId, [FromBody] UpdateIterationPathRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _mediator.Send(new UpdateWorkItemIterationPathCommand(tfsId, request.IterationPath), cancellationToken);
            if (!success)
                return BadRequest($"Failed to update IterationPath for work item {tfsId}.");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IterationPath for work item {TfsId}", tfsId);
            return StatusCode(500, "Error updating IterationPath");
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
    /// Validates a work item by ID directly from TFS (bypasses cache).
    /// Used specifically for validating backlog root work item IDs in product creation/editing.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidateWorkItemResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateWorkItemResponse>> ValidateWorkItem(
        [FromBody] ValidateWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _mediator.Send(new ValidateWorkItemQuery(request.WorkItemId), cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating work item {WorkItemId}", request.WorkItemId);
            // Return a response with error instead of throwing
            return Ok(new ValidateWorkItemResponse
            {
                Exists = false,
                Id = request.WorkItemId,
                Title = null,
                Type = null,
                ErrorMessage = $"Unexpected error: {ex.Message}"
            });
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
    /// Gets area paths directly from TFS (bypasses cache).
    /// Used specifically for Add Profile flow where cache is not yet populated.
    /// </summary>
    [HttpGet("area-paths/from-tfs")]
    public async Task<ActionResult<IEnumerable<string>>> GetAreaPathsFromTfs(CancellationToken cancellationToken)
    {
        try
        {
            var areaPaths = await _mediator.Send(new GetAreaPathsFromTfsQuery(), cancellationToken);
            return Ok(areaPaths);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving area paths from TFS");
            return StatusCode(500, "Error retrieving area paths from TFS");
        }
    }

    /// <summary>
    /// Gets goals directly from TFS (bypasses cache).
    /// Used specifically for Add Profile flow where cache is not yet populated.
    /// </summary>
    [HttpGet("goals/from-tfs")]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetGoalsFromTfs(CancellationToken cancellationToken)
    {
        try
        {
            var goals = await _mediator.Send(new GetGoalsFromTfsQuery(), cancellationToken);
            return Ok(goals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goals from TFS");
            return StatusCode(500, "Error retrieving goals from TFS");
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

    /// <summary>
    /// Gets historical state timeline for a specific work item showing state transitions and bottlenecks.
    /// </summary>
    /// <param name="id">Work item TFS ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Work item state timeline or 404 if work item not found</returns>
    [HttpGet("{id:int}/state-timeline")]
    public async Task<ActionResult<WorkItemStateTimelineDto>> GetStateTimeline(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var timeline = await _mediator.Send(new GetWorkItemStateTimelineQuery(id), cancellationToken);

            if (timeline == null)
            {
                return NotFound($"Work item with ID {id} not found");
            }

            return Ok(timeline);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving state timeline for work item ID: {WorkItemId}", id);
            return StatusCode(500, "Error retrieving work item state timeline");
        }
    }

    /// <summary>
    /// Gets revision history for a specific work item.
    /// Retrieves revisions directly from TFS.
    /// </summary>
    /// <param name="workItemId">Work item TFS ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Revision history for the work item</returns>
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
            _logger.LogError(ex, "Error retrieving revisions for work item ID: {WorkItemId}", workItemId);
            return StatusCode(500, "Error retrieving work item revisions");
        }
    }

    /// <summary>
    /// Gets work items using advanced multi-dimensional filtering.
    /// </summary>
    /// <param name="typeFilter">Filter by work item type</param>
    /// <param name="stateFilter">Filter by state</param>
    /// <param name="iterationPathFilter">Filter by iteration path (contains)</param>
    /// <param name="areaPathFilter">Filter by area path (contains)</param>
    /// <param name="minEffort">Minimum effort value</param>
    /// <param name="maxEffort">Maximum effort value</param>
    /// <param name="hasValidationIssues">Filter items with validation issues</param>
    /// <param name="isBlocked">Filter blocked items</param>
    /// <param name="titleSearch">Search in title (contains)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Filtered work items</returns>
    [HttpGet("advanced-filter")]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetAdvancedFiltered(
        [FromQuery] string? typeFilter = null,
        [FromQuery] string? stateFilter = null,
        [FromQuery] string? iterationPathFilter = null,
        [FromQuery] string? areaPathFilter = null,
        [FromQuery] int? minEffort = null,
        [FromQuery] int? maxEffort = null,
        [FromQuery] bool? hasValidationIssues = null,
        [FromQuery] bool? isBlocked = null,
        [FromQuery] string? titleSearch = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetFilteredWorkItemsAdvancedQuery(
                typeFilter,
                stateFilter,
                iterationPathFilter,
                areaPathFilter,
                minEffort,
                maxEffort,
                hasValidationIssues,
                isBlocked,
                titleSearch
            );

            var workItems = await _mediator.Send(query, cancellationToken);
            return Ok(workItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering work items with advanced filter");
            return StatusCode(500, "Error filtering work items");
        }
    }

    /// <summary>
    /// Gets dependency graph showing work item relationships and critical paths.
    /// </summary>
    /// <param name="areaPathFilter">Optional area path filter</param>
    /// <param name="workItemIds">Optional comma-separated list of work item IDs to include</param>
    /// <param name="workItemTypes">Optional comma-separated list of work item types to include (e.g., "Epic,Feature,Task")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dependency graph with nodes, links, critical paths, and circular dependencies</returns>
    [HttpGet("dependency-graph")]
    public async Task<ActionResult<DependencyGraphDto>> GetDependencyGraph(
        [FromQuery] string? areaPathFilter = null,
        [FromQuery] string? workItemIds = null,
        [FromQuery] string? workItemTypes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<int>? ids = null;

            if (!string.IsNullOrWhiteSpace(workItemIds))
            {
                try
                {
                    ids = workItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse)
                        .ToList();
                }
                catch (FormatException)
                {
                    return BadRequest("Invalid work item ID format. Must be comma-separated integers.");
                }
            }

            IReadOnlyList<string>? types = null;
            if (!string.IsNullOrWhiteSpace(workItemTypes))
            {
                types = workItemTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToList();
            }

            var query = new GetDependencyGraphQuery(areaPathFilter, ids, types);
            var graph = await _mediator.Send(query, cancellationToken);

            return Ok(graph);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dependency graph");
            return StatusCode(500, "Error retrieving dependency graph");
        }
    }

    /// <summary>
    /// Gets historical validation violation records for tracking patterns over time.
    /// </summary>
    /// <param name="areaPathFilter">Optional area path filter</param>
    /// <param name="startDate">Optional start date for filtering violations</param>
    /// <param name="endDate">Optional end date for filtering violations</param>
    /// <param name="violationType">Optional violation type filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Historical validation violation records</returns>
    [HttpGet("validation-history")]
    public async Task<ActionResult<IEnumerable<ValidationViolationHistoryDto>>> GetValidationHistory(
        [FromQuery] string? areaPathFilter = null,
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        [FromQuery] string? violationType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetValidationViolationHistoryQuery(
                areaPathFilter,
                startDate,
                endDate,
                violationType
            );
            var history = await _mediator.Send(query, cancellationToken);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving validation history");
            return StatusCode(500, "Error retrieving validation history");
        }
    }

    /// <summary>
    /// Gets impact analysis of validation violations showing blocked work items and recommendations.
    /// </summary>
    /// <param name="areaPathFilter">Optional area path filter</param>
    /// <param name="iterationPathFilter">Optional iteration path filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation impact analysis with blocked items and workflow recommendations</returns>
    [HttpGet("validation-impact-analysis")]
    public async Task<ActionResult<ValidationImpactAnalysisDto>> GetValidationImpactAnalysis(
        [FromQuery] string? areaPathFilter = null,
        [FromQuery] string? iterationPathFilter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetValidationImpactAnalysisQuery(
                areaPathFilter,
                iterationPathFilter
            );
            var analysis = await _mediator.Send(query, cancellationToken);
            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving validation impact analysis");
            return StatusCode(500, "Error retrieving validation impact analysis");
        }
    }

    /// <summary>
    /// Fixes validation violations in batch by updating work item states in TFS.
    /// </summary>
    /// <param name="command">Batch fix command with list of fixes to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of batch fix operation showing success/failure for each item</returns>
    [HttpPost("fix-validation-violations")]
    public async Task<ActionResult<FixValidationViolationResultDto>> FixValidationViolations(
        [FromBody] FixValidationViolationBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (command.Fixes == null || command.Fixes.Count == 0)
            {
                return BadRequest("At least one fix must be provided");
            }

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing validation violations");
            return StatusCode(500, "Error fixing validation violations");
        }
    }

    /// <summary>
    /// Assigns effort estimates to multiple work items in batch.
    /// Provides efficient bulk update with validation and error handling.
    /// </summary>
    /// <param name="command">Bulk effort assignment command with list of work item ID and effort pairs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of bulk assignment operation showing success/failure for each item</returns>
    [HttpPost("bulk-assign-effort")]
    public async Task<ActionResult<BulkEffortAssignmentResultDto>> BulkAssignEffort(
        [FromBody] BulkAssignEffortCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (command.Assignments == null || command.Assignments.Count == 0)
            {
                return BadRequest("At least one assignment must be provided");
            }

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning effort in bulk");
            return StatusCode(500, "Error assigning effort in bulk");
        }
    }

    /// <summary>
    /// Gets work items by root IDs (hierarchical tree loading).
    /// Loads the complete hierarchy starting from specified root work item IDs.
    /// Used for product-scoped loading operations.
    /// </summary>
    /// <param name="rootIds">Comma-separated list of root work item IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of work items including roots and their descendants</returns>
    [HttpGet("by-root-ids")]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetByRootIds(
        [FromQuery] string rootIds,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rootIds))
            {
                return BadRequest("Root IDs parameter is required");
            }

            var ids = rootIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToArray();

            if (ids.Length == 0)
            {
                return BadRequest("At least one root ID must be provided");
            }

            // Validate that all IDs are positive
            if (ids.Any(id => id <= 0))
            {
                return BadRequest("All root IDs must be positive integers");
            }

            var workItems = await _mediator.Send(new GetWorkItemsByRootIdsQuery(ids), cancellationToken);
            return Ok(workItems);
        }
        catch (FormatException)
        {
            return BadRequest("Invalid root ID format");
        }
        catch (OverflowException)
        {
            return BadRequest("Root ID value is too large");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work items by root IDs: {RootIds}", rootIds);
            return StatusCode(500, "Error retrieving work items by root IDs");
        }
    }

    /// <summary>
    /// Gets the available bug severity options in TFS format.
    /// Returns severity values as they appear in TFS (e.g., "1 - Critical", "2 - High", etc.).
    /// </summary>
    [HttpGet("bug-severity-options")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<string>> GetBugSeverityOptions()
    {
        // Return standard TFS severity values in the order they appear in TFS
        // This matches the format used in Microsoft.VSTS.Common.Severity field
        var severityOptions = new List<string>
        {
            "1 - Critical",
            "2 - High",
            "3 - Medium",
            "4 - Low"
        };

        _logger.LogDebug("Returning {Count} bug severity options", severityOptions.Count);
        return Ok(severityOptions);
    }

    /// <summary>
    /// Gets the product-scoped backlog state with hierarchical refinement scores.
    /// Returns scores per Epic → Feature → PBI without any SI / sprint / velocity data.
    /// </summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Backlog state with refinement scores, or 404 if the product is not found.</returns>
    [HttpGet("backlog-state/{productId:int}")]
    public async Task<ActionResult<ProductBacklogStateDto>> GetBacklogState(
        int productId,
        CancellationToken cancellationToken)
    {
        try
        {
            var state = await _mediator.Send(new GetProductBacklogStateQuery(productId), cancellationToken);

            if (state is null)
            {
                return NotFound($"Product with ID {productId} not found");
            }

            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backlog state for ProductId: {ProductId}", productId);
            return StatusCode(500, "Error retrieving backlog state");
        }
    }

    /// <summary>
    /// Parses a comma-separated product IDs string into an int array.
    /// Returns null when <paramref name="productIds"/> is null or whitespace.
    /// Returns a BadRequest result when the format is invalid.
    /// </summary>
    private bool TryParseProductIds(string? productIds, out int[]? result, out ActionResult? badRequest)
    {
        result = null;
        badRequest = null;

        if (string.IsNullOrWhiteSpace(productIds))
        {
            return true;
        }

        try
        {
            result = productIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToArray();
            return true;
        }
        catch (FormatException)
        {
            badRequest = BadRequest("Invalid product ID format. Must be comma-separated integers.");
            return false;
        }
    }
}

/// <summary>
/// Request body for updating a work item's BacklogPriority.
/// </summary>
public sealed record UpdateBacklogPriorityRequest(double Priority);

/// <summary>
/// Request body for updating a work item's tags.
/// </summary>
public sealed record UpdateTagsRequest(List<string> Tags);

/// <summary>
/// Request body for updating a work item's title and/or description.
/// </summary>
public sealed record UpdateTitleDescriptionRequest(string? Title, string? Description);

/// <summary>
/// Request body for updating a work item's iteration path (sprint assignment).
/// </summary>
public sealed record UpdateIterationPathRequest(string IterationPath);
