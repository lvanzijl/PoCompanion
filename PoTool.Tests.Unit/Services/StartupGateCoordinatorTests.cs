using PoTool.Client.Services;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class StartupGateCoordinatorTests
{
    [TestMethod]
    public async Task ResolveAsync_SlowResolution_KeepsRouterHiddenUntilReady()
    {
        var orchestrator = new DelayedStartupOrchestrator();
        var coordinator = new StartupGateCoordinator(orchestrator);

        var resolutionTask = coordinator.ResolveAsync("/home/delivery/execution?sprintId=3");

        Assert.IsTrue(coordinator.Snapshot.IsResolving);
        Assert.IsFalse(coordinator.Snapshot.ShouldRenderRouter);
        Assert.IsNull(coordinator.Snapshot.PendingNavigationUri);

        orchestrator.Complete(new StartupStateResolution(
            Contract: new StartupStateResponseDto(
                StartupStateDto.Ready,
                "/home/delivery/execution?sprintId=3",
                "/home/delivery/execution?sprintId=3",
                7,
                StartupSyncStatusDto.Success,
                BlockedReason: null,
                Diagnostics: EmptyDiagnostics()),
            TargetUri: "/home/delivery/execution?sprintId=3",
            ShouldRenderCurrentRoute: true,
            Reason: "Ready",
            RecoveryHint: "Continue."));

        await resolutionTask;

        Assert.IsFalse(coordinator.Snapshot.IsResolving);
        Assert.IsTrue(coordinator.Snapshot.ShouldRenderRouter);
    }

    [TestMethod]
    public async Task ResolveAsync_RedirectRequired_LeavesNavigationPending()
    {
        var orchestrator = new StaticStartupOrchestrator(new StartupStateResolution(
            Contract: new StartupStateResponseDto(
                StartupStateDto.NoProfile,
                "/profiles?returnUrl=%2Fhome",
                "/home",
                ActiveProfileId: null,
                StartupSyncStatusDto.NotApplicable,
                BlockedReason: null,
                Diagnostics: EmptyDiagnostics()),
            TargetUri: "/profiles?returnUrl=%2Fhome",
            ShouldRenderCurrentRoute: false,
            Reason: "No profile",
            RecoveryHint: "Select one."));

        var coordinator = new StartupGateCoordinator(orchestrator);

        await coordinator.ResolveAsync("/home");

        Assert.IsFalse(coordinator.Snapshot.IsResolving);
        Assert.IsFalse(coordinator.Snapshot.ShouldRenderRouter);
        Assert.AreEqual("/profiles?returnUrl=%2Fhome", coordinator.Snapshot.PendingNavigationUri);
    }

    [TestMethod]
    public async Task ResolveAsync_DeepLinkReady_RendersCurrentRoute()
    {
        var orchestrator = new StaticStartupOrchestrator(new StartupStateResolution(
            Contract: new StartupStateResponseDto(
                StartupStateDto.Ready,
                "/home/pipeline-insights",
                "/home/pipeline-insights",
                7,
                StartupSyncStatusDto.Success,
                BlockedReason: null,
                Diagnostics: EmptyDiagnostics()),
            TargetUri: "/home/pipeline-insights",
            ShouldRenderCurrentRoute: true,
            Reason: "Ready",
            RecoveryHint: "Continue."));

        var coordinator = new StartupGateCoordinator(orchestrator);

        await coordinator.ResolveAsync("/home/pipeline-insights");

        Assert.IsTrue(coordinator.Snapshot.ShouldRenderRouter);
        Assert.IsNull(coordinator.Snapshot.PendingNavigationUri);
        Assert.AreEqual("/home/pipeline-insights", coordinator.LastResolution?.TargetUri);
    }

    private static StartupDiagnosticFlagsDto EmptyDiagnostics()
    {
        return new(
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false);
    }

    private sealed class StaticStartupOrchestrator : IStartupOrchestratorService
    {
        private readonly StartupStateResolution _resolution;

        public StaticStartupOrchestrator(StartupStateResolution resolution)
        {
            _resolution = resolution;
        }

        public Task<StartupStateResolution> ResolveStartupStateAsync(string? currentRelativeUri, CancellationToken cancellationToken = default)
            => Task.FromResult(_resolution);
    }

    private sealed class DelayedStartupOrchestrator : IStartupOrchestratorService
    {
        private readonly TaskCompletionSource<StartupStateResolution> _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<StartupStateResolution> ResolveStartupStateAsync(string? currentRelativeUri, CancellationToken cancellationToken = default)
            => _taskCompletionSource.Task;

        public void Complete(StartupStateResolution resolution)
        {
            _taskCompletionSource.TrySetResult(resolution);
        }
    }
}
