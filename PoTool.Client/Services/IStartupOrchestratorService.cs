namespace PoTool.Client.Services;

/// <summary>
/// Startup readiness DTO matching the backend StartupReadinessDto.
/// This is a separate copy because the Client (Blazor WebAssembly) cannot 
/// directly reference PoTool.Core which targets the server-side.
/// Keep this in sync with PoTool.Core/Settings/StartupReadinessDto.cs.
/// </summary>
public sealed record StartupReadinessDto(
    bool IsMockDataEnabled,
    bool HasSavedTfsConfig,
    bool HasTestedConnectionSuccessfully,
    bool HasVerifiedTfsApiSuccessfully,
    bool HasAnyProfile,
    int? ActiveProfileId,
    string? MissingRequirementMessage
);

/// <summary>
/// Explicit readiness states for startup gating and root routing.
/// </summary>
public enum StartupReadinessState
{
    Ready,
    NotReady,
    SetupRequired,
    SyncRequired,
    Unavailable,
    Error
}

/// <summary>
/// Structured startup readiness result that never relies on null.
/// </summary>
public sealed record StartupReadinessResult(
    StartupReadinessState State,
    StartupReadinessDto? Readiness,
    string Reason,
    string RecoveryHint
);

/// <summary>
/// Represents the startup routing destination.
/// </summary>
public enum StartupRoute
{
    /// <summary>
    /// Route to the home dashboard.
    /// </summary>
    Home,

    /// <summary>
     /// Route to Profiles Home (user is ready to use the app).
     /// </summary>
    ProfilesHome,

    /// <summary>
    /// Route to TFS Configuration page.
    /// </summary>
    Configuration,

    /// <summary>
     /// Route to Create First Profile page.
     /// </summary>
    CreateFirstProfile,

    /// <summary>
    /// Route to Sync Gate page.
    /// </summary>
    SyncGate,

    /// <summary>
    /// Route to the blocking startup error page.
    /// </summary>
    BlockingError
}

/// <summary>
/// Result of startup routing decision.
/// </summary>
public sealed record StartupRoutingResult(
    StartupRoute Route,
    string Message,
    string RecoveryHint,
    bool IsBlocking
);

/// <summary>
/// Interface for the Startup Orchestrator that determines where to route the user on app startup.
/// </summary>
public interface IStartupOrchestratorService
{
    /// <summary>
    /// Gets the startup readiness state from the backend.
    /// </summary>
    Task<StartupReadinessResult> GetStartupReadinessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines where to route the user based on startup readiness state.
    /// </summary>
    StartupRoutingResult DetermineRoute(StartupReadinessResult readiness);

    /// <summary>
    /// Checks if a given feature page should be accessible based on the current readiness state.
    /// Returns true if accessible, false if should be blocked.
    /// </summary>
    bool IsFeaturePageAccessible(StartupReadinessResult readiness);
}
