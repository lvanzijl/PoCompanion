namespace PoTool.Shared.Contracts.TfsVerification;

/// <summary>
/// Types of mutations performed during write verification.
/// </summary>
public enum MutationType
{
    /// <summary>
    /// Creating a new work item.
    /// </summary>
    Create,
    
    /// <summary>
    /// Updating an existing work item.
    /// </summary>
    Update,
    
    /// <summary>
    /// Creating or updating work item links.
    /// </summary>
    Link,
    
    /// <summary>
    /// Closing or completing a work item.
    /// </summary>
    Close,
    
    /// <summary>
    /// Other mutation type.
    /// </summary>
    Other
}
