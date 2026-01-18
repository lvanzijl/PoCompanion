namespace PoTool.Api.Persistence.Entities;

/// <summary>
/// Entity for storing work item state classifications.
/// Scoped to TFS project - all teams and products within a project share the same configuration.
/// </summary>
public class WorkItemStateClassificationEntity
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// TFS project name this classification applies to.
    /// </summary>
    public required string TfsProjectName { get; set; }

    /// <summary>
    /// Work item type name (e.g., "Epic", "Feature", "Product Backlog Item").
    /// </summary>
    public required string WorkItemType { get; set; }

    /// <summary>
    /// State name (e.g., "New", "Active", "Done").
    /// </summary>
    public required string StateName { get; set; }

    /// <summary>
    /// Classification for this state (0=New, 1=InProgress, 2=Done).
    /// </summary>
    public required int Classification { get; set; }

    /// <summary>
    /// When this classification was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this classification was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
