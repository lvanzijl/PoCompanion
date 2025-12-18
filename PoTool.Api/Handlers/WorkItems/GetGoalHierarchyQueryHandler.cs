using Mediator;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Settings;
using PoTool.Core.WorkItems;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.WorkItems;

/// <summary>
/// Handler for retrieving work items based on configured Goals.
/// </summary>
public class GetGoalHierarchyQueryHandler : IQueryHandler<GetGoalHierarchyQuery, IEnumerable<WorkItemDto>>
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly MockDataProvider _mockDataProvider;

    public GetGoalHierarchyQueryHandler(
        ISettingsRepository settingsRepository,
        IWorkItemRepository workItemRepository,
        MockDataProvider mockDataProvider)
    {
        _settingsRepository = settingsRepository;
        _workItemRepository = workItemRepository;
        _mockDataProvider = mockDataProvider;
    }

    public async ValueTask<IEnumerable<WorkItemDto>> Handle(GetGoalHierarchyQuery query, CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
        
        if (settings == null || settings.DataMode == DataMode.Mock)
        {
            // Return mock data for specified goal IDs
            return _mockDataProvider.GetMockHierarchyForGoals(query.GoalIds);
        }

        // For TFS mode, query from repository
        // Get all work items that are descendants of the specified goals
        var allItems = await _workItemRepository.GetAllAsync(cancellationToken);
        return WorkItemHierarchyHelper.FilterDescendants(query.GoalIds, allItems);
    }
}
