namespace PoTool.Core.Contracts;

public enum ReportingExpandMode
{
    None = 0,
    Fields = 1
}

public enum ReportingRevisionsTerminationReason
{
    MaxEmptyPages = 1,
    MaxTotalPages = 2,
    RepeatedContinuationToken = 3,
    ProgressWithoutData = 4,
    MalformedPayload = 5
}

public sealed record ReportingRevisionsTermination
{
    public ReportingRevisionsTermination(ReportingRevisionsTerminationReason reason, string message)
    {
        Reason = reason;
        Message = message;
    }

    public ReportingRevisionsTerminationReason Reason { get; }
    public string Message { get; }
}

public sealed record ReportingRevisionsResult
{
    public ReportingRevisionsResult(
        IReadOnlyList<WorkItemRevision> revisions,
        string? continuationToken,
        ReportingRevisionsTermination? termination = null,
        int? httpStatusCode = null,
        long? httpDurationMs = null,
        long? parseDurationMs = null,
        long? transformDurationMs = null)
    {
        Revisions = revisions ?? throw new ArgumentNullException(nameof(revisions));
        ContinuationToken = string.IsNullOrWhiteSpace(continuationToken) ? null : continuationToken;
        Termination = termination;
        HttpStatusCode = httpStatusCode;
        HttpDurationMs = httpDurationMs;
        ParseDurationMs = parseDurationMs;
        TransformDurationMs = transformDurationMs;

        if (Termination != null && ContinuationToken != null)
        {
            throw new InvalidOperationException("Invalid state: a terminated result cannot include a continuation token.");
        }
    }

    public IReadOnlyList<WorkItemRevision> Revisions { get; }
    public string? ContinuationToken { get; }
    public ReportingRevisionsTermination? Termination { get; }
    public bool HasMoreResults => ContinuationToken is not null;
    public int? HttpStatusCode { get; }
    public long? HttpDurationMs { get; }
    public long? ParseDurationMs { get; }
    public long? TransformDurationMs { get; }
    public bool IsComplete => ContinuationToken is null;
    public bool WasTerminatedEarly => Termination is not null;
}

public record WorkItemRevision
{
    public required int WorkItemId { get; init; }
    public required int RevisionNumber { get; init; }
    public required string WorkItemType { get; init; }
    public required string Title { get; init; }
    public required string State { get; init; }
    public string? Reason { get; init; }
    public required string IterationPath { get; init; }
    public required string AreaPath { get; init; }
    public DateTimeOffset? CreatedDate { get; init; }
    public required DateTimeOffset ChangedDate { get; init; }
    public DateTimeOffset? ClosedDate { get; init; }
    public double? Effort { get; init; }
    public int? BusinessValue { get; init; }
    public string? Tags { get; init; }
    public string? Severity { get; init; }
    public string? ChangedBy { get; init; }
    public IReadOnlyDictionary<string, FieldDelta>? FieldDeltas { get; init; }
    public IReadOnlyList<RelationDelta>? RelationDeltas { get; init; }
}

public record FieldDelta
{
    public required string FieldName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

public record RelationDelta
{
    public required RelationChangeType ChangeType { get; init; }
    public required string RelationType { get; init; }
    public required int TargetWorkItemId { get; init; }
}

public enum RelationChangeType
{
    Added = 0,
    Removed = 1
}
