using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Forecasting.Models;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Core.WorkItems;
using PoTool.Shared.Planning;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Builds read-only project planning summaries from existing project, work-item, sprint, and projection data.
/// </summary>
public sealed class ProjectPlanningSummaryService
{
    private const int VisibleSprintWindow = 3;
    private const int HistoricalSprintsForCalibration = 6;

    private readonly PoToolDbContext _context;
    private readonly IProjectRepository _projectRepository;
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IVelocityCalibrationService _velocityCalibrationService;

    public ProjectPlanningSummaryService(
        PoToolDbContext context,
        IProjectRepository projectRepository,
        IWorkItemReadProvider workItemReadProvider,
        IVelocityCalibrationService velocityCalibrationService)
    {
        _context = context;
        _projectRepository = projectRepository;
        _workItemReadProvider = workItemReadProvider;
        _velocityCalibrationService = velocityCalibrationService;
    }

    public async Task<ProjectPlanningSummaryDto?> GetSummaryAsync(string aliasOrId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetProjectByAliasOrIdAsync(aliasOrId, cancellationToken);
        if (project == null)
        {
            return null;
        }

        var products = (await _projectRepository.GetProjectProductsAsync(aliasOrId, cancellationToken)).ToList();
        if (products.Count == 0)
        {
            return new ProjectPlanningSummaryDto(
                project.Alias,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                Array.Empty<ProjectPlanningProductSummaryDto>());
        }

        var rootIds = products
            .SelectMany(product => product.BacklogRootWorkItemIds)
            .Distinct()
            .ToArray();

        var allWorkItems = rootIds.Length == 0
            ? Array.Empty<WorkItemDto>()
            : (await _workItemReadProvider.GetByRootIdsAsync(rootIds, cancellationToken)).ToArray();

        var productIds = products.Select(product => product.Id).ToArray();
        var primaryTeamIds = products
            .Where(product => product.TeamIds.Count > 0)
            .Select(product => product.TeamIds[0])
            .Distinct()
            .ToArray();

        var visibleSprintsByTeamId = await LoadVisibleSprintsByTeamIdAsync(primaryTeamIds, cancellationToken);
        var completedSprintsByTeamId = await LoadCompletedCalibrationSprintsByTeamIdAsync(primaryTeamIds, cancellationToken);

        var completedSprintIds = completedSprintsByTeamId.Values
            .SelectMany(static sprints => sprints)
            .Select(static sprint => sprint.Id)
            .Distinct()
            .ToArray();

        var projections = completedSprintIds.Length == 0
            ? Array.Empty<Persistence.Entities.SprintMetricsProjectionEntity>()
            : await _context.SprintMetricsProjections
                .AsNoTracking()
                .Where(projection => productIds.Contains(projection.ProductId) && completedSprintIds.Contains(projection.SprintId))
                .ToArrayAsync(cancellationToken);

        var doneOrRemovedStates = await _context.WorkItemStateClassifications
            .AsNoTracking()
            .Where(classification =>
                classification.Classification == (int)StateClassification.Done ||
                classification.Classification == (int)StateClassification.Removed)
            .Select(classification => new
            {
                Type = classification.WorkItemType.ToLower(),
                State = classification.StateName.ToLower()
            })
            .ToArrayAsync(cancellationToken);

        var doneOrRemovedStateSet = doneOrRemovedStates
            .Select(static classification => (classification.Type, classification.State))
            .ToHashSet();

        var productSummaries = new List<ProjectPlanningProductSummaryDto>(products.Count);
        var totalWindowCapacity = 0d;

        foreach (var product in products)
        {
            var productWorkItems = WorkItemHierarchyHelper.FilterDescendants(product.BacklogRootWorkItemIds, allWorkItems);
            var visibleSprints = product.TeamIds.Count == 0
                ? Array.Empty<Persistence.Entities.SprintEntity>()
                : visibleSprintsByTeamId.GetValueOrDefault(product.TeamIds[0]) ?? Array.Empty<Persistence.Entities.SprintEntity>();

            var visibleSprintPaths = visibleSprints
                .Select(static sprint => NormalizeIterationPath(sprint.Path))
                .ToHashSet();

            var totalEpics = productWorkItems.Count(static workItem => IsRoadmapEpic(workItem));
            var totalPbis = productWorkItems.Count(workItem => IsActivePbi(workItem, doneOrRemovedStateSet));
            var plannedPbis = productWorkItems.Count(workItem =>
                IsActivePbi(workItem, doneOrRemovedStateSet) &&
                visibleSprintPaths.Contains(NormalizeIterationPath(workItem.IterationPath)));

            var totalEffort = productWorkItems
                .Where(workItem => IsActivePbi(workItem, doneOrRemovedStateSet))
                .Sum(static workItem => workItem.Effort ?? 0);

            var plannedEffort = productWorkItems
                .Where(workItem =>
                    IsActivePbi(workItem, doneOrRemovedStateSet) &&
                    visibleSprintPaths.Contains(NormalizeIterationPath(workItem.IterationPath)))
                .Sum(static workItem => workItem.Effort ?? 0);

            var completedSprints = product.TeamIds.Count == 0
                ? Array.Empty<Persistence.Entities.SprintEntity>()
                : completedSprintsByTeamId.GetValueOrDefault(product.TeamIds[0]) ?? Array.Empty<Persistence.Entities.SprintEntity>();

            var capacityPerSprint = CalibrateCapacityPerSprint(product.Id, completedSprints, projections);
            totalWindowCapacity += capacityPerSprint * visibleSprints.Length;

            productSummaries.Add(new ProjectPlanningProductSummaryDto(
                product.Id,
                product.Name,
                totalEpics,
                totalPbis,
                plannedPbis,
                Math.Max(0, totalPbis - plannedPbis),
                totalEffort,
                plannedEffort,
                capacityPerSprint));
        }

        var summary = new ProjectPlanningSummaryDto(
            project.Alias,
            productSummaries.Count,
            productSummaries.Sum(static product => product.EpicCount),
            productSummaries.Sum(static product => product.TotalPBIs),
            productSummaries.Sum(static product => product.PlannedPBIs),
            productSummaries.Sum(static product => product.UnplannedPBIs),
            productSummaries.Sum(static product => product.TotalEffort),
            productSummaries.Sum(static product => product.PlannedEffort),
            productSummaries.Sum(static product => product.CapacityPerSprint),
            productSummaries.Sum(static product => product.PlannedEffort) > totalWindowCapacity,
            productSummaries
                .OrderByDescending(static product => product.TotalEffort)
                .ThenBy(static product => product.ProductName)
                .ToArray());

        return summary;
    }

