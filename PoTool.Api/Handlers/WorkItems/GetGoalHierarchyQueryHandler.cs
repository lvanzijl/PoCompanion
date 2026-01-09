using Mediator;
using Microsoft.Extensions.Configuration;
using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for retrieving work items based on configured Goals.
/// </summary>
public class GetGoalHierarchyQueryHandler : IQueryHandler<GetGoalHierarchyQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly BattleshipMockDataFacade? _mockDataFacade;
    private readonly bool _useMockClient;

    public GetGoalHierarchyQueryHandler(
        IWorkItemRepository workItemRepository,
        IConfiguration configuration,
        BattleshipMockDataFacade? mockDataFacade = null)
    {
        _workItemRepository = workItemRepository;
        _mockDataFacade = mockDataFacade;
        _useMockClient = configuration.GetValue<bool>("TfsIntegration:UseMockClient", false);
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(GetGoalHierarchyQuery query, CancellationToken cancellationToken)
    {
        if (_useMockClient && _mockDataFacade != null)
        {
            // Return mock data for specified goal IDs using new Battleship system
            return _mockDataFacade.GetMockHierarchyForGoals(query.GoalIds);
        }

        // For TFS mode, query from repository
        // Get all work items that are descendants of the specified goals
        var allItems = await _workItemRepository.GetAllAsync(cancellationToken);
        return WorkItemHierarchyHelper.FilterDescendants(query.GoalIds, allItems);
    }
}
