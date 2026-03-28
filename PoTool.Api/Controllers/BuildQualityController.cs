using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Services;
using PoTool.Core.BuildQuality.Queries;
using PoTool.Shared.BuildQuality;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Controllers;

/// <summary>
/// API endpoints for BuildQuality execution-layer consumers.
/// </summary>
[ApiController]
[Route("api/buildquality")]
public sealed class BuildQualityController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly DeliveryFilterResolutionService _deliveryFilterResolutionService;

    public BuildQualityController(
        IMediator mediator,
        DeliveryFilterResolutionService deliveryFilterResolutionService)
    {
        _mediator = mediator;
        _deliveryFilterResolutionService = deliveryFilterResolutionService;
    }

    [HttpGet("rolling")]
    public async Task<ActionResult<DeliveryQueryResponseDto<BuildQualityPageDto>>> GetRolling(
        [FromQuery] int productOwnerId,
        [FromQuery] DateTime windowStartUtc,
        [FromQuery] DateTime windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        var resolution = await _deliveryFilterResolutionService.ResolveAsync(
            new DeliveryFilterBoundaryRequest(
                ProductOwnerId: productOwnerId,
                RangeStartUtc: new DateTimeOffset(DateTime.SpecifyKind(windowStartUtc, DateTimeKind.Utc)),
                RangeEndUtc: new DateTimeOffset(DateTime.SpecifyKind(windowEndUtc, DateTimeKind.Utc))),
            nameof(GetRolling),
            cancellationToken);

        var result = await _mediator.Send(
            new GetBuildQualityRollingWindowQuery(productOwnerId, resolution.EffectiveFilter),
            cancellationToken);

        return Ok(DeliveryFilterResolutionService.ToResponse(result, resolution));
    }

    [HttpGet("sprint")]
    public async Task<ActionResult<DeliveryQueryResponseDto<DeliveryBuildQualityDto>>> GetSprint(
        [FromQuery] int productOwnerId,
        [FromQuery] int sprintId,
        CancellationToken cancellationToken = default)
    {
        var resolution = await _deliveryFilterResolutionService.ResolveAsync(
            new DeliveryFilterBoundaryRequest(
                ProductOwnerId: productOwnerId,
                SprintId: sprintId),
            nameof(GetSprint),
            cancellationToken);

        var result = await _mediator.Send(
            new GetBuildQualitySprintQuery(productOwnerId, resolution.EffectiveFilter),
            cancellationToken);

        return Ok(DeliveryFilterResolutionService.ToResponse(result, resolution));
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
