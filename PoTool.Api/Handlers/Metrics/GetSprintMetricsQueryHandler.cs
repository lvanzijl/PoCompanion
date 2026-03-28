using Mediator;
using Microsoft.EntityFrameworkCore;
using PoTool.Api.Adapters;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.Models;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Sprints;
using PoTool.Core.Metrics.Queries;
using PoTool.Core.WorkItems;
using PoTool.Shared.WorkItems;
using PoTool.Shared.Metrics;
using PoTool.Core.WorkItems.Queries;

namespace PoTool.Api.Handlers.Metrics;

/// <summary>
/// Handler for GetSprintMetricsQuery.
/// Reconstructs historical sprint metrics for a sprint window.
/// Planned scope comes from sprint membership at the canonical commitment timestamp,
/// and completed scope comes from the first canonical Done transition within the sprint window.
/// This matches the delivery rules in docs/domain/domain_model.md,
/// docs/domain/rules/sprint_rules.md, docs/domain/rules/metrics_rules.md,
/// and docs/domain/rules/source_rules.md.
/// </summary>
public sealed class GetSprintMetricsQueryHandler : IQueryHandler<GetSprintMetricsQuery, SprintMetricsDto?>
{
    private const string IterationPathFieldRefName = "System.IterationPath";
    private const string StateFieldRefName = "System.State";

    private readonly ISprintRepository _sprintRepository;
    private readonly IWorkItemStateClassificationService _stateClassificationService;
    private readonly ISprintCommitmentService _sprintCommitmentService;
    private readonly ISprintScopeChangeService _sprintScopeChangeService;
    private readonly ISprintCompletionService _sprintCompletionService;
    private readonly ISprintFactService _sprintFactService;
    private readonly SprintScopedWorkItemLoader _workItemLoader;
    private readonly PoToolDbContext _context;
    private readonly ILogger<GetSprintMetricsQueryHandler> _logger;

    public GetSprintMetricsQueryHandler(
        ISprintRepository sprintRepository,
        IWorkItemStateClassificationService stateClassificationService,
        ISprintCommitmentService sprintCommitmentService,
        ISprintScopeChangeService sprintScopeChangeService,
        ISprintCompletionService sprintCompletionService,
        ISprintFactService sprintFactService,
        SprintScopedWorkItemLoader workItemLoader,
        PoToolDbContext context,
        ILogger<GetSprintMetricsQueryHandler> logger)
    {
        _sprintRepository = sprintRepository;
        _stateClassificationService = stateClassificationService;
        _sprintCommitmentService = sprintCommitmentService;
        _sprintScopeChangeService = sprintScopeChangeService;
        _sprintCompletionService = sprintCompletionService;
        _sprintFactService = sprintFactService;
        _workItemLoader = workItemLoader;
        _context = context;
        _logger = logger;
    }

    public GetSprintMetricsQueryHandler(
        IWorkItemRepository repository,
        IProductRepository productRepository,
        ISprintRepository sprintRepository,
        IWorkItemStateClassificationService stateClassificationService,
        ISprintCommitmentService sprintCommitmentService,
        ISprintScopeChangeService sprintScopeChangeService,
        ISprintCompletionService sprintCompletionService,
        ISprintFactService sprintFactService,
        IMediator mediator,
        PoToolDbContext context,
        ILogger<GetSprintMetricsQueryHandler> logger)
        : this(
            sprintRepository,
            stateClassificationService,
            sprintCommitmentService,
            sprintScopeChangeService,
            sprintCompletionService,
            sprintFactService,
            new SprintScopedWorkItemLoader(new RepositoryBackedWorkItemReadProvider(repository), productRepository, mediator),
            context,
            logger)
    {
    }

