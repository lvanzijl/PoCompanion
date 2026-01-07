namespace PoTool.Client.Services;

/// <summary>
/// Startup readiness DTO matching the backend StartupReadinessDto.
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
/// Represents the startup routing destination.
/// </summary>
public enum StartupRoute
{
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
    CreateFirstProfile
}

/// <summary>
/// Result of startup routing decision.
/// </summary>
public sealed record StartupRoutingResult(
    StartupRoute Route,
    string? Message,
    bool IsAppUsable
);

/// <summary>
/// Interface for the Startup Orchestrator that determines where to route the user on app startup.
/// </summary>
public interface IStartupOrchestratorService
{
    /// <summary>
    /// Gets the startup readiness state from the backend.
    /// </summary>
    Task<StartupReadinessDto?> GetStartupReadinessAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Determines where to route the user based on startup readiness state.
    /// </summary>
    StartupRoutingResult DetermineRoute(StartupReadinessDto readiness);
    
    /// <summary>
    /// Checks if a given feature page should be accessible based on the current readiness state.
    /// Returns true if accessible, false if should be blocked.
    /// </summary>
    bool IsFeaturePageAccessible(StartupReadinessDto readiness);
}
