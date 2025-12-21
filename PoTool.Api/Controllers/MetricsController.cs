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
}
