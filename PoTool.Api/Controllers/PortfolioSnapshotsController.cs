using Mediator;
using Microsoft.AspNetCore.Mvc;
using PoTool.Core.Metrics.Commands;
using PoTool.Shared.Metrics;

namespace PoTool.Api.Controllers;

[ApiController]
[Route("api/portfolio/snapshots")]
public sealed class PortfolioSnapshotsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PortfolioSnapshotsController> _logger;

    public PortfolioSnapshotsController(
        IMediator mediator,
        ILogger<PortfolioSnapshotsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("capture")]
    public async Task<ActionResult<PortfolioSnapshotCaptureResultDto>> Capture(
        [FromBody] CapturePortfolioSnapshotsCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (command.ProductOwnerId <= 0)
            {
                return BadRequest("ProductOwnerId must be greater than zero.");
            }

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing portfolio snapshots for ProductOwner {ProductOwnerId}", command.ProductOwnerId);
            return StatusCode(500, "Error capturing portfolio snapshots");
        }
    }
}
