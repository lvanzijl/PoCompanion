using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetFilteredWorkItemsQuery.
/// </summary>
public sealed class GetFilteredWorkItemsQueryHandler : IQueryHandler<GetFilteredWorkItemsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<GetFilteredWorkItemsQueryHandler> _logger;

    public GetFilteredWorkItemsQueryHandler(
        IWorkItemRepository repository,
        ILogger<GetFilteredWorkItemsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetFilteredWorkItemsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetFilteredWorkItemsQuery with filter={Filter}", query.Filter);
        return await _repository.GetFilteredAsync(query.Filter, cancellationToken);
    }
}
