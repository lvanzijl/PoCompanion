using Mediator;
using PoTool.Api.Configuration;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for retrieving work items based on configured Goals.
/// Uses product-scoped hierarchical loading or loads directly by goal IDs.
/// Uses read provider to support both Live and Cached modes.
/// </summary>
public class GetGoalHierarchyQueryHandler : IQueryHandler<GetGoalHierarchyQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IProductRepository _productRepository;
    private readonly BattleshipMockDataFacade? _mockDataFacade;
    private readonly TfsRuntimeMode _runtimeMode;
    private readonly ILogger<GetGoalHierarchyQueryHandler> _logger;

    public GetGoalHierarchyQueryHandler(
        IWorkItemReadProvider workItemReadProvider,
        IProductRepository productRepository,
        TfsRuntimeMode runtimeMode,
        ILogger<GetGoalHierarchyQueryHandler> logger,
        BattleshipMockDataFacade? mockDataFacade = null)
    {
        _workItemReadProvider = workItemReadProvider;
        _productRepository = productRepository;
        _mockDataFacade = mockDataFacade;
        _runtimeMode = runtimeMode;
        _logger = logger;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(GetGoalHierarchyQuery query, CancellationToken cancellationToken)
    {
        TfsRuntimeModeGuard.EnsureExpectedMockFacade(_runtimeMode, _mockDataFacade, _logger, nameof(GetGoalHierarchyQueryHandler));

        if (_runtimeMode.UseMockClient)
        {
            // Return mock data for specified goal IDs using new Battleship system
            return _mockDataFacade!.GetMockHierarchyForGoals(query.GoalIds);
        }

        // For TFS mode, use hierarchical loading by goal IDs (goals are product roots)
        _logger.LogDebug("Loading hierarchy for {Count} goals: {GoalIds}", 
            query.GoalIds.Count, string.Join(", ", query.GoalIds));
        
        // Load hierarchically using the goal IDs as roots
        return await _workItemReadProvider.GetByRootIdsAsync(query.GoalIds.ToArray(), cancellationToken);
    }
}
