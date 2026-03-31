namespace PoTool.Shared.WorkItems;

/// <summary>
/// Immutable DTO for work items.
/// All data extracted from TFS is now stored in typed properties.
/// </summary>
public sealed record WorkItemDto(
    int TfsId,
    string Type,
    string Title,
    int? ParentTfsId,
    string AreaPath,
    string IterationPath,
    string State,
    DateTimeOffset RetrievedAt,
    int? Effort,
    string? Description,
    DateTimeOffset? CreatedDate = null,
    DateTimeOffset? ClosedDate = null,
    string? Severity = null,
    string? Tags = null,
    bool? IsBlocked = null,
    List<WorkItemRelation>? Relations = null,
    DateTimeOffset? ChangedDate = null,
    int? BusinessValue = null,
    double? BacklogPriority = null,
    int? StoryPoints = null,
    double? TimeCriticality = null,
    string? ProjectNumber = null,
    string? ProjectElement = null
)
{
    public WorkItemDto()
        : this(0, string.Empty, string.Empty, null, string.Empty, string.Empty, string.Empty, default, null, null)
    {
    }
}
