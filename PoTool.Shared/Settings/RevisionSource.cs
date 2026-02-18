namespace PoTool.Shared.Settings;

/// <summary>
/// Configures which upstream endpoint is used to retrieve work item revisions.
/// </summary>
public enum RevisionSource
{
    /// <summary>
    /// Use the reporting revisions REST endpoint (existing behavior).
    /// </summary>
    RestReportingRevisions = 0,

    /// <summary>
    /// Use the Analytics OData revisions endpoint.
    /// </summary>
    AnalyticsODataRevisions = 1
}
