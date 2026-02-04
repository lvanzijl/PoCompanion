using PoTool.Shared.WorkItems;

namespace PoTool.Core.WorkItems;

/// <summary>
/// Work item with validation issues.
/// </summary>
public sealed record WorkItemWithValidation(
    int TfsId,
    string Type,
    string Title,
    int? ParentTfsId,
    string AreaPath,
    string IterationPath,
    string State,
    string JsonPayload,
    DateTimeOffset RetrievedAt,
    int? Effort,
    IReadOnlyList<ValidationIssue> ValidationIssues,
    bool? IsBlocked = null
);
