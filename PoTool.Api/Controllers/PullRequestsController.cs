using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Queries;
namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for pull request operations.
/// Uses Mediator pattern for clean separation of concerns.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PullRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PullRequestsController> _logger;

    public PullRequestsController(
        IMediator mediator,
        ILogger<PullRequestsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets all cached pull requests.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PullRequestDto>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var pullRequests = await _mediator.Send(new GetAllPullRequestsQuery(), cancellationToken);
            return Ok(pullRequests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pull requests");
            return StatusCode(500, "Error retrieving pull requests");
        }
    }

    /// <summary>
    /// Gets a specific pull request by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PullRequestDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var pullRequest = await _mediator.Send(new GetPullRequestByIdQuery(id), cancellationToken);

            if (pullRequest == null)
            {
                return NotFound();
            }

            return Ok(pullRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pull request with ID: {PullRequestId}", id);
            return StatusCode(500, "Error retrieving pull request");
        }
    }

    /// <summary>
    /// Gets cached pull requests linked to a specific work item.
    /// </summary>
    [HttpGet("by-workitem/{workItemId:int}")]
    public async Task<ActionResult<IEnumerable<PullRequestDto>>> GetByWorkItemId(
        int workItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            var pullRequests = await _mediator.Send(new GetPullRequestsByWorkItemIdQuery(workItemId), cancellationToken);
            return Ok(pullRequests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pull requests for work item ID: {WorkItemId}", workItemId);
            return StatusCode(500, "Error retrieving pull requests for work item");
        }
    }

    /// <summary>
    /// Gets aggregated metrics for pull requests.
    /// </summary>
    /// <param name="productIds">Optional comma-separated list of product IDs to filter by</param>
    /// <param name="fromDate">Optional start date filter (ISO 8601 format). Defaults to 6 months ago if not specified.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("metrics")]
    public async Task<ActionResult<IEnumerable<PullRequestMetricsDto>>> GetMetrics(
        [FromQuery] string? productIds = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var productIdsList = ParseProductIds(productIds, out var errorMessage);
            if (errorMessage != null)
            {
                return BadRequest(errorMessage);
            }

            var metrics = await _mediator.Send(new GetPullRequestMetricsQuery(productIdsList, fromDate), cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pull request metrics");
            return StatusCode(500, "Error retrieving pull request metrics");
        }
    }

    /// <summary>
    /// Gets filtered pull requests based on query parameters including product scope.
    /// </summary>
    /// <param name="productIds">Optional comma-separated list of product IDs to filter by</param>
    /// <param name="iterationPath">Optional iteration path filter</param>
    /// <param name="createdBy">Optional creator filter</param>
    /// <param name="fromDate">Optional start date filter</param>
    /// <param name="toDate">Optional end date filter</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("filter")]
    public async Task<ActionResult<IEnumerable<PullRequestDto>>> GetFiltered(
        [FromQuery] string? productIds = null,
        [FromQuery] string? iterationPath = null,
        [FromQuery] string? createdBy = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var productIdsList = ParseProductIds(productIds, out var errorMessage);
            if (errorMessage != null)
            {
                return BadRequest(errorMessage);
            }

            var query = new GetFilteredPullRequestsQuery(productIdsList, iterationPath, createdBy, fromDate, toDate, status);
            var pullRequests = await _mediator.Send(query, cancellationToken);
            return Ok(pullRequests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering pull requests");
            return StatusCode(500, "Error filtering pull requests");
        }
    }

    /// <summary>
    /// Gets iterations for a specific pull request.
    /// </summary>
    [HttpGet("{id:int}/iterations")]
    public async Task<ActionResult<IEnumerable<PullRequestIterationDto>>> GetIterations(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var iterations = await _mediator.Send(new GetPullRequestIterationsQuery(id), cancellationToken);
            return Ok(iterations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving iterations for pull request ID: {PullRequestId}", id);
            return StatusCode(500, "Error retrieving pull request iterations");
        }
    }

    /// <summary>
    /// Gets comments for a specific pull request.
    /// </summary>
    [HttpGet("{id:int}/comments")]
    public async Task<ActionResult<IEnumerable<PullRequestCommentDto>>> GetComments(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var comments = await _mediator.Send(new GetPullRequestCommentsQuery(id), cancellationToken);
            return Ok(comments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving comments for pull request ID: {PullRequestId}", id);
            return StatusCode(500, "Error retrieving pull request comments");
        }
    }

    /// <summary>
    /// Gets file changes for a specific pull request.
    /// </summary>
    [HttpGet("{id:int}/filechanges")]
    public async Task<ActionResult<IEnumerable<PullRequestFileChangeDto>>> GetFileChanges(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var fileChanges = await _mediator.Send(new GetPullRequestFileChangesQuery(id), cancellationToken);
            return Ok(fileChanges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file changes for pull request ID: {PullRequestId}", id);
            return StatusCode(500, "Error retrieving pull request file changes");
        }
    }

    /// <summary>
    /// Gets per-sprint PR trend metrics for a set of sprint IDs.
    /// Sprint mapping rule: a PR belongs to a sprint if its CreatedDate falls within [SprintStart, SprintEnd).
    /// </summary>
    /// <param name="sprintIds">Ordered list of sprint IDs defining the trend horizon.</param>
    /// <param name="productIds">Optional comma-separated product IDs to scope PRs.</param>
    /// <param name="teamId">Optional team ID; resolved to linked products when productIds is null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("sprint-trends")]
    public async Task<ActionResult<GetPrSprintTrendsResponse>> GetSprintTrends(
        [FromQuery] List<int> sprintIds,
        [FromQuery] string? productIds = null,
        [FromQuery] int? teamId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sprintIds.Count == 0)
            {
                return BadRequest("At least one sprint ID is required.");
            }

            var productIdsList = ParseProductIds(productIds, out var errorMessage);
            if (errorMessage != null)
            {
                return BadRequest(errorMessage);
            }

            var result = await _mediator.Send(
                new GetPrSprintTrendsQuery(sprintIds, productIdsList, teamId),
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PR sprint trends");
            return StatusCode(500, "Error retrieving PR sprint trends");
        }
    }

    /// <summary>
    /// Gets PR review bottleneck analysis showing reviewer performance and bottlenecks.
    /// </summary>
    /// <param name="maxPRs">Maximum number of PRs to analyze (default: 100)</param>
    /// <param name="daysBack">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PR review bottleneck analysis</returns>
    [HttpGet("review-bottleneck")]
    public async Task<ActionResult<PRReviewBottleneckDto>> GetReviewBottleneck(
        [FromQuery] int maxPRs = 100,
        [FromQuery] int daysBack = 30,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maxPRs < 1 || maxPRs > 500)
            {
                return BadRequest("MaxPRs must be between 1 and 500");
            }

            if (daysBack < 1 || daysBack > 365)
            {
                return BadRequest("DaysBack must be between 1 and 365");
            }

            var analysis = await _mediator.Send(
                new GetPRReviewBottleneckQuery(maxPRs, daysBack),
                cancellationToken);

            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PR review bottleneck analysis");
            return StatusCode(500, "Error retrieving PR review bottleneck analysis");
        }
    }

    /// <summary>
    /// Gets PR Insights for a team over a date range.
    /// All data comes from the local cache — no live TFS calls.
    /// </summary>
    /// <param name="teamId">Optional team ID. When omitted, all PRs in range are returned.</param>
    /// <param name="fromDate">Start of the date range. Defaults to 6 months ago.</param>
    /// <param name="toDate">End of the date range. Defaults to now.</param>
    /// <param name="repositoryName">Optional repository filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("insights")]
    public async Task<ActionResult<PullRequestInsightsDto>> GetInsights(
        [FromQuery] int? teamId = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] string? repositoryName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var from = fromDate ?? DateTimeOffset.UtcNow.AddMonths(-6);
            var to   = toDate   ?? DateTimeOffset.UtcNow;

            var result = await _mediator.Send(
                new GetPullRequestInsightsQuery(teamId, from, to, repositoryName),
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PR insights");
            return StatusCode(500, "Error retrieving PR insights");
        }
    }

    /// <summary>
    /// Gets PR Delivery Insights for a team, classifying PRs by linked work items and
    /// aggregating metrics at Epic and Feature level.
    /// All data comes from the local cache — no live TFS calls.
    /// </summary>
    /// <param name="teamId">Optional team ID. When omitted, PRs across all teams are included.</param>
    /// <param name="sprintId">Optional sprint ID. When provided, date range is derived from sprint boundaries.</param>
    /// <param name="fromDate">Start of the date range. Defaults to 6 months ago unless sprintId is supplied.</param>
    /// <param name="toDate">End of the date range. Defaults to now unless sprintId is supplied.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("delivery-insights")]
    public async Task<ActionResult<PrDeliveryInsightsDto>> GetDeliveryInsights(
        [FromQuery] int? teamId = null,
        [FromQuery] int? sprintId = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var from = fromDate ?? DateTimeOffset.UtcNow.AddMonths(-6);
            var to   = toDate   ?? DateTimeOffset.UtcNow;

            var result = await _mediator.Send(
                new GetPrDeliveryInsightsQuery(teamId, sprintId, from, to),
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PR delivery insights");
            return StatusCode(500, "Error retrieving PR delivery insights");
        }
    }

    /// <summary>
    /// Helper method to parse comma-separated product IDs from query string.
    /// </summary>
    private static List<int>? ParseProductIds(string? productIds, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(productIds))
        {
            return null;
        }

        var result = new List<int>();
        var parts = productIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (!int.TryParse(part.Trim(), out var id))
            {
                errorMessage = $"Invalid product ID format: '{part}'. Expected comma-separated integers.";
                return null;
            }
            result.Add(id);
        }

        return result;
    }
}
