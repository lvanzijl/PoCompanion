using System.ComponentModel.DataAnnotations;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Services;
using PoTool.Shared.DataState;
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
    private readonly CacheStateResponseService _cacheStateResponseService;
    private readonly DeliveryFilterResolutionService _deliveryFilterResolutionService;
    private readonly SprintFilterResolutionService _sprintFilterResolutionService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMediator mediator,
        CacheStateResponseService cacheStateResponseService,
        DeliveryFilterResolutionService deliveryFilterResolutionService,
        SprintFilterResolutionService sprintFilterResolutionService,
        ILogger<MetricsController> logger)
    {
        _mediator = mediator;
        _cacheStateResponseService = cacheStateResponseService;
        _deliveryFilterResolutionService = deliveryFilterResolutionService;
        _sprintFilterResolutionService = sprintFilterResolutionService;
        _logger = logger;
    }

    private static PortfolioReadQueryOptions BuildPortfolioReadOptions(
        int? productId,
        string? projectNumber,
        string? workPackage,
        PortfolioLifecycleState? lifecycleState,
        PortfolioReadSortBy sortBy,
        PortfolioReadSortDirection sortDirection,
        PortfolioReadGroupBy groupBy,
        int snapshotCount = 6,
        DateTimeOffset? rangeStartUtc = null,
        DateTimeOffset? rangeEndUtc = null,
        bool includeArchivedSnapshots = false,
        long? compareToSnapshotId = null)
        => new(
            productId,
            projectNumber,
            workPackage,
            lifecycleState,
            sortBy,
            sortDirection,
            groupBy,
            snapshotCount,
            rangeStartUtc,
            rangeEndUtc,
            includeArchivedSnapshots,
            compareToSnapshotId);

    /// <summary>
    /// Gets historical metrics for a specific sprint window.
    /// </summary>
    /// <param name="iterationPath">The iteration path of the sprint (e.g., "ProjectName\2025\Sprint 1")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sprint metrics, or 404 when the sprint path has no dated sprint metadata</returns>
    [HttpGet("sprint")]
    public async Task<ActionResult<SprintQueryResponseDto<SprintMetricsDto>>> GetSprintMetrics(
        [FromQuery] string? iterationPath = null,
        [FromQuery] int? productOwnerId = null,
        [FromQuery] int[]? productIds = null,
        [FromQuery] int? sprintId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(iterationPath) && !sprintId.HasValue)
            {
                return BadRequest("Iteration path or sprintId is required");
            }

            var resolution = await _sprintFilterResolutionService.ResolveAsync(
                new SprintFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    ProductIds: productIds,
                    IterationPath: iterationPath,
                    SprintId: sprintId),
                nameof(GetSprintMetrics),
                cancellationToken);

            var metrics = await _mediator.Send(
                new GetSprintMetricsQuery(resolution.EffectiveFilter),
                cancellationToken);

            if (metrics == null)
            {
                var emptyMetrics = CreateEmptySprintMetricsDto(resolution, iterationPath, sprintId);
                return Ok(SprintFilterResolutionService.ToResponse(emptyMetrics, resolution));
            }

            return Ok(SprintFilterResolutionService.ToResponse(metrics, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sprint metrics for iteration: {IterationPath}", iterationPath);
            return StatusCode(500, "Error retrieving sprint metrics");
        }
    }

    /// <summary>
    /// Gets backlog health metrics for a specific iteration.
    /// </summary>
    /// <param name="iterationPath">The iteration path of the sprint</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backlog health metrics or 404 if no work items found</returns>
    [HttpGet("backlog-health")]
    public async Task<ActionResult<SprintQueryResponseDto<BacklogHealthDto>>> GetBacklogHealth(
        [FromQuery] string? iterationPath = null,
        [FromQuery] int? productOwnerId = null,
        [FromQuery] int[]? productIds = null,
        [FromQuery] int? sprintId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(iterationPath) && !sprintId.HasValue)
            {
                return BadRequest("Iteration path or sprintId is required");
            }

            var resolution = await _sprintFilterResolutionService.ResolveAsync(
                new SprintFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    ProductIds: productIds,
                    IterationPath: iterationPath,
                    SprintId: sprintId),
                nameof(GetBacklogHealth),
                cancellationToken);

            var health = await _mediator.Send(
                new GetBacklogHealthQuery(resolution.EffectiveFilter),
                cancellationToken);

            if (health == null)
            {
                var emptyHealth = CreateEmptyBacklogHealthDto(resolution, iterationPath, sprintId);
                return Ok(SprintFilterResolutionService.ToResponse(emptyHealth, resolution));
            }

            return Ok(SprintFilterResolutionService.ToResponse(health, resolution));
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
    public async Task<ActionResult<SprintQueryResponseDto<MultiIterationBacklogHealthDto>>> GetMultiIterationBacklogHealth(
        [FromQuery] int? productOwnerId = null,
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

            var resolution = await _sprintFilterResolutionService.ResolveAsync(
                new SprintFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    ProductIds: productIds,
                    AreaPath: areaPath),
                nameof(GetMultiIterationBacklogHealth),
                cancellationToken);

            var health = await _mediator.Send(
                new GetMultiIterationBacklogHealthQuery(resolution.EffectiveFilter, maxIterations),
                cancellationToken);

            return Ok(SprintFilterResolutionService.ToResponse(health, resolution));
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
    public async Task<ActionResult<SprintQueryResponseDto<SprintCapacityPlanDto>>> GetSprintCapacityPlan(
        [FromQuery] string? iterationPath = null,
        [FromQuery] int? productOwnerId = null,
        [FromQuery] int[]? productIds = null,
        [FromQuery] int? sprintId = null,
        [FromQuery] int? defaultCapacity = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(iterationPath) && !sprintId.HasValue)
            {
                return BadRequest("Iteration path or sprintId is required");
            }

            if (defaultCapacity.HasValue && defaultCapacity.Value < 0)
            {
                return BadRequest("DefaultCapacity must be non-negative");
            }

            var resolution = await _sprintFilterResolutionService.ResolveAsync(
                new SprintFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    ProductIds: productIds,
                    IterationPath: iterationPath,
                    SprintId: sprintId),
                nameof(GetSprintCapacityPlan),
                cancellationToken);

            var plan = await _mediator.Send(
                new GetSprintCapacityPlanQuery(resolution.EffectiveFilter, defaultCapacity),
                cancellationToken);

            if (plan == null)
            {
                return NotFound($"No work items found for sprint scope: {iterationPath ?? sprintId?.ToString() ?? "unknown"}");
            }

            return Ok(SprintFilterResolutionService.ToResponse(plan, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sprint capacity plan for iteration: {IterationPath}", iterationPath);
            return StatusCode(500, "Error retrieving sprint capacity plan");
        }
    }

    /// <summary>
    /// Gets the latest available read-only portfolio progress snapshot.
    /// </summary>
    [HttpGet("/api/portfolio/progress")]
    public async Task<ActionResult<PortfolioProgressDto>> GetPortfolioProgress(
        [FromQuery][Required] int productOwnerId,
        [FromQuery] int? productId = null,
        [FromQuery] string? projectNumber = null,
        [FromQuery] string? workPackage = null,
        [FromQuery] PortfolioLifecycleState? lifecycleState = null,
        [FromQuery] PortfolioReadSortBy sortBy = PortfolioReadSortBy.Default,
        [FromQuery] PortfolioReadSortDirection sortDirection = PortfolioReadSortDirection.Desc,
        [FromQuery] PortfolioReadGroupBy groupBy = PortfolioReadGroupBy.None,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var progress = await _mediator.Send(
                new GetPortfolioProgressQuery(
                    productOwnerId,
                    BuildPortfolioReadOptions(productId, projectNumber, workPackage, lifecycleState, sortBy, sortDirection, groupBy)),
                cancellationToken);

            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving portfolio progress for ProductOwner {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving portfolio progress");
        }
    }

    /// <summary>
    /// Gets the latest available read-only portfolio snapshot.
    /// </summary>
    [HttpGet("/api/portfolio/snapshots")]
    public async Task<ActionResult<PortfolioSnapshotDto>> GetPortfolioSnapshots(
        [FromQuery][Required] int productOwnerId,
        [FromQuery] int? productId = null,
        [FromQuery] string? projectNumber = null,
        [FromQuery] string? workPackage = null,
        [FromQuery] PortfolioLifecycleState? lifecycleState = null,
        [FromQuery] PortfolioReadSortBy sortBy = PortfolioReadSortBy.Default,
        [FromQuery] PortfolioReadSortDirection sortDirection = PortfolioReadSortDirection.Desc,
        [FromQuery] PortfolioReadGroupBy groupBy = PortfolioReadGroupBy.None,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await _mediator.Send(
                new GetPortfolioSnapshotsQuery(
                    productOwnerId,
                    BuildPortfolioReadOptions(productId, projectNumber, workPackage, lifecycleState, sortBy, sortDirection, groupBy)),
                cancellationToken);

            return Ok(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving portfolio snapshots for ProductOwner {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving portfolio snapshots");
        }
    }

    /// <summary>
    /// Gets the read-only comparison between the latest snapshot and either the previous snapshot or an explicitly selected earlier snapshot.
    /// </summary>
    [HttpGet("/api/portfolio/comparison")]
    public async Task<ActionResult<PortfolioComparisonDto>> GetPortfolioComparison(
        [FromQuery][Required] int productOwnerId,
        [FromQuery] int? productId = null,
        [FromQuery] string? projectNumber = null,
        [FromQuery] string? workPackage = null,
        [FromQuery] PortfolioLifecycleState? lifecycleState = null,
        [FromQuery] PortfolioReadSortBy sortBy = PortfolioReadSortBy.Default,
        [FromQuery] PortfolioReadSortDirection sortDirection = PortfolioReadSortDirection.Desc,
        [FromQuery] PortfolioReadGroupBy groupBy = PortfolioReadGroupBy.None,
        [FromQuery] DateTimeOffset? rangeStartUtc = null,
        [FromQuery] DateTimeOffset? rangeEndUtc = null,
        [FromQuery] bool includeArchivedSnapshots = false,
        [FromQuery] long? compareToSnapshotId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var comparison = await _mediator.Send(
                new GetPortfolioComparisonQuery(
                    productOwnerId,
                    BuildPortfolioReadOptions(
                        productId,
                        projectNumber,
                        workPackage,
                        lifecycleState,
                        sortBy,
                        sortDirection,
                        groupBy,
                        rangeStartUtc: rangeStartUtc,
                        rangeEndUtc: rangeEndUtc,
                        includeArchivedSnapshots: includeArchivedSnapshots,
                        compareToSnapshotId: compareToSnapshotId)),
                cancellationToken);

            return Ok(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving portfolio comparison for ProductOwner {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving portfolio comparison");
        }
    }

    /// <summary>
    /// Gets persisted multi-snapshot portfolio trend history.
    /// </summary>
    [HttpGet("/api/portfolio/trends")]
    public async Task<ActionResult<PortfolioTrendDto>> GetPortfolioTrends(
        [FromQuery][Required] int productOwnerId,
        [FromQuery] int? productId = null,
        [FromQuery] string? projectNumber = null,
        [FromQuery] string? workPackage = null,
        [FromQuery] PortfolioLifecycleState? lifecycleState = null,
        [FromQuery] PortfolioReadSortBy sortBy = PortfolioReadSortBy.Default,
        [FromQuery] PortfolioReadSortDirection sortDirection = PortfolioReadSortDirection.Desc,
        [FromQuery] PortfolioReadGroupBy groupBy = PortfolioReadGroupBy.None,
        [FromQuery] int snapshotCount = 6,
        [FromQuery] DateTimeOffset? rangeStartUtc = null,
        [FromQuery] DateTimeOffset? rangeEndUtc = null,
        [FromQuery] bool includeArchivedSnapshots = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var trend = await _mediator.Send(
                new GetPortfolioTrendsQuery(
                    productOwnerId,
                    BuildPortfolioReadOptions(
                        productId,
                        projectNumber,
                        workPackage,
                        lifecycleState,
                        sortBy,
                        sortDirection,
                        groupBy,
                        snapshotCount,
                        rangeStartUtc,
                        rangeEndUtc,
                        includeArchivedSnapshots)),
                cancellationToken);

            return Ok(trend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving portfolio trends for ProductOwner {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving portfolio trends");
        }
    }

    /// <summary>
    /// Gets deterministic decision-support signals derived from persisted portfolio history.
    /// </summary>
    [HttpGet("/api/portfolio/signals")]
    public async Task<ActionResult<PortfolioSignalsDto>> GetPortfolioSignals(
        [FromQuery][Required] int productOwnerId,
        [FromQuery] int? productId = null,
        [FromQuery] string? projectNumber = null,
        [FromQuery] string? workPackage = null,
        [FromQuery] PortfolioLifecycleState? lifecycleState = null,
        [FromQuery] PortfolioReadSortBy sortBy = PortfolioReadSortBy.Default,
        [FromQuery] PortfolioReadSortDirection sortDirection = PortfolioReadSortDirection.Desc,
        [FromQuery] PortfolioReadGroupBy groupBy = PortfolioReadGroupBy.None,
        [FromQuery] int snapshotCount = 6,
        [FromQuery] DateTimeOffset? rangeStartUtc = null,
        [FromQuery] DateTimeOffset? rangeEndUtc = null,
        [FromQuery] bool includeArchivedSnapshots = false,
        [FromQuery] long? compareToSnapshotId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var signals = await _mediator.Send(
                new GetPortfolioSignalsQuery(
                    productOwnerId,
                    BuildPortfolioReadOptions(
                        productId,
                        projectNumber,
                        workPackage,
                        lifecycleState,
                        sortBy,
                        sortDirection,
                        groupBy,
                        snapshotCount,
                        rangeStartUtc,
                        rangeEndUtc,
                        includeArchivedSnapshots,
                        compareToSnapshotId)),
                cancellationToken);

            return Ok(signals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving portfolio signals for ProductOwner {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving portfolio signals");
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
    /// Identifies disproportionate effort-hour allocations and provides rebalancing recommendations.
    /// </summary>
    /// <param name="areaPathFilter">Optional area path filter</param>
    /// <param name="maxIterations">Maximum number of iterations to include (default: 10)</param>
    /// <param name="defaultCapacity">Default capacity per iteration for utilization context in sprint descriptions</param>
    /// <param name="imbalanceThreshold">Base threshold for imbalance detection (default: 0.3 = 30%, with higher bands at 1.5x and 2.5x)</param>
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
    /// Identifies scenarios where effort is concentrated in single areas or iterations.
    /// </summary>
    /// <param name="areaPathFilter">Optional area path filter</param>
    /// <param name="maxIterations">Maximum number of iterations to include (default: 10)</param>
    /// <param name="concentrationThreshold">Legacy compatibility parameter. Stable concentration analysis uses fixed 25/40/60/80 share bands and ignores this value.</param>
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
    public async Task<ActionResult<SprintQueryResponseDto<GetSprintTrendMetricsResponse>>> GetSprintTrendMetrics(
        [FromQuery][Required] int productOwnerId,
        [FromQuery][Required] int[] sprintIds,
        [FromQuery] int[]? productIds = null,
        [FromQuery] bool recompute = false,
        [FromQuery] bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sprintIds.Length == 0)
            {
                return BadRequest("At least one sprint ID is required");
            }

            var resolution = await _sprintFilterResolutionService.ResolveAsync(
                new SprintFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    ProductIds: productIds,
                    SprintIds: sprintIds),
                nameof(GetSprintTrendMetrics),
                cancellationToken);

            var response = await _mediator.Send(
                new GetSprintTrendMetricsQuery(productOwnerId, resolution.EffectiveFilter, recompute, includeDetails),
                cancellationToken);

            if (!response.Success)
            {
                return BadRequest(response.ErrorMessage);
            }

            return Ok(SprintFilterResolutionService.ToResponse(response, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sprint trend metrics for ProductOwner: {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving sprint trend metrics");
        }
    }

    /// <summary>
    /// Gets portfolio progress trend metrics across a selected sprint range.
    /// Returns per-sprint % done, total scope effort, remaining scope effort, and throughput.
    /// The response DTO keeps legacy <c>RemainingEffort</c> naming for API compatibility,
    /// but that field represents remaining scope effort rather than a separate domain concept.
    /// </summary>
    /// <param name="productOwnerId">Product Owner ID</param>
    /// <param name="sprintIds">Sprint IDs to include in the trend (ordered chronologically)</param>
    /// <param name="productIds">Optional product IDs to filter to a specific product (default = all products)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Portfolio progress trend with per-sprint data and summary</returns>
    [HttpGet("portfolio-progress-trend")]
    public async Task<ActionResult<DeliveryQueryResponseDto<PortfolioProgressTrendDto>>> GetPortfolioProgressTrend(
        [FromQuery][Required] int productOwnerId,
        [FromQuery][Required] int[] sprintIds,
        [FromQuery] int[]? productIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sprintIds.Length == 0)
            {
                return BadRequest("At least one sprint ID is required");
            }

            var resolution = await _deliveryFilterResolutionService.ResolveAsync(
                new DeliveryFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    ProductIds: productIds,
                    SprintIds: sprintIds),
                nameof(GetPortfolioProgressTrend),
                cancellationToken);

            var result = await _mediator.Send(
                new GetPortfolioProgressTrendQuery(resolution.EffectiveFilter),
                cancellationToken);

            return Ok(DeliveryFilterResolutionService.ToResponse(result, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving portfolio progress trend for ProductOwner: {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving portfolio progress trend");
        }
    }

    [HttpGet("state/portfolio-progress-trend")]
    public async Task<ActionResult<DataStateResponseDto<DeliveryQueryResponseDto<PortfolioProgressTrendDto>>>> GetPortfolioProgressTrendState(
        [FromQuery][Required] int productOwnerId,
        [FromQuery][Required] int[] sprintIds,
        [FromQuery] int[]? productIds = null,
        CancellationToken cancellationToken = default)
    {
        if (sprintIds.Length == 0)
        {
            return BadRequest("At least one sprint ID is required");
        }

        var response = await _cacheStateResponseService.ExecuteAsync(
            async ct =>
            {
                var resolution = await _deliveryFilterResolutionService.ResolveAsync(
                    new DeliveryFilterBoundaryRequest(productOwnerId, productIds, SprintIds: sprintIds),
                    nameof(GetPortfolioProgressTrendState),
                    ct);

                var result = await _mediator.Send(new GetPortfolioProgressTrendQuery(resolution.EffectiveFilter), ct);
                var trend = result ?? new PortfolioProgressTrendDto
                {
                    Sprints = Array.Empty<PortfolioSprintProgressDto>(),
                    Summary = new PortfolioProgressSummaryDto()
                };

                return DeliveryFilterResolutionService.ToResponse(trend, resolution);
            },
            envelope => envelope?.Data?.Sprints.Count == 0,
            "No portfolio flow trend data is available for the selected sprint range.",
            "Portfolio flow could not be loaded right now.",
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Gets capacity calibration metrics (velocity distribution + predictability) for a selected sprint range.
    /// Returns median/P25/P75 velocity, median predictability, and outlier sprint names.
    /// </summary>
    /// <param name="productOwnerId">Product Owner ID</param>
    /// <param name="sprintIds">Sprint IDs to include (ordered chronologically)</param>
    /// <param name="productIds">Optional product IDs to filter (default = all products)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Capacity calibration DTO</returns>
    [HttpGet("capacity-calibration")]
    public async Task<ActionResult<DeliveryQueryResponseDto<CapacityCalibrationDto>>> GetCapacityCalibration(
        [FromQuery][Required] int productOwnerId,
        [FromQuery][Required] int[] sprintIds,
        [FromQuery] int[]? productIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sprintIds.Length == 0)
            {
                return BadRequest("At least one sprint ID is required");
            }

            var resolution = await _deliveryFilterResolutionService.ResolveAsync(
                new DeliveryFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    ProductIds: productIds,
                    SprintIds: sprintIds),
                nameof(GetCapacityCalibration),
                cancellationToken);

            var result = await _mediator.Send(
                new GetCapacityCalibrationQuery(resolution.EffectiveFilter),
                cancellationToken);

            return Ok(DeliveryFilterResolutionService.ToResponse(result, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving capacity calibration for ProductOwner: {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving capacity calibration");
        }
    }

    /// <summary>
    /// Gets an aggregated portfolio delivery snapshot across products for a selected sprint range.
    /// Returns composition and distribution data — no time-series information.
    /// Delivery workspace: Portfolio Delivery view.
    /// </summary>
    /// <param name="productOwnerId">Product Owner ID</param>
    /// <param name="sprintIds">Sprint IDs to include in the aggregated snapshot</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Portfolio delivery snapshot DTO</returns>
    [HttpGet("portfolio-delivery")]
    public async Task<ActionResult<DeliveryQueryResponseDto<PortfolioDeliveryDto>>> GetPortfolioDelivery(
        [FromQuery][Required] int productOwnerId,
        [FromQuery][Required] int[] sprintIds,
        [FromQuery] int[]? productIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sprintIds.Length == 0)
            {
                return BadRequest("At least one sprint ID is required");
            }

            var resolution = await _deliveryFilterResolutionService.ResolveAsync(
                new DeliveryFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    ProductIds: productIds,
                    SprintIds: sprintIds),
                nameof(GetPortfolioDelivery),
                cancellationToken);

            var result = await _mediator.Send(
                new GetPortfolioDeliveryQuery(resolution.EffectiveFilter),
                cancellationToken);

            return Ok(DeliveryFilterResolutionService.ToResponse(result, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving portfolio delivery for ProductOwner: {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving portfolio delivery");
        }
    }

    /// <summary>
    /// Gets compact contextual metrics for the Home product bar.
    /// </summary>
    [HttpGet("home-product-bar")]
    public async Task<ActionResult<DeliveryQueryResponseDto<HomeProductBarMetricsDto>>> GetHomeProductBarMetrics(
        [FromQuery][Required] int productOwnerId,
        [FromQuery] int? productId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolution = await _deliveryFilterResolutionService.ResolveAsync(
                new DeliveryFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    ProductIds: productId.HasValue ? [productId.Value] : null),
                nameof(GetHomeProductBarMetrics),
                cancellationToken);

            var result = await _mediator.Send(
                new GetHomeProductBarMetricsQuery(productOwnerId, resolution.EffectiveFilter),
                cancellationToken);

            return Ok(DeliveryFilterResolutionService.ToResponse(result, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Home product bar metrics for ProductOwner: {ProductOwnerId}", productOwnerId);
            return StatusCode(500, "Error retrieving Home product bar metrics");
        }
    }

    /// <summary>
    /// Gets sprint execution analysis for internal diagnostics.
    /// Reconstructs sprint backlog evolution: initial scope, added/removed items,
    /// completion order, and starved work detection.
    /// Delivery workspace: Sprint Execution view.
    /// </summary>
    /// <param name="productOwnerId">Product Owner ID</param>
    /// <param name="sprintId">Sprint ID to analyze</param>
    /// <param name="productId">Optional product ID to filter results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sprint execution analysis DTO</returns>
    [HttpGet("sprint-execution")]
    public async Task<ActionResult<SprintQueryResponseDto<SprintExecutionDto>>> GetSprintExecution(
        [FromQuery][Required] int productOwnerId,
        [FromQuery][Required] int sprintId,
        [FromQuery] int? productId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolution = await _sprintFilterResolutionService.ResolveAsync(
                new SprintFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    ProductIds: productId.HasValue ? [productId.Value] : null,
                    SprintId: sprintId),
                nameof(GetSprintExecution),
                cancellationToken);

            var result = await _mediator.Send(
                new GetSprintExecutionQuery(productOwnerId, resolution.EffectiveFilter),
                cancellationToken);

            return Ok(SprintFilterResolutionService.ToResponse(result, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sprint execution for ProductOwner: {ProductOwnerId}, Sprint: {SprintId}", productOwnerId, sprintId);
            return StatusCode(500, "Error retrieving sprint execution");
        }
    }

    /// <summary>
    /// Gets activity details for a work item and its descendants in the selected sprint period.
    /// </summary>
    [HttpGet("work-item-activity/{workItemId:int}")]
    public async Task<ActionResult<SprintQueryResponseDto<WorkItemActivityDetailsDto>>> GetWorkItemActivityDetails(
        int workItemId,
        [FromQuery][Required] int productOwnerId,
        [FromQuery] int? sprintId = null,
        [FromQuery] DateTimeOffset? periodStartUtc = null,
        [FromQuery] DateTimeOffset? periodEndUtc = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolution = await _sprintFilterResolutionService.ResolveAsync(
                new SprintFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    SprintId: sprintId,
                    RangeStartUtc: periodStartUtc,
                    RangeEndUtc: periodEndUtc),
                nameof(GetWorkItemActivityDetails),
                cancellationToken);

            var details = await _mediator.Send(
                new GetWorkItemActivityDetailsQuery(productOwnerId, workItemId, resolution.EffectiveFilter),
                cancellationToken);

            if (details == null)
            {
                return NotFound($"Work item with ID {workItemId} not found for ProductOwner {productOwnerId}");
            }

            return Ok(SprintFilterResolutionService.ToResponse(details, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work item activity details for WorkItemId: {WorkItemId}", workItemId);
            return StatusCode(500, "Error retrieving work item activity details");
        }
    }

    [HttpGet("state/work-item-activity/{workItemId:int}")]
    public async Task<ActionResult<DataStateResponseDto<SprintQueryResponseDto<WorkItemActivityDetailsDto>>>> GetWorkItemActivityDetailsState(
        int workItemId,
        [FromQuery][Required] int productOwnerId,
        [FromQuery] int? sprintId = null,
        [FromQuery] DateTimeOffset? periodStartUtc = null,
        [FromQuery] DateTimeOffset? periodEndUtc = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _cacheStateResponseService.ExecuteAsync<SprintQueryResponseDto<WorkItemActivityDetailsDto>>(
            async ct =>
            {
                var resolution = await _sprintFilterResolutionService.ResolveAsync(
                    new SprintFilterBoundaryRequest(
                        ProductOwnerId: productOwnerId,
                        SprintId: sprintId,
                        RangeStartUtc: periodStartUtc,
                        RangeEndUtc: periodEndUtc),
                    nameof(GetWorkItemActivityDetailsState),
                    ct);

                var details = await _mediator.Send(new GetWorkItemActivityDetailsQuery(productOwnerId, workItemId, resolution.EffectiveFilter), ct)
                    ?? new WorkItemActivityDetailsDto
                    {
                        WorkItemId = workItemId,
                        WorkItemTitle = $"Work item #{workItemId}",
                        WorkItemType = "Work Item",
                        PeriodStartUtc = periodStartUtc,
                        PeriodEndUtc = periodEndUtc,
                        Activities = Array.Empty<WorkItemActivityEntryDto>()
                    };

                return SprintFilterResolutionService.ToResponse(details, resolution);
            },
            envelope => envelope?.Data is null || envelope.Data.Activities.Count == 0,
            "No activity details are available for this work item.",
            "Sprint activity could not be loaded right now.",
            cancellationToken);

        return Ok(response);
    }

    private static SprintMetricsDto CreateEmptySprintMetricsDto(
        SprintFilterResolution resolution,
        string? iterationPath,
        int? sprintId)
    {
        var effectiveIterationPath = resolution.EffectiveFilter.IterationPaths.FirstOrDefault()
            ?? iterationPath
            ?? (sprintId.HasValue ? $"Sprint {sprintId.Value}" : "Unknown sprint");

        return new SprintMetricsDto(
            IterationPath: effectiveIterationPath,
            SprintName: ExtractSprintName(effectiveIterationPath, sprintId),
            StartDate: resolution.EffectiveFilter.RangeStartUtc,
            EndDate: resolution.EffectiveFilter.RangeEndUtc,
            CompletedStoryPoints: 0,
            PlannedStoryPoints: 0,
            CompletedWorkItemCount: 0,
            TotalWorkItemCount: 0,
            CompletedPBIs: 0,
            CompletedBugs: 0,
            CompletedTasks: 0);
    }

    private static BacklogHealthDto CreateEmptyBacklogHealthDto(
        SprintFilterResolution resolution,
        string? iterationPath,
        int? sprintId)
    {
        var effectiveIterationPath = resolution.EffectiveFilter.IterationPaths.FirstOrDefault()
            ?? iterationPath
            ?? (sprintId.HasValue ? $"Sprint {sprintId.Value}" : "Unknown sprint");

        return new BacklogHealthDto(
            IterationPath: effectiveIterationPath,
            SprintName: ExtractSprintName(effectiveIterationPath, sprintId),
            TotalWorkItems: 0,
            WorkItemsWithoutEffort: 0,
            WorkItemsInProgressWithoutEffort: 0,
            ParentProgressIssues: 0,
            BlockedItems: 0,
            InProgressAtIterationEnd: 0,
            IterationStart: resolution.EffectiveFilter.RangeStartUtc,
            IterationEnd: resolution.EffectiveFilter.RangeEndUtc,
            ValidationIssues: Array.Empty<ValidationIssueSummary>());
    }

    private static string ExtractSprintName(string iterationPath, int? sprintId)
    {
        if (!string.IsNullOrWhiteSpace(iterationPath))
        {
            var segments = iterationPath.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length > 0)
            {
                return segments[^1];
            }
        }

        return sprintId.HasValue ? $"Sprint {sprintId.Value}" : "Unknown sprint";
    }
}
