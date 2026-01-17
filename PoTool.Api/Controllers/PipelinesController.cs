using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines;
using PoTool.Core.Pipelines.Commands;
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
    private readonly ILogger<PipelinesController> _logger;

    public PipelinesController(
        IMediator mediator,
        ILogger<PipelinesController> logger)
    {
        _mediator = mediator;
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
    public async Task<ActionResult<IEnumerable<PipelineMetricsDto>>> GetMetrics(
        [FromQuery] string? productIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse productIds string to list
            List<int>? productIdsList = null;
            if (!string.IsNullOrWhiteSpace(productIds))
            {
                productIdsList = productIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => int.TryParse(id.Trim(), out var parsedId) ? parsedId : 0)
                    .Where(id => id > 0)
                    .ToList();
            }

            var metrics = await _mediator.Send(new GetPipelineMetricsQuery(productIdsList), cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pipeline metrics");
            return StatusCode(500, "Error retrieving pipeline metrics");
        }
    }

    /// <summary>
    /// Gets all pipeline definitions.
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
}