    private async Task<Dictionary<int, Persistence.Entities.SprintEntity[]>> LoadVisibleSprintsByTeamIdAsync(
        IReadOnlyCollection<int> teamIds,
        CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
        {
            return [];
        }

        var nowUtc = DateTime.UtcNow;
        var sprints = await _context.Sprints
            .AsNoTracking()
            .Where(sprint => teamIds.Contains(sprint.TeamId) && sprint.EndDateUtc.HasValue && sprint.EndDateUtc.Value > nowUtc)
            .OrderBy(sprint => sprint.TeamId)
            .ThenBy(sprint => sprint.StartDateUtc)
            .ToArrayAsync(cancellationToken);

        return sprints
            .GroupBy(static sprint => sprint.TeamId)
            .ToDictionary(
                static group => group.Key,
                static group => group.Take(VisibleSprintWindow).ToArray());
    }

    private async Task<Dictionary<int, Persistence.Entities.SprintEntity[]>> LoadCompletedCalibrationSprintsByTeamIdAsync(
        IReadOnlyCollection<int> teamIds,
        CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
        {
            return [];
        }

        var nowUtc = DateTime.UtcNow;
        var sprints = await _context.Sprints
            .AsNoTracking()
            .Where(sprint => teamIds.Contains(sprint.TeamId) && sprint.EndDateUtc.HasValue && sprint.EndDateUtc.Value <= nowUtc)
            .OrderBy(sprint => sprint.TeamId)
            .ThenByDescending(sprint => sprint.EndDateUtc)
            .ToArrayAsync(cancellationToken);

        return sprints
            .GroupBy(static sprint => sprint.TeamId)
            .ToDictionary(
                static group => group.Key,
                static group => group.Take(HistoricalSprintsForCalibration).ToArray());
    }

    private double CalibrateCapacityPerSprint(
        int productId,
        IReadOnlyCollection<Persistence.Entities.SprintEntity> completedSprints,
        IReadOnlyCollection<Persistence.Entities.SprintMetricsProjectionEntity> projections)
    {
        if (completedSprints.Count == 0)
        {
            return 0;
        }

        var projectionsBySprintId = projections
            .Where(projection => projection.ProductId == productId)
            .GroupBy(static projection => projection.SprintId)
            .ToDictionary(static group => group.Key, static group => group.ToArray());

        var calibration = _velocityCalibrationService.Calibrate(
            completedSprints
                .Select(sprint =>
                {
                    var sprintProjections = projectionsBySprintId.GetValueOrDefault(sprint.Id) ?? Array.Empty<Persistence.Entities.SprintMetricsProjectionEntity>();
                    return new VelocityCalibrationSample(
                        sprint.Name,
                        sprintProjections.Sum(static projection => projection.PlannedStoryPoints),
                        sprintProjections.Sum(static projection => projection.DerivedStoryPoints),
                        sprintProjections.Sum(static projection => projection.CompletedPbiStoryPoints),
                        sprintProjections.Sum(static projection => projection.CompletedPbiEffort));
                })
                .ToList());

        return calibration.MedianVelocity;
    }

    private static bool IsRoadmapEpic(WorkItemDto workItem) =>
        string.Equals(workItem.Type?.Trim(), "Epic", StringComparison.OrdinalIgnoreCase) &&
        HasRoadmapTag(workItem.Tags);

    private static bool IsActivePbi(WorkItemDto workItem, IReadOnlySet<(string type, string state)> doneOrRemovedStates)
    {
        if (!string.Equals(workItem.Type?.Trim(), "PBI", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(workItem.Type?.Trim(), "Product Backlog Item", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedType = (workItem.Type ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedState = workItem.State?.Trim().ToLowerInvariant() ?? string.Empty;
        return !doneOrRemovedStates.Contains((normalizedType, normalizedState));
    }

    private static bool HasRoadmapTag(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return false;
        }

        return tags
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(tag => string.Equals(tag, "roadmap", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeIterationPath(string path) =>
        path.Replace('/', '\\').Trim();
}
