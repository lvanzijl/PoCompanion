using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Abstraction for retrieving work item revisions for ingestion.
/// </summary>
public interface IWorkItemRevisionSource
{
    /// <summary>
    /// Identifies the configured source type.
    /// </summary>
    RevisionSource SourceType { get; }

    /// <summary>
    /// Retrieves a page of revisions using a source-specific continuation token.
    /// </summary>
    Task<ReportingRevisionsResult> GetRevisionsAsync(
        DateTimeOffset? startDateTime = null,
        string? continuationToken = null,
        ReportingExpandMode expandMode = ReportingExpandMode.None,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves full revisions for a single work item.
    /// </summary>
    Task<IReadOnlyList<WorkItemRevision>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the effective revision source according to persisted configuration.
/// </summary>
public interface IWorkItemRevisionSourceSelector
{
    /// <summary>
    /// Returns the active revision source for the current configuration context.
    /// </summary>
    Task<IWorkItemRevisionSource> GetSourceAsync(CancellationToken cancellationToken = default);
}
