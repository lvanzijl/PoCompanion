using Mediator;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for GetAllGoalsQuery.
/// Retrieves all work items of type Goal.
/// </summary>
public sealed class GetAllGoalsQueryHandler : IQueryHandler<GetAllGoalsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemRepository _repository;
    private readonly ILogger<GetAllGoalsQueryHandler> _logger;

    public GetAllGoalsQueryHandler(
        IWorkItemRepository repository,
        ILogger<GetAllGoalsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(
        GetAllGoalsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetAllGoalsQuery");
        var allWorkItems = await _repository.GetAllAsync(cancellationToken);
        return allWorkItems.Where(wi => wi.Type == WorkItemType.Goal).ToList();
    }
}
