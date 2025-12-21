using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetWorkItemRevisionsQuery.
/// Retrieves revision history directly from TFS for a specific work item.
/// </summary>
public sealed class GetWorkItemRevisionsQueryHandler : IQueryHandler<GetWorkItemRevisionsQuery, IEnumerable<WorkItemRevisionDto>>
{
    private readonly ITfsClient _tfsClient;
    private readonly ILogger<GetWorkItemRevisionsQueryHandler> _logger;

    public GetWorkItemRevisionsQueryHandler(
        ITfsClient tfsClient,
        ILogger<GetWorkItemRevisionsQueryHandler> logger)
    {
        _tfsClient = tfsClient;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemRevisionDto>> Handle(
        GetWorkItemRevisionsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetWorkItemRevisionsQuery for WorkItemId={WorkItemId}", query.WorkItemId);
        
        var revisions = await _tfsClient.GetWorkItemRevisionsAsync(query.WorkItemId, cancellationToken);
        
        _logger.LogInformation("Retrieved {Count} revisions for work item {WorkItemId}", 
            revisions.Count(), query.WorkItemId);
        
        return revisions;
    }
}
