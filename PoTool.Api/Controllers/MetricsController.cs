using System.ComponentModel.DataAnnotations;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Metrics;
using PoTool.Core.Metrics.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for metrics and velocity operations.
/// Provides read-only access to sprint and velocity data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMediator mediator,
        ILogger<MetricsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets metrics for a specific sprint.
    /// </summary>
    /// <param name="iterationPath">The iteration path of the sprint (e.g., "ProjectName\2025\Sprint 1")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sprint metrics or 404 if no work items found for the sprint</returns>
    [HttpGet("sprint")]
    public async Task<ActionResult<SprintMetricsDto>> GetSprintMetrics(
        [FromQuery][Required] string iterationPath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(iterationPath))
            {
                return BadRequest("Iteration path is required");
            }

            var metrics = await _mediator.Send(
                new GetSprintMetricsQuery(iterationPath), 
                cancellationToken);

            if (metrics == null)
            {
                return NotFound($"No work items found for iteration path: {iterationPath}");
            }

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sprint metrics for iteration: {IterationPath}", iterationPath);
            return StatusCode(500, "Error retrieving sprint metrics");
        }
    }

    /// <summary>
    /// Gets velocity trend data across multiple sprints.
    /// </summary>
    /// <param name="areaPath">Optional area path to filter work items</param>
    /// <param name="maxSprints">Maximum number of sprints to include (default: 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Velocity trend data including sprint metrics and averages</returns>
    [HttpGet("velocity")]
    public async Task<ActionResult<VelocityTrendDto>> GetVelocityTrend(
        [FromQuery] string? areaPath = null,
        [FromQuery] int maxSprints = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maxSprints < 1 || maxSprints > 50)
            {
                return BadRequest("MaxSprints must be between 1 and 50");
            }

            var velocityTrend = await _mediator.Send(
                new GetVelocityTrendQuery(areaPath, maxSprints), 
                cancellationToken);

            return Ok(velocityTrend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving velocity trend for area: {AreaPath}", areaPath ?? "All");
            return StatusCode(500, "Error retrieving velocity trend");
        }
    }

    /// <summary>
    /// Gets backlog health metrics for a specific iteration.
    /// </summary>
    /// <param name="iterationPath">The iteration path of the sprint</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backlog health metrics or 404 if no work items found</returns>
    [HttpGet("backlog-health")]
    public async Task<ActionResult<BacklogHealthDto>> GetBacklogHealth(
        [FromQuery][Required] string iterationPath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(iterationPath))
            {
                return BadRequest("Iteration path is required");
            }

            var health = await _mediator.Send(
                new GetBacklogHealthQuery(iterationPath), 
                cancellationToken);

            if (health == null)
            {
                return NotFound($"No work items found for iteration path: {iterationPath}");
            }

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backlog health for iteration: {IterationPath}", iterationPath);
            return StatusCode(500, "Error retrieving backlog health");
        }
    }

    /// <summary>
    /// Gets aggregated backlog health across multiple iterations with trend analysis.
    /// </summary>
    /// <param name="areaPath">Optional area path to filter work items</param>
    /// <param name="maxIterations">Maximum number of iterations to include (default: 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Multi-iteration backlog health with trend analysis</returns>
    [HttpGet("multi-iteration-health")]
    public async Task<ActionResult<MultiIterationBacklogHealthDto>> GetMultiIterationBacklogHealth(
        [FromQuery] string? areaPath = null,
        [FromQuery] int maxIterations = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maxIterations < 1 || maxIterations > 20)
            {
                return BadRequest("MaxIterations must be between 1 and 20");
            }

            var health = await _mediator.Send(
                new GetMultiIterationBacklogHealthQuery(areaPath, maxIterations), 
                cancellationToken);

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving multi-iteration backlog health for area: {AreaPath}", areaPath ?? "All");
            return StatusCode(500, "Error retrieving multi-iteration backlog health");
        }
    }

    /// <summary>
    /// Gets effort distribution heat map data across area paths and iterations.
    /// </summary>
    /// <param name="areaPathFilter">Optional area path filter</param>
    /// <param name="maxIterations">Maximum number of iterations to include (default: 10)</param>
    /// <param name="defaultCapacity">Default capacity per iteration for utilization calculations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Effort distribution heat map data</returns>
    [HttpGet("effort-distribution")]
    public async Task<ActionResult<EffortDistributionDto>> GetEffortDistribution(
        [FromQuery] string? areaPathFilter = null,
        [FromQuery] int maxIterations = 10,
        [FromQuery] int? defaultCapacity = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maxIterations < 1 || maxIterations > 20)
            {
                return BadRequest("MaxIterations must be between 1 and 20");
            }

            if (defaultCapacity.HasValue && defaultCapacity.Value < 0)
            {
                return BadRequest("DefaultCapacity must be non-negative");
            }

            var distribution = await _mediator.Send(
                new GetEffortDistributionQuery(areaPathFilter, maxIterations, defaultCapacity), 
                cancellationToken);

            return Ok(distribution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving effort distribution for area: {AreaPath}", areaPathFilter ?? "All");
            return StatusCode(500, "Error retrieving effort distribution");
        }
    }

    /// <summary>
    /// Gets sprint capacity planning analysis for a specific iteration.
    /// </summary>
    /// <param name="iterationPath">The iteration path of the sprint</param>
    /// <param name="defaultCapacity">Default capacity per person (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sprint capacity plan or 404 if no work items found</returns>
    [HttpGet("capacity-plan")]
    public async Task<ActionResult<SprintCapacityPlanDto>> GetSprintCapacityPlan(
        [FromQuery][Required] string iterationPath,
        [FromQuery] int? defaultCapacity = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(iterationPath))
            {
                return BadRequest("Iteration path is required");
            }

            if (defaultCapacity.HasValue && defaultCapacity.Value < 0)
            {
                return BadRequest("DefaultCapacity must be non-negative");
            }

            var plan = await _mediator.Send(
                new GetSprintCapacityPlanQuery(iterationPath, defaultCapacity), 
                cancellationToken);

            if (plan == null)
            {
                return NotFound($"No work items found for iteration path: {iterationPath}");
            }

            return Ok(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sprint capacity plan for iteration: {IterationPath}", iterationPath);
            return StatusCode(500, "Error retrieving sprint capacity plan");
        }
    }
}
