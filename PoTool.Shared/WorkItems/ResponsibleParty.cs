namespace PoTool.Shared.WorkItems;

/// <summary>
/// The party responsible for addressing a validation violation.
/// </summary>
public enum ResponsibleParty
{
    /// <summary>
    /// Product Owner is responsible (Epic/Feature descriptions, refinement readiness).
    /// </summary>
    ProductOwner = 1,

    /// <summary>
    /// Development Team is responsible (PBI descriptions, effort estimates).
    /// </summary>
    DevelopmentTeam = 2,

    /// <summary>
    /// Process or organizational issue (structural integrity violations).
    /// </summary>
    Process = 3
}
