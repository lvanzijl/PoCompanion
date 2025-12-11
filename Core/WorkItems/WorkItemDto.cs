namespace Core.WorkItems;

/// <summary>
/// Immutable DTO for work items retrieved from TFS/Azure DevOps.
/// </summary>
public sealed record WorkItemDto
{
    /// <summary>
    /// The TFS/Azure DevOps work item ID.
    /// </summary>
    public required int TfsId { get; init; }

    /// <summary>
    /// The type of work item (e.g., Epic, Feature, Product Backlog Item, Bug).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The title of the work item.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The area path for organizational hierarchy.
    /// </summary>
    public required string AreaPath { get; init; }

    /// <summary>
    /// The iteration path for sprint/release planning.
    /// </summary>
    public required string IterationPath { get; init; }

    /// <summary>
    /// The current state of the work item (e.g., New, Active, Closed).
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Complete JSON payload from TFS for extensibility and detail views.
    /// </summary>
    public required string JsonPayload { get; init; }

    /// <summary>
    /// Timestamp when this work item was retrieved from TFS.
    /// </summary>
    public required DateTimeOffset RetrievedAt { get; init; }
}
