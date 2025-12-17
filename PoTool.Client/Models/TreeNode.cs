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
    /// The type of work item (Epic, Feature, User Story, etc.).
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
}
