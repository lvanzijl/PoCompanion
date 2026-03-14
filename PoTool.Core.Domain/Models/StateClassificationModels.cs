namespace PoTool.Core.Domain.Models;

/// <summary>
/// Canonical lifecycle classification for a raw work item state.
/// </summary>
public enum StateClassification
{
    New = 0,
    InProgress = 1,
    Done = 2,
    Removed = 3
}

/// <summary>
/// Domain input describing how one raw state maps to a canonical lifecycle classification.
/// </summary>
/// <param name="WorkItemType">The work item type name.</param>
/// <param name="StateName">The raw state name.</param>
/// <param name="Classification">The canonical lifecycle classification.</param>
public readonly record struct WorkItemStateClassification(
    string WorkItemType,
    string StateName,
    StateClassification Classification);
