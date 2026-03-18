using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;

namespace PoTool.Api.Configuration;

/// <summary>
/// Captures the TFS runtime mode selected during startup so all downstream consumers
/// use one authoritative mock-vs-real decision for the lifetime of the process.
/// </summary>
public sealed class TfsRuntimeMode
{
    public TfsRuntimeMode(bool useMockClient)
    {
        UseMockClient = useMockClient;
    }

    public bool UseMockClient { get; }

    public bool IsRealDataMode => !UseMockClient;

    public string Name => UseMockClient ? "Mock" : "Real";
}

/// <summary>
/// Guard helpers that prevent silent mixed mock+real runtime states.
/// </summary>
public static class TfsRuntimeModeGuard
{
    public static void EnsureExpectedClient(
        TfsRuntimeMode runtimeMode,
        ITfsClient tfsClient,
        ILogger logger,
        string callSite)
    {
        var isMockClient = tfsClient is MockTfsClient or BattleshipMockDataFacade;
        if (runtimeMode.IsRealDataMode && isMockClient)
        {
            logger.LogCritical(
                "Mixed runtime detected at {CallSite}: real-data mode selected but {Implementation} was resolved for {Abstraction}",
                callSite,
                tfsClient.GetType().FullName,
                nameof(ITfsClient));

            throw new InvalidOperationException(
                $"Real-data mode is active but {tfsClient.GetType().FullName} was resolved for {nameof(ITfsClient)}.");
        }

        if (runtimeMode.UseMockClient && tfsClient is not MockTfsClient)
        {
            logger.LogCritical(
                "Mixed runtime detected at {CallSite}: mock mode selected but {Implementation} was resolved for {Abstraction}",
                callSite,
                tfsClient.GetType().FullName,
                nameof(ITfsClient));

            throw new InvalidOperationException(
                $"Mock mode is active but {tfsClient.GetType().FullName} was resolved for {nameof(ITfsClient)}.");
        }
    }

    public static void EnsureExpectedMockFacade(
        TfsRuntimeMode runtimeMode,
        BattleshipMockDataFacade? mockDataFacade,
        ILogger logger,
        string callSite)
    {
        if (runtimeMode.IsRealDataMode && mockDataFacade != null)
        {
            logger.LogCritical(
                "Mixed runtime detected at {CallSite}: real-data mode selected but {Implementation} was resolved",
                callSite,
                mockDataFacade.GetType().FullName);

            throw new InvalidOperationException(
                $"Real-data mode is active but {mockDataFacade.GetType().FullName} was resolved.");
        }

        if (runtimeMode.UseMockClient && mockDataFacade == null)
        {
            logger.LogCritical(
                "Mixed runtime detected at {CallSite}: mock mode selected but {Implementation} was not registered",
                callSite,
                nameof(BattleshipMockDataFacade));

            throw new InvalidOperationException(
                $"Mock mode is active but {nameof(BattleshipMockDataFacade)} was not registered.");
        }
    }
}
