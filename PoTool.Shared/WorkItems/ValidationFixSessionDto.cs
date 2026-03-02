namespace PoTool.Shared.WorkItems;

/// <summary>
/// Result of the validation fix session query.
/// Contains the rule metadata and all work items that violate the rule.
/// Used by the Validation Fix Session page.
/// </summary>
/// <param name="RuleId">The rule identifier (e.g. "SI-1", "RC-2").</param>
/// <param name="RuleTitle">Short human-readable description of the rule.</param>
/// <param name="CategoryKey">Category key (e.g. "SI", "RR", "RC", "EFF").</param>
/// <param name="CategoryLabel">Human-readable category label.</param>
/// <param name="Items">All work items that violate this rule, ordered by TFS ID.</param>
public sealed record ValidationFixSessionDto(
    string RuleId,
    string RuleTitle,
    string CategoryKey,
    string CategoryLabel,
    List<ValidationFixItemDto> Items
);

/// <summary>
/// A single work item entry in a validation fix session.
/// Contains only the fields needed for the Fix Session page.
/// </summary>
/// <param name="TfsId">TFS work item identifier.</param>
/// <param name="Type">Work item type (e.g. "Epic", "Feature", "Product Backlog Item").</param>
/// <param name="Title">Work item title.</param>
/// <param name="ParentTfsId">TFS ID of the parent work item, if any.</param>
/// <param name="State">Current work item state.</param>
/// <param name="AreaPath">Area path of the work item.</param>
/// <param name="IterationPath">Iteration path of the work item.</param>
/// <param name="Effort">Story point / effort estimate, if set.</param>
/// <param name="Description">Work item description (may be null or empty for the violating items).</param>
/// <param name="ViolationMessage">The specific violation message for this rule on this item.</param>
public sealed record ValidationFixItemDto(
    int TfsId,
    string Type,
    string Title,
    int? ParentTfsId,
    string State,
    string AreaPath,
    string IterationPath,
    int? Effort,
    string? Description,
    string ViolationMessage
);
