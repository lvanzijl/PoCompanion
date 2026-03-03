using System.ComponentModel.DataAnnotations;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.Metrics;
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
    /// <param name="productIds">Optional product IDs to filter work items (comma-separated for multiple)</param>
    /// <param name="areaPath">Optional area path to filter work items</param>
    /// <param name="maxSprints">Maximum number of sprints to include (default: 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Velocity trend data including sprint metrics and averages</returns>
    [HttpGet("velocity")]
    public async Task<ActionResult<VelocityTrendDto>> GetVelocityTrend(
        [FromQuery] int[]? productIds = null,
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
                new GetVelocityTrendQuery(productIds, areaPath, maxSprints),
                cancellationToken);

            return Ok(velocityTrend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving velocity trend for products: {ProductIds}, area: {AreaPath}", 
                productIds != null ? string.Join(", ", productIds) : "None",
                areaPath ?? "All");
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
    /// <param name="productIds">Optional product IDs to filter work items by product hierarchy (comma-separated for multiple)</param>
    /// <param name="areaPath">Optional area path to filter work items (used if productIds is not specified)</param>
    /// <param name="maxIterations">Maximum number of iterations to include (default: 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Multi-iteration backlog health with trend analysis</returns>
    [HttpGet("multi-iteration-health")]
    public async Task<ActionResult<MultiIterationBacklogHealthDto>> GetMultiIterationBacklogHealth(
        [FromQuery] int[]? productIds = null,
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
                new GetMultiIterationBacklogHealthQuery(productIds, areaPath, maxIterations),
                cancellationToken);

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving multi-iteration backlog health for products: {ProductIds}, area: {AreaPath}", 
                productIds != null ? string.Join(", ", productIds) : "None", 
                areaPath ?? "All");
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

    /// <summary>
    /// Gets completion forecast for an Epic or Feature based on historical velocity.
    /// </summary>
    /// <param name="epicId">The TFS ID of the Epic or Feature</param>
    /// <param name="maxSprintsForVelocity">Maximum number of sprints to use for velocity calculation (default: 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Epic completion forecast or 404 if Epic not found</returns>
    [HttpGet("epic-forecast/{epicId:int}")]
    public async Task<ActionResult<EpicCompletionForecastDto>> GetEpicForecast(
        int epicId,
        [FromQuery] int maxSprintsForVelocity = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maxSprintsForVelocity < 1 || maxSprintsForVelocity > 20)
            {
                return BadRequest("MaxSprintsForVelocity must be between 1 and 20");
            }

            var forecast = await _mediator.Send(
                new GetEpicCompletionForecastQuery(epicId, maxSprintsForVelocity),
                cancellationToken);

            if (forecast == null)
            {
                return NotFound($"Epic with ID {epicId} not found");
            }

            return Ok(forecast);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Epic forecast for ID: {EpicId}", epicId);
            return StatusCode(500, "Error retrieving Epic completion forecast");
        }
    }

    /// <summary>
    /// Gets effort imbalance analysis across teams and sprints.
    /// Identifies disproportionate allocations and provides rebalancing recommendations.
    /// </summary>
    /// <param name="areaPathFilter">Optional area path filter</param>
    /// <param name="maxIterations">Maximum number of iterations to include (default: 10)</param>
    /// <param name="defaultCapacity">Default capacity per iteration for utilization calculations</param>
    /// <param name="imbalanceThreshold">Threshold for imbalance detection (default: 0.3 = 30%)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Effort imbalance analysis with recommendations</returns>
    [HttpGet("effort-imbalance")]
    public async Task<ActionResult<EffortImbalanceDto>> GetEffortImbalance(
        [FromQuery] string? areaPathFilter = null,
        [FromQuery] int maxIterations = 10,
        [FromQuery] int? defaultCapacity = null,
        [FromQuery] double imbalanceThreshold = 0.3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maxIterations < 1 || maxIterations > 20)
            {
                return BadRequest("MaxIterations must be between 1 and 20");
            }

            if (imbalanceThreshold < 0 || imbalanceThreshold > 1)
            {
                return BadRequest("ImbalanceThreshold must be between 0 and 1");
            }

            if (defaultCapacity.HasValue && defaultCapacity.Value < 0)
            {
                return BadRequest("DefaultCapacity must be non-negative");
            }

            var imbalance = await _mediator.Send(
                new GetEffortImbalanceQuery(areaPathFilter, maxIterations, defaultCapacity, imbalanceThreshold),
                cancellationToken);

            return Ok(imbalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving effort imbalance for area: {AreaPath}", areaPathFilter ?? "All");
            return StatusCode(500, "Error retrieving effort imbalance");
        }
    }

    /// <summary>
    /// Gets effort distribution trend analysis over time.
    /// Shows how distribution patterns change sprint by sprint with forecasts.
    /// </summary>
    /// <param name="areaPathFilter">Optional area path filter</param>
    /// <param name="maxIterations">Maximum number of iterations to include (default: 10)</param>
    /// <param name="defaultCapacity">Default capacity per iteration for utilization calculations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Effort distribution trend with forecasts</returns>
    [HttpGet("effort-distribution-trend")]
    public async Task<ActionResult<EffortDistributionTrendDto>> GetEffortDistributionTrend(
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

            var trend = await _mediator.Send(
                new GetEffortDistributionTrendQuery(areaPathFilter, maxIterations, defaultCapacity),
                cancellationToken);

            return Ok(trend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving effort distribution trend for area: {AreaPath}", areaPathFilter ?? "All");
            return StatusCode(500, "Error retrieving effort distribution trend");
        }
    }

    /// <summary>
    /// Gets effort concentration risk analysis.
    /// Identifies scenarios where effort is concentrated in single features or areas.
    /// </summary>
    /// <param name="areaPathFilter">Optional area path filter</param>
    /// <param name="maxIterations">Maximum number of iterations to include (default: 10)</param>
    /// <param name="concentrationThreshold">Threshold for concentration risk (default: 0.5 = 50%)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Concentration risk analysis with mitigation recommendations</returns>
    [HttpGet("effort-concentration-risk")]
    public async Task<ActionResult<EffortConcentrationRiskDto>> GetEffortConcentrationRisk(
        [FromQuery] string? areaPathFilter = null,
        [FromQuery] int maxIterations = 10,
        [FromQuery] double concentrationThreshold = 0.5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maxIterations < 1 || maxIterations > 20)
            {
                return BadRequest("MaxIterations must be between 1 and 20");
            }

            if (concentrationThreshold < 0 || concentrationThreshold > 1)
            {
                return BadRequest("ConcentrationThreshold must be between 0 and 1");
            }

            var risk = await _mediator.Send(
                new GetEffortConcentrationRiskQuery(areaPathFilter, maxIterations, concentrationThreshold),
                cancellationToken);

            return Ok(risk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving effort concentration risk for area: {AreaPath}", areaPathFilter ?? "All");
            return StatusCode(500, "Error retrieving effort concentration risk");
        }
    }

    /// <summary>
    /// Gets intelligent effort estimation suggestions for work items without effort.
    /// Uses ML/heuristic analysis of historical data to suggest appropriate effort values.
    /// </summary>
    /// <param name="iterationPath">Optional iteration path filter</param>
    /// <param name="areaPath">Optional area path filter</param>
    /// <param name="onlyInProgressItems">Only include items in progress state (default: true)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of effort estimation suggestions with rationale and confidence scores</returns>
    [HttpGet("effort-estimation-suggestions")]
    public async Task<ActionResult<IReadOnlyList<EffortEstimationSuggestionDto>>> GetEffortEstimationSuggestions(
        [FromQuery] string? iterationPath = null,
        [FromQuery] string? areaPath = null,
        [FromQuery] bool onlyInProgressItems = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var suggestions = await _mediator.Send(
                new GetEffortEstimationSuggestionsQuery(iterationPath, areaPath, onlyInProgressItems),
                cancellationToken);

            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving effort estimation suggestions");
            return StatusCode(500, "Error retrieving effort estimation suggestions");
        }
    }

    /// <summary>
    /// Gets effort estimation quality metrics.
    /// Analyzes historical estimation accuracy and patterns.
    /// </summary>
    /// <param name="areaPath">Optional area path filter</param>
    /// <param name="maxIterations">Maximum number of iterations to analyze (default: 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Effort estimation quality analysis with trends and recommendations</returns>
    [HttpGet("effort-estimation-quality")]
    public async Task<ActionResult<EffortEstimationQualityDto>> GetEffortEstimationQuality(
        [FromQuery] string? areaPath = null,
        [FromQuery] int maxIterations = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maxIterations < 1 || maxIterations > 20)
            {
                return BadRequest("MaxIterations must be between 1 and 20");
            }

            var quality = await _mediator.Send(
                new GetEffortEstimationQualityQuery(areaPath, maxIterations),
                cancellationToken);

            return Ok(quality);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving effort estimation quality for area: {AreaPath}", areaPath ?? "All");
            return StatusCode(500, "Error retrieving effort estimation quality");
        }
    }

    /// <summary>
    /// Gets sprint trend metrics showing planned vs worked items.
    /// Uses revision-based data for accurate historical tracking.
    /// </summary>
    /// <param name="productOwnerId">Product Owner ID</param>
    /// <param name="sprintIds">Sprint IDs to get metrics for</param>
    /// <param name="recompute">Whether to recompute metrics from revision data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sprint trend metrics</returns>
    [HttpGet("sprint-trend")]
    public async Task<ActionResult<GetSprintTrendMetricsResponse>> GetSprintTrendMetrics(
        [FromQuery][Required] int productOwnerId,
        [FromQuery][Required] int[] sprintIds,
        [FromQuery] bool recompute = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sprintIds.Length == 0)
            {
                return BadRequest("At least one sprint ID is required");
            }

            var response = await _mediator.Send(
                new GetSprintTrendMetricsQuery(productOwnerId, sprintIds, recompute),
                cancellationToken);

            if (!response.Success)
            {
                return BadRequest(response.ErrorMessage);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sprint trend metrics for ProductOwner: {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving sprint trend metrics");
        }
    }

    /// <summary>
    /// Gets portfolio progress trend metrics across a selected sprint range.
    /// Returns per-sprint % done, total scope, remaining effort, and throughput.
    /// </summary>
    /// <param name="productOwnerId">Product Owner ID</param>
    /// <param name="sprintIds">Sprint IDs to include in the trend (ordered chronologically)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Portfolio progress trend with per-sprint data and summary</returns>
    [HttpGet("portfolio-progress-trend")]
    public async Task<ActionResult<PortfolioProgressTrendDto>> GetPortfolioProgressTrend(
        [FromQuery][Required] int productOwnerId,
        [FromQuery][Required] int[] sprintIds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sprintIds.Length == 0)
            {
                return BadRequest("At least one sprint ID is required");
            }

            var result = await _mediator.Send(
                new GetPortfolioProgressTrendQuery(productOwnerId, sprintIds),
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving portfolio progress trend for ProductOwner: {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving portfolio progress trend");
        }
    }

    /// <summary>
    /// Gets activity details for a work item and its descendants in the selected sprint period.
    /// </summary>
    [HttpGet("work-item-activity/{workItemId:int}")]
    public async Task<ActionResult<WorkItemActivityDetailsDto>> GetWorkItemActivityDetails(
        int workItemId,
        [FromQuery][Required] int productOwnerId,
        [FromQuery] DateTimeOffset? periodStartUtc = null,
        [FromQuery] DateTimeOffset? periodEndUtc = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var details = await _mediator.Send(
                new GetWorkItemActivityDetailsQuery(productOwnerId, workItemId, periodStartUtc, periodEndUtc),
                cancellationToken);

            if (details == null)
            {
                return NotFound($"Work item with ID {workItemId} not found for ProductOwner {productOwnerId}");
            }

            return Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work item activity details for WorkItemId: {WorkItemId}", workItemId);
            return StatusCode(500, "Error retrieving work item activity details");
        }
    }
}