    public async ValueTask<SprintMetricsDto?> Handle(
        GetSprintMetricsQuery query,
        CancellationToken cancellationToken)
    {
        var iterationPath = query.EffectiveFilter.IterationPath;
        if (string.IsNullOrWhiteSpace(iterationPath))
        {
            return null;
        }

        _logger.LogDebug("Handling GetSprintMetricsQuery for iteration: {IterationPath}", iterationPath);

        var allSprints = await _sprintRepository.GetAllSprintsAsync(cancellationToken);
        var matchingSprint = allSprints.FirstOrDefault(s =>
            s.Path.Equals(iterationPath, StringComparison.OrdinalIgnoreCase));

        if (matchingSprint?.StartUtc == null || matchingSprint.EndUtc == null)
        {
            _logger.LogDebug(
                "Sprint metrics require a dated sprint window; no sprint metadata was found for {IterationPath}",
                iterationPath);
            return null;
        }

        var relevantWorkItems = await _workItemLoader.LoadAsync(query.EffectiveFilter, cancellationToken);

        var workItemIds = relevantWorkItems.Select(wi => wi.TfsId).ToArray();
        var sprintStart = matchingSprint.StartUtc.Value;
        var sprintEnd = matchingSprint.EndUtc.Value;
        var sprintDefinition = matchingSprint.ToDefinition();
        var workItemSnapshotsById = relevantWorkItems.ToSnapshotDictionary();
        var canonicalWorkItemsById = relevantWorkItems.ToDictionary(workItem => workItem.TfsId, workItem => workItem.ToCanonicalWorkItem());
        var commitmentTimestamp = _sprintCommitmentService.GetCommitmentTimestamp(sprintStart);
        var iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>();
        var stateEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>();
        var firstDoneByWorkItem = new Dictionary<int, DateTimeOffset>();
        var addedWorkItemIds = new HashSet<int>();
        IReadOnlyDictionary<(string WorkItemType, string StateName), StateClassification>? stateLookup = null;

        if (workItemIds.Length > 0)
        {
            var allHistoryEvents = await _context.ActivityEventLedgerEntries
                .AsNoTracking()
                .Where(e => workItemIds.Contains(e.WorkItemId)
                            && (e.FieldRefName == IterationPathFieldRefName || e.FieldRefName == StateFieldRefName))
                .ToListAsync(cancellationToken);
            var allHistoryFieldChanges = allHistoryEvents.ToFieldChangeEvents();

            var iterationEvents = allHistoryFieldChanges
                .Where(e => string.Equals(e.FieldRefName, IterationPathFieldRefName, StringComparison.OrdinalIgnoreCase))
                .Where(e => e.TimestampUtc >= sprintStart.UtcDateTime)
                .ToList();
            var stateEvents = allHistoryFieldChanges
                .Where(e => string.Equals(e.FieldRefName, StateFieldRefName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            iterationEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>(iterationEvents.GroupByWorkItemId());
            stateEventsByWorkItem = new Dictionary<int, IReadOnlyList<FieldChangeEvent>>(stateEvents.GroupByWorkItemId());

            var classifications = await _stateClassificationService.GetClassificationsAsync(cancellationToken);
            stateLookup = StateClassificationLookup.Create(classifications.Classifications.ToDomainStateClassifications());
            firstDoneByWorkItem = _sprintCompletionService.BuildFirstDoneByWorkItem(allHistoryFieldChanges, workItemSnapshotsById, stateLookup)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            addedWorkItemIds = _sprintScopeChangeService
                .DetectScopeAdded(sprintDefinition, iterationEventsByWorkItem)
                .Select(scopeChange => scopeChange.WorkItemId)
                .ToHashSet();
        }

        var sprintFact = _sprintFactService.BuildSprintFactResult(
            sprintDefinition,
            canonicalWorkItemsById,
            workItemSnapshotsById,
            iterationEventsByWorkItem,
            stateEventsByWorkItem,
            stateLookup,
            nextSprintPath: null);

        var committedWorkItemIds = _sprintCommitmentService.BuildCommittedWorkItemIds(
                workItemSnapshotsById,
                iterationEventsByWorkItem,
                sprintDefinition.Path,
                commitmentTimestamp)
            .ToHashSet();

        var sprintScopeIds = committedWorkItemIds
            .Concat(addedWorkItemIds)
            .Distinct()
            .ToHashSet();

        var sprintScopeWorkItems = relevantWorkItems
            .Where(wi => sprintScopeIds.Contains(wi.TfsId))
            .ToList();

        var completedItems = sprintScopeWorkItems
            .Where(wi => firstDoneByWorkItem.TryGetValue(wi.TfsId, out var firstDoneTimestamp)
                         && firstDoneTimestamp >= sprintStart
                         && firstDoneTimestamp <= sprintEnd)
            .ToList();

        var completedPBIs = completedItems.Count(wi =>
            IsPbiType(wi.Type));

        var completedBugs = completedItems.Count(wi =>
            wi.Type.Equals(WorkItemType.Bug, StringComparison.OrdinalIgnoreCase));

        var completedTasks = completedItems.Count(wi =>
            wi.Type.Equals(WorkItemType.Task, StringComparison.OrdinalIgnoreCase));

        var sprintName = string.IsNullOrWhiteSpace(matchingSprint.Name)
            ? iterationPath
            : matchingSprint.Name;
        var completedStoryPoints = (int)Math.Round(sprintFact.DeliveredStoryPoints, MidpointRounding.AwayFromZero);
        var plannedStoryPoints = (int)Math.Round(sprintFact.CommittedStoryPoints, MidpointRounding.AwayFromZero);

        var metrics = new SprintMetricsDto(
            IterationPath: iterationPath,
            SprintName: sprintName,
            StartDate: sprintStart,
            EndDate: sprintEnd,
            CompletedStoryPoints: completedStoryPoints,
            PlannedStoryPoints: plannedStoryPoints,
            CompletedWorkItemCount: completedItems.Count,
            TotalWorkItemCount: sprintScopeWorkItems.Count,
            CompletedPBIs: completedPBIs,
            CompletedBugs: completedBugs,
            CompletedTasks: completedTasks
        );

        _logger.LogInformation(
            "Sprint metrics calculated for {IterationPath}: delivered {CompletedPoints} story points from {CompletedCount} scope items against {PlannedPoints} committed story points",
            iterationPath,
            sprintFact.DeliveredStoryPoints,
            completedItems.Count,
            sprintFact.CommittedStoryPoints);

        return metrics;
    }

    private static bool IsPbiType(string workItemType)
    {
        return workItemType.Equals(WorkItemType.Pbi, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.PbiShort, StringComparison.OrdinalIgnoreCase)
            || workItemType.Equals(WorkItemType.UserStory, StringComparison.OrdinalIgnoreCase);
    }
}
