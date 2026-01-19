namespace PoTool.Shared.WorkItems;

/// <summary>
/// Represents the complete hierarchical validation result for a work item tree.
/// </summary>
/// <param name="RootWorkItemId">The TFS ID of the root work item in the tree.</param>
/// <param name="BacklogHealthProblems">Structural integrity violations (always evaluated).</param>
/// <param name="RefinementBlockers">Refinement readiness violations (Epic/Feature descriptions).</param>
/// <param name="IncompleteRefinementIssues">PBI-level violations (only evaluated if no refinement blockers).</param>
/// <param name="WasSuppressed">True if PBI validation was suppressed due to refinement blockers.</param>
public sealed record HierarchicalValidationResult(
    int RootWorkItemId,
    IReadOnlyList<ValidationRuleResult> BacklogHealthProblems,
    IReadOnlyList<ValidationRuleResult> RefinementBlockers,
    IReadOnlyList<ValidationRuleResult> IncompleteRefinementIssues,
    bool WasSuppressed
)
{
    /// <summary>
    /// Gets whether this tree has any structural integrity issues.
    /// </summary>
    public bool HasBacklogHealthProblems => BacklogHealthProblems.Count > 0;

    /// <summary>
    /// Gets whether this tree has any refinement blockers.
    /// </summary>
    public bool HasRefinementBlockers => RefinementBlockers.Count > 0;

    /// <summary>
    /// Gets whether this tree has any incomplete refinement issues.
    /// </summary>
    public bool HasIncompleteRefinement => IncompleteRefinementIssues.Count > 0;

    /// <summary>
    /// Gets whether this tree is ready for refinement (no refinement blockers).
    /// </summary>
    public bool IsReadyForRefinement => !HasRefinementBlockers;

    /// <summary>
    /// Gets whether this tree is ready for implementation (no refinement blockers and no incomplete refinement).
    /// </summary>
    public bool IsReadyForImplementation => IsReadyForRefinement && !HasIncompleteRefinement;

    /// <summary>
    /// Gets all validation violations across all categories.
    /// </summary>
    public IEnumerable<ValidationRuleResult> AllViolations =>
        BacklogHealthProblems
            .Concat(RefinementBlockers)
            .Concat(IncompleteRefinementIssues);
}
