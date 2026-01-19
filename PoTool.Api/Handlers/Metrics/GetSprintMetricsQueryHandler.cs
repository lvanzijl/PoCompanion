using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Metrics;
using PoTool.Shared.WorkItems;
using PoTool.Core.Metrics.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetSprintMetricsQuery.
/// Calculates metrics for a specific sprint based on work items.
/// </summary>
public sealed class GetSprintMetricsQueryHandler : IQueryHandler<GetSprintMetricsQuery, SprintMetricsDto?>
{
    private readonly IWorkItemRepository _repository;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly ILogger<GetSprintMetricsQueryHandler> _logger;

    public GetSprintMetricsQueryHandler(
        IWorkItemRepository repository,
        IWorkItemStateClassificationService stateClassificationService,
        ILogger<GetSprintMetricsQueryHandler> logger)
    {
        _repository = repository;
        _stateClassificationService = stateClassificationService;
        _logger = logger;
    }

    public async ValueTask<SprintMetricsDto?> Handle(
        GetSprintMetricsQuery query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Handling GetSprintMetricsQuery for iteration: {IterationPath}", query.IterationPath);

        var allWorkItems = await _repository.GetAllAsync(cancellationToken);

        // Filter work items for this specific iteration
        var sprintWorkItems = allWorkItems
            .Where(wi => wi.IterationPath.Equals(query.IterationPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!sprintWorkItems.Any())
        {
            _logger.LogDebug("No work items found for iteration: {IterationPath}", query.IterationPath);
            return null;
        }

        // Calculate metrics
        var completedItems = new List<WorkItemDto>();
        foreach (var wi in sprintWorkItems)
        {
            if (await _stateClassificationService.IsDoneStateAsync(wi.Type, wi.State, cancellationToken))
            {
                completedItems.Add(wi);
            }
        }

        var completedStoryPoints = completedItems
            .Where(wi => wi.Effort.HasValue)
            .Sum(wi => wi.Effort!.Value);

        var plannedStoryPoints = sprintWorkItems
            .Where(wi => wi.Effort.HasValue)
            .Sum(wi => wi.Effort!.Value);

        var completedPBIs = completedItems.Count(wi =>
            wi.Type.Equals("Product Backlog Item", StringComparison.OrdinalIgnoreCase) ||
            wi.Type.Equals("PBI", StringComparison.OrdinalIgnoreCase) ||
            wi.Type.Equals("User Story", StringComparison.OrdinalIgnoreCase));

        var completedBugs = completedItems.Count(wi =>
            wi.Type.Equals("Bug", StringComparison.OrdinalIgnoreCase));

        var completedTasks = completedItems.Count(wi =>
            wi.Type.Equals("Task", StringComparison.OrdinalIgnoreCase));

        // Extract sprint name from iteration path (last segment)
        // Handle both backslash and forward slash separators
        var separators = new[] { '\\', '/' };
        var sprintName = query.IterationPath.Split(separators, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? query.IterationPath;

        var metrics = new SprintMetricsDto(
            IterationPath: query.IterationPath,
            SprintName: sprintName,
            StartDate: null, // Could be extracted from TFS iteration data if available
            EndDate: null,   // Could be extracted from TFS iteration data if available
            CompletedStoryPoints: completedStoryPoints,
            PlannedStoryPoints: plannedStoryPoints,
            CompletedWorkItemCount: completedItems.Count,
            TotalWorkItemCount: sprintWorkItems.Count,
            CompletedPBIs: completedPBIs,
            CompletedBugs: completedBugs,
            CompletedTasks: completedTasks
        );

        _logger.LogInformation(
            "Sprint metrics calculated for {IterationPath}: {CompletedPoints} of {PlannedPoints} story points completed",
            query.IterationPath, completedStoryPoints, plannedStoryPoints);

        return metrics;
    }
}
