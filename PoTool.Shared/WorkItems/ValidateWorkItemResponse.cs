namespace PoTool.Shared.WorkItems;

/// <summary>
/// Response DTO for work item validation.
/// Used to validate work item existence directly from TFS.
/// </summary>
public sealed record ValidateWorkItemResponse
{
    /// <summary>
    /// Whether the work item exists in TFS.
    /// </summary>
    public required bool Exists { get; init; }

    /// <summary>
    /// The work item ID (same as requested).
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The title of the work item if it exists.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The type of the work item if it exists (e.g., "Epic", "Feature").
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Error message if validation failed due to connectivity or authorization issues.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
