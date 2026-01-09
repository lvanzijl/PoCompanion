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
    /// Gets aggregated metrics for all pipelines.
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<IEnumerable<PipelineMetricsDto>>> GetMetrics(CancellationToken cancellationToken)
    {
        try
        {
            var metrics = await _mediator.Send(new GetPipelineMetricsQuery(), cancellationToken);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pipeline metrics");
            return StatusCode(500, "Error retrieving pipeline metrics");
        }
    }

    /// <summary>
    /// Synchronizes pipelines from TFS/Azure DevOps to local cache.
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<PipelineSyncResult>> Sync(
        [FromQuery] int runsPerPipeline = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _mediator.Send(new SyncPipelinesCommand(runsPerPipeline), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing pipelines");
            return StatusCode(500, "Error syncing pipelines");
        }
    }
}
