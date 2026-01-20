using Mediator;
using Microsoft.Extensions.Configuration;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for retrieving work items by root IDs (hierarchical tree loading).
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public class GetWorkItemsByRootIdsQueryHandler : IQueryHandler<GetWorkItemsByRootIdsQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly BattleshipMockDataFacade? _mockDataFacade;
    private readonly bool _useMockClient;

    public GetWorkItemsByRootIdsQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        IConfiguration configuration,
        BattleshipMockDataFacade? mockDataFacade = null)
    {
        _workItemReadProvider = workItemReadProvider;
        _mockDataFacade = mockDataFacade;
        _useMockClient = configuration.GetValue<bool>("TfsIntegration:UseMockClient", false);
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(GetWorkItemsByRootIdsQuery query, CancellationToken cancellationToken)
    {
        if (_useMockClient && _mockDataFacade != null)
        {
            // Return mock data for specified root IDs using new Battleship system
            return _mockDataFacade.GetMockHierarchyForGoals(query.RootIds.ToList());
        }

        // For TFS mode, use hierarchical loading from read provider
        // Live-only mode: use injected provider directly
        return await _workItemReadProvider.GetByRootIdsAsync(query.RootIds, cancellationToken);
    }
}
