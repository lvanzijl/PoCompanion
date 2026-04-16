namespace PoTool.Client.Services;

public sealed record StartupGateSnapshot(
    bool IsResolving,
    bool ShouldRenderRouter,
    string? PendingNavigationUri);

public sealed class StartupGateCoordinator
{
    private readonly IStartupOrchestratorService _startupOrchestrator;
    private int _resolutionVersion;

    public StartupGateCoordinator(IStartupOrchestratorService startupOrchestrator)
    {
        _startupOrchestrator = startupOrchestrator;
        Snapshot = new StartupGateSnapshot(IsResolving: true, ShouldRenderRouter: false, PendingNavigationUri: null);
    }

    public StartupGateSnapshot Snapshot { get; private set; }

    public StartupStateResolution? LastResolution { get; private set; }

    public async Task ResolveAsync(string? currentRelativeUri, CancellationToken cancellationToken = default)
    {
        var resolutionVersion = Interlocked.Increment(ref _resolutionVersion);
        Snapshot = new StartupGateSnapshot(IsResolving: true, ShouldRenderRouter: false, PendingNavigationUri: null);

        var resolution = await _startupOrchestrator.ResolveStartupStateAsync(currentRelativeUri, cancellationToken);
        if (resolutionVersion != _resolutionVersion)
        {
            return;
        }

        LastResolution = resolution;
        Snapshot = resolution.ShouldRenderCurrentRoute
            ? new StartupGateSnapshot(IsResolving: false, ShouldRenderRouter: true, PendingNavigationUri: null)
            : new StartupGateSnapshot(IsResolving: false, ShouldRenderRouter: false, PendingNavigationUri: resolution.TargetUri);
    }
}
