using PoTool.Client.Models;

namespace PoTool.Client.Services;

public interface IOnboardingWorkspaceService
{
    Task<OnboardingWorkspaceData> GetWorkspaceDataAsync(OnboardingWorkspaceFilter filter, CancellationToken cancellationToken = default);
}
