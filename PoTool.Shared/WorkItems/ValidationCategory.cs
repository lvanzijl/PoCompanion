namespace PoTool.Shared.WorkItems;

/// <summary>
/// Categories of validation applied hierarchically to work items.
/// Evaluated in order: StructuralIntegrity → RefinementReadiness → RefinementCompleteness.
/// MissingEffort is always evaluated, independent of other category suppression.
/// </summary>
public enum ValidationCategory
{
    /// <summary>
    /// Detects logically inconsistent work item trees.
    /// Always evaluated; does not block refinement or implementation.
    /// </summary>
    StructuralIntegrity = 1,

    /// <summary>
    /// Ensures intent and context exist before refinement proceeds.
    /// Evaluated after Structural Integrity, before PBI-level rules.
    /// </summary>
    RefinementReadiness = 2,

    /// <summary>
    /// Assesses whether PBIs are ready for implementation.
    /// Only evaluated if all Refinement Readiness rules pass.
    /// </summary>
    RefinementCompleteness = 3,

    /// <summary>
    /// Identifies work items missing effort estimates.
    /// Always evaluated, regardless of Refinement Readiness violations.
    /// </summary>
    MissingEffort = 4
}
