using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetWorkItemByIdQuery.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public sealed class GetWorkItemByIdQueryHandler : IQueryHandler<GetWorkItemByIdQuery, WorkItemDto?>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly ILogger<GetWorkItemByIdQueryHandler> _logger;

    public GetWorkItemByIdQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        ILogger<GetWorkItemByIdQueryHandler> logger)
    {
        _workItemReadProvider = workItemReadProvider;
        _logger = logger;
    }

    public async ValueTask<WorkItemDto?> Handle(
        GetWorkItemByIdQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetWorkItemByIdQuery for TfsId={TfsId}", query.TfsId);
        // Live-only mode: use injected provider directly
        return await _workItemReadProvider.GetByTfsIdAsync(query.TfsId, cancellationToken);
    }
}
