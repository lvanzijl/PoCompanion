namespace PoTool.Shared.Contracts;

/// <summary>
/// Represents the state of a progress phase.
/// </summary>
public enum ProgressState
{
    /// <summary>
    /// Phase is currently running.
    /// </summary>
    Running,
    
    /// <summary>
    /// Phase completed successfully.
    /// </summary>
    Succeeded,
    
    /// <summary>
    /// Phase failed.
    /// </summary>
    Failed
}

/// <summary>
/// Progress update for TFS configuration save and verify operation.
/// </summary>
public sealed record TfsConfigProgressUpdate(
    string Phase,
    ProgressState State,
    string Message,
    int? PercentComplete = null,
    string? Details = null
);
