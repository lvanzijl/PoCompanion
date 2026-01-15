using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.PullRequests;
using PoTool.Core.PullRequests.Commands;
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
    /// Gets aggregated metrics for all pull requests.
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<IEnumerable<PullRequestMetricsDto>>> GetMetrics(CancellationToken cancellationToken)
    {
        try
        {
            var metrics = await _mediator.Send(new GetPullRequestMetricsQuery(), cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pull request metrics");
            return StatusCode(500, "Error retrieving pull request metrics");
        }
    }

    /// <summary>
    /// Gets filtered pull requests based on query parameters.
    /// </summary>
    [HttpGet("filter")]
    public async Task<ActionResult<IEnumerable<PullRequestDto>>> GetFiltered(
        [FromQuery] string? iterationPath = null,
        [FromQuery] string? createdBy = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetFilteredPullRequestsQuery(null, iterationPath, createdBy, fromDate, toDate, status);
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
    /// Synchronizes pull requests from TFS/Azure DevOps to local cache.
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<int>> Sync(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _mediator.Send(new SyncPullRequestsCommand(), cancellationToken);
            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing pull requests");
            return StatusCode(500, "Error syncing pull requests");
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
}
