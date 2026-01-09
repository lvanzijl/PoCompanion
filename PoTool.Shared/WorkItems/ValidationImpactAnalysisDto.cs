namespace PoTool.Shared.WorkItems;

/// <summary>
/// DTO representing impact analysis of validation violations.
/// Shows which work items are blocked by validation issues.
/// </summary>
public sealed record ValidationImpactAnalysisDto(
    IReadOnlyList<ViolationImpact> Violations,
    int TotalBlockedItems,
    int TotalAffectedHierarchies,
    IReadOnlyList<WorkflowRecommendation> Recommendations
);

/// <summary>
/// Represents the impact of a single validation violation.
/// </summary>
public sealed record ViolationImpact(
    int WorkItemId,
    string WorkItemType,
    string WorkItemTitle,
    string ViolationType,
    string Severity,
    IReadOnlyList<int> BlockedChildrenIds,
    IReadOnlyList<int> BlockedDescendantIds
);

/// <summary>
/// Represents a workflow recommendation based on validation patterns.
/// </summary>
public sealed record WorkflowRecommendation(
    string RecommendationType,
    string Description,
    IReadOnlyList<int> AffectedWorkItemIds,
    int Priority
);
