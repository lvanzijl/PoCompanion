using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Core.BuildQuality.Queries;
using PoTool.Shared.BuildQuality;

namespace PoTool.Api.Controllers;

/// <summary>
/// API endpoints for BuildQuality execution-layer consumers.
/// </summary>
[ApiController]
[Route("api/buildquality")]
public sealed class BuildQualityController : ControllerBase
{
    private readonly IMediator _mediator;

    public BuildQualityController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("rolling")]
    public async Task<ActionResult<BuildQualityPageDto>> GetRolling(
        [FromQuery] int productOwnerId,
        [FromQuery] DateTime windowStartUtc,
        [FromQuery] DateTime windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetBuildQualityRollingWindowQuery(productOwnerId, windowStartUtc, windowEndUtc),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("sprint")]
    public async Task<ActionResult<DeliveryBuildQualityDto>> GetSprint(
        [FromQuery] int productOwnerId,
        [FromQuery] int sprintId,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetBuildQualitySprintQuery(productOwnerId, sprintId),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("pipeline")]
    public async Task<ActionResult<PipelineBuildQualityDto>> GetPipeline(
        [FromQuery] int productOwnerId,
        [FromQuery] int sprintId,
        [FromQuery] int? pipelineDefinitionId = null,
        [FromQuery] int? repositoryId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetBuildQualityPipelineDetailQuery(productOwnerId, sprintId, pipelineDefinitionId, repositoryId),
            cancellationToken);

        return Ok(result);
    }
}
