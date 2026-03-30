using Mediator;
using PoTool.Api.Configuration;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Shared.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for retrieving work items based on configured Goals.
/// Uses the cache-backed analytical query boundary for cached goal hierarchy reads,
/// while keeping mock/runtime behavior explicit in the handler.
/// </summary>
public class GetGoalHierarchyQueryHandler : IQueryHandler<GetGoalHierarchyQuery, IEnumerable<WorkItemDto>>
{
    private readonly IWorkItemQuery _workItemQuery;
    private readonly BattleshipMockDataFacade? _mockDataFacade;
    private readonly TfsRuntimeMode _runtimeMode;
    private readonly ILogger<GetGoalHierarchyQueryHandler> _logger;

    public GetGoalHierarchyQueryHandler(
        IWorkItemQuery workItemQuery,
        TfsRuntimeMode runtimeMode,
        ILogger<GetGoalHierarchyQueryHandler> logger,
        BattleshipMockDataFacade? mockDataFacade = null)
    {
        _workItemQuery = workItemQuery;
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

        _logger.LogDebug("Loading cached analytical goal hierarchy for {Count} goals: {GoalIds}",
            query.GoalIds.Count, string.Join(", ", query.GoalIds));

        return await _workItemQuery.GetGoalHierarchyAsync(query.GoalIds, cancellationToken);
    }
}
