using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

/// <summary>
/// Client-side startup adapter that consumes the authoritative backend startup-state contract.
/// </summary>
public sealed record StartupStateResolution(
    StartupStateResponseDto? Contract,
    string TargetUri,
    bool ShouldRenderCurrentRoute,
    string Reason,
    string RecoveryHint);

public interface IStartupOrchestratorService
{
    Task<StartupStateResolution> ResolveStartupStateAsync(string? currentRelativeUri, CancellationToken cancellationToken = default);
}
