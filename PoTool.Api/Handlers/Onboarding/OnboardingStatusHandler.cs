using PoTool.Api.Services.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Handlers.Onboarding;

public interface IOnboardingStatusHandler
{
    Task<OnboardingOperationResult<OnboardingStatusDto>> GetStatusAsync(CancellationToken cancellationToken);
}

public sealed class OnboardingStatusHandler : IOnboardingStatusHandler
{
    private readonly IOnboardingStatusService _statusService;

    public OnboardingStatusHandler(IOnboardingStatusService statusService)
    {
        _statusService = statusService;
    }

    public Task<OnboardingOperationResult<OnboardingStatusDto>> GetStatusAsync(CancellationToken cancellationToken)
        => _statusService.GetStatusAsync(cancellationToken);
}
