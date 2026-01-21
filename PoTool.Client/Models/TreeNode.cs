using PoTool.Shared.WorkItems;

namespace PoTool.Client.Models;

/// <summary>
/// Represents a node in the work item tree hierarchy.
/// </summary>
public class TreeNode
{
    /// <summary>
    /// The TFS ID of the work item.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The type of work item (Goal, Objective, Epic, Feature, Product Backlog Item, Task).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The title/name of the work item.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The current state of the work item (Active, Closed, etc.).
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// The TFS ID of the parent work item, if any.
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// Child nodes in the hierarchy.
    /// </summary>
    public List<TreeNode> Children { get; set; } = new();

    /// <summary>
    /// IDs of direct children (for quick access).
    /// </summary>
    public List<int> ChildrenIds { get; set; } = new();

    /// <summary>
    /// Whether this node is expanded in the tree view.
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// The depth level of this node in the tree (0 for root nodes).
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Alias for Level to match spec terminology.
    /// </summary>
    public int Depth => Level;

    /// <summary>
    /// The full work item data as JSON.
    /// </summary>
    public string? JsonPayload { get; set; }

    /// <summary>
    /// Whether this node is selected.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// The color associated with this work item type.
    /// </summary>
    public string TypeColor => WorkItemTypeHelper.GetColor(Type);

    /// <summary>
    /// List of validation issues for this work item.
    /// </summary>
    public List<string> ValidationIssues { get; set; } = new();

    /// <summary>
    /// Highest severity level of validation issues (Error > Warning).
    /// </summary>
    public string? HighestSeverity { get; set; }
    
    /// <summary>
    /// Highest validation category of issues on this item (for self issues).
    /// Used to determine which icon to display.
    /// </summary>
    public ValidationCategory? HighestCategory { get; set; }
    
    /// <summary>
    /// Highest validation category of issues in descendant items.
    /// Used to determine which icon to display for descendant issues.
    /// </summary>
    public ValidationCategory? HighestDescendantCategory { get; set; }

    /// <summary>
    /// Gets the validation icon to display for self issues (different icon per category).
    /// </summary>
    public string SelfValidationIcon => HighestCategory switch
    {
        ValidationCategory.StructuralIntegrity => "🔴", // Red circle for structural integrity
        ValidationCategory.RefinementReadiness => "🟡", // Yellow circle for refinement readiness
        ValidationCategory.RefinementCompleteness => "🟠", // Orange circle for refinement completeness
        _ => "❌" // Fallback error icon
    };
    
    /// <summary>
    /// Gets the validation icon to display for descendant issues.
    /// </summary>
    public string DescendantValidationIcon => "→"; // Arrow icon for descendant issues

    /// <summary>
    /// Gets the validation icon to display (single icon showing highest severity).
    /// Uses descendant icon if only descendants have issues, otherwise uses self icon.
    /// </summary>
    public string ValidationIcon
    {
        get
        {
            if (HasValidationIssues)
            {
                // Self has issues - use category-specific icon
                return SelfValidationIcon;
            }
            else if (HasDescendantIssues)
            {
                // Only descendants have issues - use arrow icon
                return DescendantValidationIcon;
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets whether this node has any validation issues.
    /// </summary>
    public bool HasValidationIssues => ValidationIssues.Any();

    /// <summary>
    /// Number of errors on this item itself (not descendants).
    /// </summary>
    public int SelfErrorCount { get; set; }

    /// <summary>
    /// Number of warnings on this item itself (not descendants).
    /// </summary>
    public int SelfWarningCount { get; set; }

    /// <summary>
    /// Ordered list of descendant IDs with validation issues.
    /// Precomputed during tree building for performance.
    /// Ordered by depth (closer descendants first), then stable pre-order traversal.
    /// </summary>
    public List<int> InvalidDescendantIds { get; set; } = new();

    /// <summary>
    /// Gets whether this node has descendant validation issues.
    /// </summary>
    public bool HasDescendantIssues => InvalidDescendantIds.Count > 0;
}
