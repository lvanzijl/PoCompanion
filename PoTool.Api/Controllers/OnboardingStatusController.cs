using Microsoft.AspNetCore.Mvc;
using PoTool.Api.Handlers.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Controllers;

[ApiController]
[Route("api/onboarding/status")]
[DataSourceMode(RouteIntent.LiveAllowed)]
public sealed class OnboardingStatusController : ControllerBase
{
    private readonly IOnboardingStatusHandler _handler;

    public OnboardingStatusController(IOnboardingStatusHandler handler)
    {
        _handler = handler;
    }

    [HttpGet]
    [ProducesResponseType(typeof(OnboardingSuccessEnvelope<OnboardingStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(OnboardingErrorDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OnboardingSuccessEnvelope<OnboardingStatusDto>>> GetStatus(CancellationToken cancellationToken = default)
        => OnboardingApiResultMapper.ToActionResult(this, await _handler.GetStatusAsync(cancellationToken));
}
