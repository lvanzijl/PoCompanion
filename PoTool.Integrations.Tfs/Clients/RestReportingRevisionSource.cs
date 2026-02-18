using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Integrations.Tfs.Clients;

/// <summary>
/// Revision source adapter that preserves existing reporting REST behavior.
/// </summary>
public sealed class RestReportingRevisionSource : IWorkItemRevisionSource
{
    private readonly IRevisionTfsClient _revisionClient;

    public RestReportingRevisionSource(IRevisionTfsClient revisionClient)
    {
        _revisionClient = revisionClient;
    }

    public RevisionSource SourceType => RevisionSource.RestReportingRevisions;

    public Task<ReportingRevisionsResult> GetRevisionsAsync(
        DateTimeOffset? startDateTime = null,
        string? continuationToken = null,
        ReportingExpandMode expandMode = ReportingExpandMode.None,
        CancellationToken cancellationToken = default)
    {
        return _revisionClient.GetReportingRevisionsAsync(startDateTime, continuationToken, expandMode, cancellationToken);
    }

    public Task<IReadOnlyList<WorkItemRevision>> GetWorkItemRevisionsAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        return _revisionClient.GetWorkItemRevisionsAsync(workItemId, cancellationToken);
    }
}
