namespace PoTool.Core.Contracts;

/// <summary>
/// Expand mode for the reporting revisions API.
/// The reporting endpoint does NOT support $expand=relations.
/// </summary>
public enum ReportingExpandMode
{
    /// <summary>
    /// No expansion. Returns only basic fields.
    /// </summary>
    None = 0,

    /// <summary>
    /// Expand fields to include long text fields.
    /// This is the only expand mode supported by the reporting endpoint.
    /// </summary>
    Fields = 1
}

/// <summary>
/// Interface for TFS revision-specific operations.
/// All revision communication must use this client.
/// This is separate from ITfsClient to maintain strict separation of concerns.
/// </summary>
public interface IRevisionTfsClient
{
    /// <summary>
    /// Retrieves work item revisions using the reporting API.
    /// Supports incremental sync via startDateTime and paging via continuation tokens.
    /// </summary>
    /// <param name="startDateTime">Optional date to retrieve revisions changed since. Used for incremental sync.</param>
    /// <param name="continuationToken">Optional continuation token for paging.</param>
    /// <param name="expandMode">Expand mode for the response. Use None or Fields (relations NOT supported by reporting endpoint).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing revisions and optional continuation token.</returns>
    Task<ReportingRevisionsResult> GetReportingRevisionsAsync(
        DateTimeOffset? startDateTime = null,
        string? continuationToken = null,
        ReportingExpandMode expandMode = ReportingExpandMode.None,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all revisions for a specific work item.
    /// Use this as a fallback or debug mechanism only.
    /// Primary ingestion should use GetReportingRevisionsAsync.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of revisions for the work item.</returns>
    Task<IReadOnlyList<WorkItemRevision>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the connection can be established.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection is valid.</returns>
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from the reporting revisions API.
/// </summary>
public record ReportingRevisionsResult
{
    /// <summary>
    /// The revisions retrieved.
    /// </summary>
    public required IReadOnlyList<WorkItemRevision> Revisions { get; init; }

    /// <summary>
    /// Continuation token for retrieving the next page of results.
    /// Null if there are no more results.
    /// </summary>
    public string? ContinuationToken { get; init; }

    /// <summary>
    /// Whether the reporting API indicates more results are available.
    /// </summary>
    public bool HasMoreResults => ContinuationToken is not null;

    /// <summary>
    /// HTTP status code returned by the reporting API call.
    /// </summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>
    /// Duration in milliseconds of the HTTP request.
    /// </summary>
    public long? HttpDurationMs { get; init; }

    /// <summary>
    /// Duration in milliseconds of JSON parsing.
    /// </summary>
    public long? ParseDurationMs { get; init; }

    /// <summary>
    /// Duration in milliseconds of transforming JSON into revision objects.
    /// </summary>
    public long? TransformDurationMs { get; init; }

    /// <summary>
    /// Whether this result represents a complete set (no more pages).
    /// </summary>
    public bool IsComplete => ContinuationToken is null;
}

/// <summary>
/// Represents a single work item revision from TFS.
/// Contains the complete state of a work item at a specific revision number.
/// </summary>
public record WorkItemRevision
{
    /// <summary>
    /// TFS work item ID.
    /// </summary>
    public required int WorkItemId { get; init; }

    /// <summary>
    /// Revision number.
    /// </summary>
    public required int RevisionNumber { get; init; }

    /// <summary>
    /// Work item type.
    /// </summary>
    public required string WorkItemType { get; init; }

    /// <summary>
    /// Work item title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Work item state.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Reason for state change.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Iteration path.
    /// </summary>
    public required string IterationPath { get; init; }

    /// <summary>
    /// Area path.
    /// </summary>
    public required string AreaPath { get; init; }

    /// <summary>
    /// When the work item was created (from first revision).
    /// </summary>
    public DateTimeOffset? CreatedDate { get; init; }

    /// <summary>
    /// When this revision was made.
    /// </summary>
    public required DateTimeOffset ChangedDate { get; init; }

    /// <summary>
    /// When the work item was closed (if applicable).
    /// </summary>
    public DateTimeOffset? ClosedDate { get; init; }

    /// <summary>
    /// Effort value.
    /// </summary>
    public int? Effort { get; init; }

    /// <summary>
    /// Tags (semicolon-separated).
    /// </summary>
    public string? Tags { get; init; }

    /// <summary>
    /// Severity (for bugs).
    /// </summary>
    public string? Severity { get; init; }

    /// <summary>
    /// Display name of who made this change.
    /// </summary>
    public string? ChangedBy { get; init; }

    /// <summary>
    /// Field changes in this revision compared to the previous revision.
    /// </summary>
    public IReadOnlyDictionary<string, FieldDelta>? FieldDeltas { get; init; }

    /// <summary>
    /// Relation changes in this revision.
    /// </summary>
    public IReadOnlyList<RelationDelta>? RelationDeltas { get; init; }
}

/// <summary>
/// Represents a change to a single field.
/// </summary>
public record FieldDelta
{
    /// <summary>
    /// The field reference name.
    /// </summary>
    public required string FieldName { get; init; }

    /// <summary>
    /// The previous value (null if newly added).
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// The new value (null if removed).
    /// </summary>
    public string? NewValue { get; init; }
}

/// <summary>
/// Represents a change to a relation.
/// </summary>
public record RelationDelta
{
    /// <summary>
    /// Type of change (Added or Removed).
    /// </summary>
    public required RelationChangeType ChangeType { get; init; }

    /// <summary>
    /// The relation type reference name.
    /// </summary>
    public required string RelationType { get; init; }

    /// <summary>
    /// The target work item ID.
    /// </summary>
    public required int TargetWorkItemId { get; init; }
}

/// <summary>
/// Type of relation change.
/// </summary>
public enum RelationChangeType
{
    /// <summary>
    /// Relation was added.
    /// </summary>
    Added = 0,

    /// <summary>
    /// Relation was removed.
    /// </summary>
    Removed = 1
}
