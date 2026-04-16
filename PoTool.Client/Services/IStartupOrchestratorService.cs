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
/// Deterministic startup resolution states emitted by the central startup state machine.
/// </summary>
public enum StartupResolutionState
{
    NoProfile,
    ProfileInvalid,
    ProfileValid_NoSync,
    Ready,
    Blocked
}

/// <summary>
/// Additional categorization for blocked startup states.
/// </summary>
public enum StartupBlockedReason
{
    MissingConfiguration,
    BackendUnavailable,
    InvalidResponse,
    UnexpectedFailure,
    CacheUnavailable
}

/// <summary>
/// Structured startup resolution result used by the root startup gate.
/// </summary>
public sealed record StartupStateResolution(
    StartupResolutionState State,
    StartupReadinessDto? Readiness,
    string RequestedReadyUri,
    string TargetUri,
    bool ShouldRenderCurrentRoute,
    int? ActiveProfileId,
    string Reason,
    string RecoveryHint,
    StartupBlockedReason? BlockedReason = null
);

/// <summary>
/// Interface for the Startup Orchestrator that determines where to route the user on app startup.
/// </summary>
public interface IStartupOrchestratorService
{
    /// <summary>
    /// Resolves startup state atomically for the current route before any page renders.
    /// </summary>
    Task<StartupStateResolution> ResolveStartupStateAsync(string? currentRelativeUri, CancellationToken cancellationToken = default);
}
