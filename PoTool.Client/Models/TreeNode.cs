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
    /// Whether this node is expanded in the tree view.
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// The depth level of this node in the tree (0 for root nodes).
    /// </summary>
    public int Level { get; set; }

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
    /// Gets the validation icon to display (single icon showing highest severity).
    /// </summary>
    public string ValidationIcon => HighestSeverity switch
    {
        "Error" => "❌",
        "Warning" => "⚠️",
        _ => string.Empty
    };

    /// <summary>
    /// Gets whether this node has any validation issues.
    /// </summary>
    public bool HasValidationIssues => ValidationIssues.Any();
}
