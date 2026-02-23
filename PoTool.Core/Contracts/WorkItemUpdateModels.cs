namespace PoTool.Core.Contracts;

public sealed record WorkItemUpdateFieldChange(
    string FieldRefName,
    string? OldValue,
    string? NewValue);

public sealed record WorkItemUpdate
{
    public required int WorkItemId { get; init; }
    public required int UpdateId { get; init; }
    public required DateTimeOffset RevisedDate { get; init; }
    public required IReadOnlyDictionary<string, WorkItemUpdateFieldChange> FieldChanges { get; init; }
}
