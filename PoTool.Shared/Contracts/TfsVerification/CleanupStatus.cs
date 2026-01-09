namespace PoTool.Shared.Contracts.TfsVerification;

/// <summary>
/// Status of cleanup after write verification.
/// </summary>
public enum CleanupStatus
{
    /// <summary>
    /// Cleanup was successful.
    /// </summary>
    CleanedUp,
    
    /// <summary>
    /// Cleanup was not required (read-only check or user-provided work item).
    /// </summary>
    NotRequired,
    
    /// <summary>
    /// Cleanup failed - manual intervention may be needed.
    /// </summary>
    Failed,
    
    /// <summary>
    /// Cleanup was skipped (verification failed before write).
    /// </summary>
    Skipped
}
