namespace PoTool.Shared.Health;

/// <summary>
/// Represents the ownership state of a Feature in the backlog state model.
/// Ownership is dynamic and reflects the current refinement responsibility.
/// </summary>
public enum FeatureOwnerState
{
    /// <summary>
    /// Description is missing. The Product Owner must define scope before refinement can proceed.
    /// </summary>
    PO,

    /// <summary>
    /// Description exists but PBIs are incomplete. The Team must complete refinement details.
    /// </summary>
    Team,

    /// <summary>
    /// All PBIs are fully refined. The Feature is ready for planning and implementation.
    /// </summary>
    Ready
}
