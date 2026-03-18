using Mediator;
using PoTool.Api.Configuration;
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
    private readonly TfsRuntimeMode _runtimeMode;
    private readonly ILogger<GetWorkItemsByRootIdsQueryHandler> _logger;

    public GetWorkItemsByRootIdsQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        TfsRuntimeMode runtimeMode,
        ILogger<GetWorkItemsByRootIdsQueryHandler> logger,
        BattleshipMockDataFacade? mockDataFacade = null)
    {
        _workItemReadProvider = workItemReadProvider;
        _mockDataFacade = mockDataFacade;
        _runtimeMode = runtimeMode;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(GetWorkItemsByRootIdsQuery query, CancellationToken cancellationToken)
    {
        TfsRuntimeModeGuard.EnsureExpectedMockFacade(_runtimeMode, _mockDataFacade, _logger, nameof(GetWorkItemsByRootIdsQueryHandler));

        if (_runtimeMode.UseMockClient)
        {
            // Return mock data for specified root IDs using new Battleship system
            return _mockDataFacade!.GetMockHierarchyForGoals(query.RootIds.ToList());
        }

        // For TFS mode, use hierarchical loading from read provider
        // Live-only mode: use injected provider directly
        return await _workItemReadProvider.GetByRootIdsAsync(query.RootIds, cancellationToken);
    }
}
