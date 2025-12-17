using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetWorkItemByIdQuery.
/// </summary>
public sealed class GetWorkItemByIdQueryHandler : IQueryHandler<GetWorkItemByIdQuery, WorkItemDto?>
{
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<GetWorkItemByIdQueryHandler> _logger;

    public GetWorkItemByIdQueryHandler(
        IWorkItemRepository repository,
        ILogger<GetWorkItemByIdQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<WorkItemDto?> Handle(
        GetWorkItemByIdQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetWorkItemByIdQuery for TfsId={TfsId}", query.TfsId);
        return await _repository.GetByTfsIdAsync(query.TfsId, cancellationToken);
    }
}
