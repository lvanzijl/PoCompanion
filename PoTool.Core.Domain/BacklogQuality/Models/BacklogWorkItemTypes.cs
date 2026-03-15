namespace PoTool.Core.Domain.BacklogQuality.Models;

/// <summary>
/// Canonical work item type names used by the backlog-quality domain slice.
/// </summary>
public static class BacklogWorkItemTypes
{
    public const string Epic = "Epic";
    public const string Feature = "Feature";
    public const string ProductBacklogItem = "Product Backlog Item";
    public const string PbiShort = "PBI";
    public const string UserStory = "User Story";
    public const string Bug = "Bug";
    public const string Task = "Task";

    /// <summary>
    /// Gets the work item types that participate in structural-integrity checks.
    /// Removed items remain in the graph and may still produce structural findings.
    /// </summary>
    public static IReadOnlyList<string> StructuralIntegrityTypes { get; } =
    [
        Epic,
        Feature,
        ProductBacklogItem,
        PbiShort,
        UserStory,
        Bug,
        Task
    ];

    /// <summary>
    /// Gets the work item types treated as implementable backlog items.
    /// </summary>
    public static IReadOnlyList<string> PbiTypes { get; } =
    [
        ProductBacklogItem,
        PbiShort,
        UserStory
    ];
}
