using Mediator;
using Microsoft.Extensions.Configuration;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for retrieving work items based on configured Goals.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public class GetGoalHierarchyQueryHandler : IQueryHandler<GetGoalHierarchyQuery, IEnumerable<WorkItemDto>>
{
    private readonly WorkItemReadProviderFactory _providerFactory;
    private readonly BattleshipMockDataFacade? _mockDataFacade;
    private readonly bool _useMockClient;

    public GetGoalHierarchyQueryHandler(
        WorkItemReadProviderFactory providerFactory,
        IConfiguration configuration,
        BattleshipMockDataFacade? mockDataFacade = null)
    {
        _providerFactory = providerFactory;
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

        // For TFS mode, query from read provider
        // Get all work items that are descendants of the specified goals
        var provider = _providerFactory.Create();
        var allItems = await provider.GetAllAsync(cancellationToken);
        return WorkItemHierarchyHelper.FilterDescendants(query.GoalIds, allItems);
    }
}
