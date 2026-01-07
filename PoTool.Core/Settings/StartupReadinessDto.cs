namespace PoTool.Core.Settings;

/// <summary>
/// Immutable DTO representing the startup readiness state.
/// Used by the Startup Orchestrator to determine where to route the user.
/// </summary>
public sealed record StartupReadinessDto(
    /// <summary>
    /// Whether mock data mode is enabled in configuration.
    /// When true, the app is usable without a real TFS connection.
    /// </summary>
    bool IsMockDataEnabled,
    
    /// <summary>
    /// Whether TFS configuration has been saved.
    /// </summary>
    bool HasSavedTfsConfig,
    
    /// <summary>
    /// Whether Test Connection has succeeded at least once.
    /// </summary>
    bool HasTestedConnectionSuccessfully,
    
    /// <summary>
    /// Whether Verify TFS API has passed (all checks).
    /// </summary>
    bool HasVerifiedTfsApiSuccessfully,
    
    /// <summary>
    /// Whether at least one profile exists.
    /// </summary>
    bool HasAnyProfile,
    
    /// <summary>
    /// The ID of the currently active profile (null if none selected).
    /// </summary>
    int? ActiveProfileId,
    
    /// <summary>
    /// A message explaining what's missing, if the app is not ready.
    /// </summary>
    string? MissingRequirementMessage
);
