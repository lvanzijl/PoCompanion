namespace PoTool.Shared.WorkItems;

/// <summary>
/// Consequences of validation rule violations.
/// </summary>
public enum ValidationConsequence
{
    /// <summary>
    /// Result of Structural Integrity violations.
    /// Reported as backlog health issues; does not suppress other validation.
    /// </summary>
    BacklogHealthProblem = 1,

    /// <summary>
    /// Result of Refinement Readiness violations.
    /// Tree is not ready for refinement; suppresses all PBI-level validation.
    /// </summary>
    RefinementBlocker = 2,

    /// <summary>
    /// Result of Refinement Completeness violations.
    /// Blocks implementation readiness.
    /// </summary>
    IncompleteRefinement = 3
}
