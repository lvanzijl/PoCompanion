using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Services;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines;
using PoTool.Core.Pipelines.Queries;

namespace PoTool.Api.Controllers;

/// <summary>
/// API controller for pipeline operations.
/// Provides endpoints for pipeline insights and metrics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PipelinesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly PipelineFilterResolutionService _filterResolutionService;
    private readonly ILogger<PipelinesController> _logger;

    public PipelinesController(
        IMediator mediator,
        PipelineFilterResolutionService filterResolutionService,
        ILogger<PipelinesController> logger)
    {
        _mediator = mediator;
        _filterResolutionService = filterResolutionService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all cached pipelines.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PipelineDto>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var pipelines = await _mediator.Send(new GetAllPipelinesQuery(), cancellationToken);
            return Ok(pipelines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pipelines");
            return StatusCode(500, "Error retrieving pipelines");
        }
    }

    /// <summary>
    /// Gets pipeline runs for a specific pipeline.
    /// </summary>
    [HttpGet("{id:int}/runs")]
    public async Task<ActionResult<IEnumerable<PipelineRunDto>>> GetRuns(
        int id,
        [FromQuery] int top = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var runs = await _mediator.Send(new GetPipelineRunsQuery(id, top), cancellationToken);
            return Ok(runs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving runs for pipeline {PipelineId}", id);
            return StatusCode(500, "Error retrieving pipeline runs");
        }
    }

    /// <summary>
    /// Gets aggregated metrics for pipelines, optionally filtered by products.
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<PipelineQueryResponseDto<IReadOnlyList<PipelineMetricsDto>>>> GetMetrics(
        [FromQuery] string? productIds = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var productIdsList = ParseProductIds(productIds, out var errorMessage);
            if (errorMessage != null)
            {
                return BadRequest(errorMessage);
            }

            var resolution = await _filterResolutionService.ResolveAsync(
                new PipelineFilterBoundaryRequest(
                    ProductIds: productIdsList,
                    RangeStartUtc: fromDate,
                    RangeEndUtc: toDate),
                nameof(GetMetrics),
                cancellationToken);

            var metrics = (await _mediator.Send(
                    new GetPipelineMetricsQuery(resolution.EffectiveFilter),
                    cancellationToken))
                .ToList();

            return Ok(PipelineFilterResolutionService.ToResponse<IReadOnlyList<PipelineMetricsDto>>(metrics, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pipeline metrics");
            return StatusCode(500, "Error retrieving pipeline metrics");
        }
    }

    /// <summary>
    /// Gets all pipeline runs for specified products using canonical filter scope.
    /// </summary>
    [HttpGet("runs")]
    public async Task<ActionResult<PipelineQueryResponseDto<IReadOnlyList<PipelineRunDto>>>> GetRunsForProducts(
        [FromQuery] string? productIds = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var productIdsList = ParseProductIds(productIds, out var errorMessage);
            if (errorMessage != null)
            {
                return BadRequest(errorMessage);
            }

            var resolution = await _filterResolutionService.ResolveAsync(
                new PipelineFilterBoundaryRequest(
                    ProductIds: productIdsList,
                    RangeStartUtc: fromDate,
                    RangeEndUtc: toDate),
                nameof(GetRunsForProducts),
                cancellationToken);

            var runs = (await _mediator.Send(
                    new GetPipelineRunsForProductsQuery(resolution.EffectiveFilter),
                    cancellationToken))
                .ToList();

            return Ok(PipelineFilterResolutionService.ToResponse<IReadOnlyList<PipelineRunDto>>(runs, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pipeline runs for products");
            return StatusCode(500, "Error retrieving pipeline runs");
        }
    }

    /// <summary>
    /// Gets pipeline definitions for configuration/discovery.
    /// This endpoint remains live-allowed and is intentionally separate from cache-only analytical reads.
    /// </summary>
    [HttpGet("definitions")]
    public async Task<ActionResult<IEnumerable<PipelineDefinitionDto>>> GetDefinitions(
        [FromQuery] int? productId = null,
        [FromQuery] int? repositoryId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetPipelineDefinitionsQuery(productId, repositoryId);
            var definitions = await _mediator.Send(query, cancellationToken);
            return Ok(definitions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pipeline definitions");
            return StatusCode(500, "Error retrieving pipeline definitions");
        }
    }

    /// <summary>
    /// Gets pipeline health insights for a single sprint per product.
    /// All data comes from the local cache — no TFS calls are made.
    /// Phase 1: aggregation, top-3 in trouble per product and globally.
    /// </summary>
    [HttpGet("insights")]
    public async Task<ActionResult<PipelineQueryResponseDto<PipelineInsightsDto>>> GetInsights(
        [FromQuery] int productOwnerId,
        [FromQuery] int sprintId,
        [FromQuery] bool includePartiallySucceeded = true,
        [FromQuery] bool includeCanceled = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolution = await _filterResolutionService.ResolveAsync(
                new PipelineFilterBoundaryRequest(
                    ProductOwnerId: productOwnerId,
                    SprintId: sprintId),
                nameof(GetInsights),
                cancellationToken);

            var result = await _mediator.Send(
                new GetPipelineInsightsQuery(
                    resolution.EffectiveFilter,
                    includePartiallySucceeded,
                    includeCanceled,
                    sprintId),
                cancellationToken);
            return Ok(PipelineFilterResolutionService.ToResponse(result, resolution));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pipeline insights for sprint {SprintId}", sprintId);
            return StatusCode(500, "Error retrieving pipeline insights");
        }
    }

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
