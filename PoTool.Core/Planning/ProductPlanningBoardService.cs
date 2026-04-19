using PoTool.Core.Contracts;
using PoTool.Core.Domain.Planning;
using PoTool.Shared.Planning;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

namespace PoTool.Core.Planning;

/// <summary>
/// Builds deterministic product planning board read models and executes planning operations in memory.
/// </summary>
public interface IProductPlanningBoardService
{
    ValueTask<ProductPlanningBoardDto?> BuildPlanningBoardAsync(int productId, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> GetPlanningBoardAsync(int productId, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ResetPlanningBoardAsync(int productId, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteMoveEpicBySprintsAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteAdjustSpacingBeforeAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteRunInParallelAsync(int productId, int epicId, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteReturnToMainAsync(int productId, int epicId, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteReorderEpicAsync(int productId, int epicId, int targetRoadmapOrder, CancellationToken cancellationToken = default);

    ValueTask<ProductPlanningBoardDto?> ExecuteShiftPlanAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default);
}

/// <summary>
/// Application-layer bridge between active product/work-item inputs and the planning engine.
/// </summary>
public sealed class ProductPlanningBoardService : IProductPlanningBoardService
{
    private static readonly DateOnly LegacyInvalidStartDateCutoff = new(2021, 4, 19);

    private readonly IProductRepository _productRepository;
    private readonly IWorkItemReadProvider _workItemReadProvider;
    private readonly IProductPlanningSessionStore _sessionStore;
    private readonly IProductPlanningIntentStore _intentStore;
    private readonly ISprintRepository _sprintRepository;
    private readonly ITfsClient _tfsClient;
    private readonly PlanningRecomputeService _recomputeService;
    private readonly PlanningOperationService _operationService;

    public ProductPlanningBoardService(
        IProductRepository productRepository,
        IWorkItemReadProvider workItemReadProvider,
        IProductPlanningSessionStore sessionStore,
        IProductPlanningIntentStore intentStore,
        ISprintRepository sprintRepository,
        ITfsClient tfsClient)
        : this(
            productRepository,
            workItemReadProvider,
            sessionStore,
            intentStore,
            sprintRepository,
            tfsClient,
            new PlanningRecomputeService(),
            new PlanningOperationService())
    {
    }

    internal ProductPlanningBoardService(
        IProductRepository productRepository,
        IWorkItemReadProvider workItemReadProvider,
        IProductPlanningSessionStore sessionStore,
        IProductPlanningIntentStore intentStore,
        ISprintRepository sprintRepository,
        ITfsClient tfsClient,
        PlanningRecomputeService recomputeService,
        PlanningOperationService operationService)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _workItemReadProvider = workItemReadProvider ?? throw new ArgumentNullException(nameof(workItemReadProvider));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _intentStore = intentStore ?? throw new ArgumentNullException(nameof(intentStore));
        _sprintRepository = sprintRepository ?? throw new ArgumentNullException(nameof(sprintRepository));
        _tfsClient = tfsClient ?? throw new ArgumentNullException(nameof(tfsClient));
        _recomputeService = recomputeService ?? throw new ArgumentNullException(nameof(recomputeService));
        _operationService = operationService ?? throw new ArgumentNullException(nameof(operationService));
    }

    public ValueTask<ProductPlanningBoardDto?> BuildPlanningBoardAsync(int productId, CancellationToken cancellationToken = default)
    {
        return BuildAsync(productId, reset: false, cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> GetPlanningBoardAsync(int productId, CancellationToken cancellationToken = default)
    {
        return BuildPlanningBoardAsync(productId, cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ResetPlanningBoardAsync(int productId, CancellationToken cancellationToken = default)
    {
        return BuildAsync(productId, reset: true, cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteMoveEpicBySprintsAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, state => _operationService.MoveEpicBySprints(state, epicId, deltaSprints), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteAdjustSpacingBeforeAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, state => _operationService.AdjustSpacingBefore(state, epicId, deltaSprints), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteRunInParallelAsync(int productId, int epicId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, state => _operationService.RunInParallel(state, epicId), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteReturnToMainAsync(int productId, int epicId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, state => _operationService.ReturnToMain(state, epicId), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteReorderEpicAsync(int productId, int epicId, int targetRoadmapOrder, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, state => _operationService.ReorderEpic(state, epicId, targetRoadmapOrder), cancellationToken);
    }

    public ValueTask<ProductPlanningBoardDto?> ExecuteShiftPlanAsync(int productId, int epicId, int deltaSprints, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(productId, state => _operationService.ShiftPlan(state, epicId, deltaSprints), cancellationToken);
    }

    private async ValueTask<ProductPlanningBoardDto?> BuildAsync(
        int productId,
        bool reset,
        CancellationToken cancellationToken)
    {
        var planningContext = await BuildPlanningContextAsync(productId, cancellationToken);
        if (planningContext is null)
        {
            return null;
        }

        if (reset)
        {
            _sessionStore.Reset(productId);
        }

        var state = GetOrLoadSessionState(planningContext);
        return CreateReadModel(planningContext, state, Array.Empty<PlanningValidationIssue>(), Array.Empty<int>(), Array.Empty<int>());
    }

    private async ValueTask<ProductPlanningBoardDto?> ExecuteAsync(
        int productId,
        Func<PlanningState, PlanningOperationResult> execute,
        CancellationToken cancellationToken)
    {
        var planningContext = await BuildPlanningContextAsync(productId, cancellationToken);
        if (planningContext is null)
        {
            return null;
        }

        var currentState = GetOrLoadSessionState(planningContext);
        var result = execute(currentState);

        await PersistPlanningIntentAsync(planningContext, result.State, cancellationToken);
        _sessionStore.SetState(productId, result.State);

        return CreateReadModel(
            planningContext,
            result.State,
            result.ValidationIssues,
            result.ChangedEpicIds,
            result.AffectedEpicIds);
    }

    private async ValueTask<PlanningContext?> BuildPlanningContextAsync(int productId, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetProductByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var rootIds = product.BacklogRootWorkItemIds
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();

        var workItems = rootIds.Length == 0
            ? Array.Empty<WorkItemDto>()
            : (await _workItemReadProvider.GetByRootIdsAsync(rootIds, cancellationToken)).ToArray();

        var roadmapEpics = workItems
            .Where(static workItem => IsRoadmapEpic(workItem))
            .OrderBy(static workItem => workItem.BacklogPriority ?? double.MaxValue)
            .ThenBy(static workItem => workItem.TfsId)
            .Select((workItem, index) => new ActiveRoadmapEpic(
                workItem.TfsId,
                string.IsNullOrWhiteSpace(workItem.Title) ? $"Epic {workItem.TfsId}" : workItem.Title,
                index + 1,
                workItem))
            .ToArray();

        var activeEpicIds = roadmapEpics.Select(static epic => epic.EpicId).ToArray();
        await _intentStore.DeleteMissingEpicsAsync(product.Id, activeEpicIds, cancellationToken);

        var persistedIntents = await _intentStore.GetByProductAsync(product.Id, cancellationToken);
        var calendarResolution = await ResolveCalendarAsync(product, cancellationToken);

        var recoveryTimestampUtc = DateTime.UtcNow;
        var recoveredIntents = new List<ProductPlanningIntentRecord>();
        var normalizedRecoveries = new List<PlanningDateWriteRequest>();
        var boardDiagnostics = new List<PlanningBoardDiagnosticDto>();
        var stateEpics = new List<PlanningEpicState>(roadmapEpics.Length);
        var persistedIntentByEpicId = persistedIntents.ToDictionary(static intent => intent.EpicId);
        var operationalStateByEpicId = new Dictionary<int, PlanningEpicOperationalState>(roadmapEpics.Length);

        if (calendarResolution.Calendar is null && !string.IsNullOrWhiteSpace(calendarResolution.FailureReason))
        {
            boardDiagnostics.Add(CreateBoardDiagnostic(
                "Error",
                "CalendarResolutionFailure",
                calendarResolution.FailureReason,
                isBlocking: true));
        }

        foreach (var (epic, index) in roadmapEpics.Select((epic, index) => (epic, index)))
        {
            if (persistedIntentByEpicId.TryGetValue(epic.EpicId, out var persistedIntent))
            {
                var persistedStartIndex = ResolvePersistedStartIndex(epic.EpicId, persistedIntent, calendarResolution);
                stateEpics.Add(new PlanningEpicState(
                    epic.EpicId,
                    epic.RoadmapOrder,
                    persistedStartIndex,
                    0,
                    persistedIntent.DurationInSprints,
                    0));
                operationalStateByEpicId[epic.EpicId] = CreatePersistedOperationalState(epic, persistedIntent, calendarResolution);
                continue;
            }

            if (TryRecoverIntent(
                    product.Id,
                    epic,
                    calendarResolution,
                    recoveryTimestampUtc,
                    out var recoveredIntent,
                    out var recoveredStartIndex,
                    out var planningDateWriteRequest))
            {
                recoveredIntents.Add(recoveredIntent!);
                if (planningDateWriteRequest is not null)
                {
                    normalizedRecoveries.Add(planningDateWriteRequest.Value);
                }

                stateEpics.Add(new PlanningEpicState(
                    epic.EpicId,
                    epic.RoadmapOrder,
                    recoveredStartIndex,
                    0,
                    recoveredIntent!.DurationInSprints,
                    0));
                operationalStateByEpicId[epic.EpicId] = CreateRecoveredOperationalState(epic, recoveredIntent, planningDateWriteRequest is not null);
                continue;
            }

            stateEpics.Add(new PlanningEpicState(
                epic.EpicId,
                epic.RoadmapOrder,
                index,
                0,
                1,
                0));
            operationalStateByEpicId[epic.EpicId] = CreateBootstrapOperationalState(epic, calendarResolution);
        }

        if (recoveredIntents.Count > 0)
        {
            await _intentStore.UpsertForProductAsync(product.Id, recoveredIntents, cancellationToken);
        }

        foreach (var normalizedRecovery in normalizedRecoveries)
        {
            await _tfsClient.UpdateWorkItemPlanningDatesAsync(
                normalizedRecovery.EpicId,
                normalizedRecovery.StartDate,
                normalizedRecovery.TargetDate,
                cancellationToken);
        }

        var baseState = stateEpics.Count == 0
            ? PlanningState.Empty
            : _recomputeService.RecomputeFrom(new PlanningState(stateEpics), 0);

        return new PlanningContext(
            product.Id,
            product.Name,
            roadmapEpics,
            baseState,
            calendarResolution,
            operationalStateByEpicId,
            boardDiagnostics);
    }

    private PlanningState GetOrLoadSessionState(PlanningContext planningContext)
    {
        if (_sessionStore.TryGetState(planningContext.ProductId, out var sessionState) &&
            SessionStateMatchesActiveScope(sessionState, planningContext.Epics))
        {
            return sessionState;
        }

        _sessionStore.SetState(planningContext.ProductId, planningContext.BaseState);
        return planningContext.BaseState;
    }

    private async Task PersistPlanningIntentAsync(
        PlanningContext planningContext,
        PlanningState state,
        CancellationToken cancellationToken)
    {
        var calendar = RequireCalendar(planningContext.CalendarResolution, planningContext.ProductId);
        var updatedAtUtc = DateTime.UtcNow;
        var intents = state.Epics
            .Select(epic => MapToIntentRecord(planningContext.ProductId, epic, calendar, updatedAtUtc))
            .ToArray();

        await _intentStore.UpsertForProductAsync(planningContext.ProductId, intents, cancellationToken);
        await _intentStore.DeleteMissingEpicsAsync(
            planningContext.ProductId,
            planningContext.Epics.Select(static epic => epic.EpicId).ToArray(),
            cancellationToken);

        foreach (var intent in intents)
        {
            var projectionStatus = TryCreatePlanningDateWriteRequest(intent.EpicId, intent, calendar, out var writeRequest);
            if (projectionStatus != PlanningProjectionStatus.Success)
            {
                throw new InvalidOperationException(
                    $"Unable to project planning dates for epic {intent.EpicId} in product {planningContext.ProductId}. {DescribeProjectionFailure(projectionStatus, intent.StartSprintStartDateUtc)}");
            }

            await _tfsClient.UpdateWorkItemPlanningDatesAsync(
                writeRequest.EpicId,
                writeRequest.StartDate,
                writeRequest.TargetDate,
                cancellationToken);
        }
    }

    private static bool SessionStateMatchesActiveScope(PlanningState sessionState, IReadOnlyList<ActiveRoadmapEpic> activeEpics)
    {
        if (sessionState.Epics.Count != activeEpics.Count)
        {
            return false;
        }

        var activeEpicIds = activeEpics.Select(static epic => epic.EpicId).OrderBy(static id => id).ToArray();
        var sessionEpicIds = sessionState.Epics.Select(static epic => epic.EpicId).OrderBy(static id => id).ToArray();
        return activeEpicIds.SequenceEqual(sessionEpicIds);
    }

    private static ProductSprintCalendar RequireCalendar(ProductSprintCalendarResolution calendarResolution, int productId)
    {
        if (calendarResolution.Calendar is null)
        {
            throw new InvalidOperationException(
                $"Product {productId} does not have an unambiguous sprint calendar required for durable planning intent. {calendarResolution.FailureReason}");
        }

        return calendarResolution.Calendar;
    }

    private int ResolvePersistedStartIndex(
        int epicId,
        ProductPlanningIntentRecord intent,
        ProductSprintCalendarResolution calendarResolution)
    {
        var calendar = RequireCalendar(calendarResolution, intent.ProductId);
        if (!calendar.StartIndexByDate.TryGetValue(intent.StartSprintStartDateUtc.Date, out var startIndex))
        {
            throw new InvalidOperationException(
                $"Persisted planning intent for epic {epicId} in product {intent.ProductId} references missing sprint boundary {intent.StartSprintStartDateUtc:yyyy-MM-dd}."
            );
        }

        return startIndex;
    }

    private static ProductPlanningIntentRecord MapToIntentRecord(
        int productId,
        PlanningEpicState epic,
        ProductSprintCalendar calendar,
        DateTime updatedAtUtc)
    {
        if (epic.PlannedStartSprintIndex < 0 || epic.PlannedStartSprintIndex >= calendar.Sprints.Count)
        {
            throw new InvalidOperationException(
                $"Epic {epic.EpicId} references missing sprint index {epic.PlannedStartSprintIndex} in the canonical calendar.");
        }

        if (epic.DurationInSprints <= 0)
        {
            throw new InvalidOperationException($"Epic {epic.EpicId} has invalid duration {epic.DurationInSprints}.");
        }

        return new ProductPlanningIntentRecord(
            productId,
            epic.EpicId,
            calendar.Sprints[epic.PlannedStartSprintIndex].StartDateUtc,
            epic.DurationInSprints,
            RecoveryStatus: null,
            UpdatedAtUtc: updatedAtUtc);
    }

    private static PlanningProjectionStatus TryCreatePlanningDateWriteRequest(
        int epicId,
        ProductPlanningIntentRecord intent,
        ProductSprintCalendar calendar,
        out PlanningDateWriteRequest writeRequest)
    {
        writeRequest = default;

        if (!calendar.StartIndexByDate.TryGetValue(intent.StartSprintStartDateUtc.Date, out var startIndex))
        {
            return PlanningProjectionStatus.MissingStartBoundary;
        }

        var finalSprintIndex = startIndex + intent.DurationInSprints - 1;
        if (finalSprintIndex < startIndex || finalSprintIndex >= calendar.Sprints.Count)
        {
            return PlanningProjectionStatus.InsufficientFutureSprintCoverage;
        }

        var startSprint = calendar.Sprints[startIndex];
        var finalSprint = calendar.Sprints[finalSprintIndex];
        writeRequest = new PlanningDateWriteRequest(
            epicId,
            DateOnly.FromDateTime(startSprint.StartDateUtc),
            DateOnly.FromDateTime(finalSprint.EndExclusiveDateUtc.AddDays(-1)));
        return PlanningProjectionStatus.Success;
    }

    private bool TryRecoverIntent(
        int productId,
        ActiveRoadmapEpic epic,
        ProductSprintCalendarResolution calendarResolution,
        DateTime recoveredAtUtc,
        out ProductPlanningIntentRecord? recoveredIntent,
        out int recoveredStartIndex,
        out PlanningDateWriteRequest? planningDateWriteRequest)
    {
        recoveredIntent = null;
        recoveredStartIndex = -1;
        planningDateWriteRequest = null;

        var calendar = calendarResolution.Calendar;
        if (calendar is null)
        {
            return false;
        }

        var startDate = epic.WorkItem.StartDate;
        var targetDate = epic.WorkItem.TargetDate;
        if (!startDate.HasValue || !targetDate.HasValue)
        {
            return false;
        }

        var startDateOnly = DateOnly.FromDateTime(startDate.Value.UtcDateTime.Date);
        var targetDateOnly = DateOnly.FromDateTime(targetDate.Value.UtcDateTime.Date);

        if (startDateOnly < LegacyInvalidStartDateCutoff || targetDateOnly < startDateOnly)
        {
            return false;
        }

        if (!TryFindSprintContainingDate(calendar, startDateOnly, out var startSprintIndex) ||
            !TryFindSprintContainingDate(calendar, targetDateOnly, out var endSprintIndex))
        {
            return false;
        }

        var durationInSprints = endSprintIndex - startSprintIndex + 1;
        if (durationInSprints < 1)
        {
            return false;
        }

        var startSprint = calendar.Sprints[startSprintIndex];
        var endSprint = calendar.Sprints[endSprintIndex];
        var exactRecovery =
            startDateOnly == DateOnly.FromDateTime(startSprint.StartDateUtc) &&
            targetDateOnly == DateOnly.FromDateTime(endSprint.EndExclusiveDateUtc.AddDays(-1));

        var recoveryStatus = exactRecovery
            ? ProductPlanningRecoveryStatus.RecoveredExact
            : ProductPlanningRecoveryStatus.RecoveredWithNormalization;

        recoveredIntent = new ProductPlanningIntentRecord(
            productId,
            epic.EpicId,
            startSprint.StartDateUtc,
            durationInSprints,
            recoveryStatus,
            recoveredAtUtc);
        recoveredStartIndex = startSprintIndex;

        if (!exactRecovery)
        {
            planningDateWriteRequest = new PlanningDateWriteRequest(
                epic.EpicId,
                DateOnly.FromDateTime(startSprint.StartDateUtc),
                DateOnly.FromDateTime(endSprint.EndExclusiveDateUtc.AddDays(-1)));
        }

        return true;
    }

    private static bool TryFindSprintContainingDate(ProductSprintCalendar calendar, DateOnly date, out int sprintIndex)
    {
        for (var index = 0; index < calendar.Sprints.Count; index++)
        {
            var sprint = calendar.Sprints[index];
            var startDate = DateOnly.FromDateTime(sprint.StartDateUtc);
            var endDateInclusive = DateOnly.FromDateTime(sprint.EndExclusiveDateUtc.AddDays(-1));
            if (date >= startDate && date <= endDateInclusive)
            {
                sprintIndex = index;
                return true;
            }
        }

        sprintIndex = -1;
        return false;
    }

    private async Task<ProductSprintCalendarResolution> ResolveCalendarAsync(ProductDto product, CancellationToken cancellationToken)
    {
        if (product.TeamIds.Count == 0)
        {
            return ProductSprintCalendarResolution.Failed("Product has no linked teams.");
        }

        var windows = new Dictionary<(DateTime Start, DateTime EndExclusive), ProductSprintWindow>();
        foreach (var teamId in product.TeamIds.Distinct().OrderBy(static teamId => teamId))
        {
            var teamSprints = await _sprintRepository.GetSprintsForTeamAsync(teamId, cancellationToken);
            foreach (var sprint in teamSprints)
            {
                if (!sprint.StartUtc.HasValue || !sprint.EndUtc.HasValue)
                {
                    continue;
                }

                var startDateUtc = sprint.StartUtc.Value.UtcDateTime.Date;
                var endExclusiveDateUtc = sprint.EndUtc.Value.UtcDateTime.Date;
                if (endExclusiveDateUtc <= startDateUtc)
                {
                    return ProductSprintCalendarResolution.Failed(
                        $"Sprint '{sprint.Name}' for team {teamId} has invalid boundaries {startDateUtc:yyyy-MM-dd}..{endExclusiveDateUtc:yyyy-MM-dd}.");
                }

                var key = (startDateUtc, endExclusiveDateUtc);
                if (!windows.ContainsKey(key))
                {
                    windows.Add(key, new ProductSprintWindow(startDateUtc, endExclusiveDateUtc));
                }
            }
        }

        if (windows.Count == 0)
        {
            return ProductSprintCalendarResolution.Failed("No sprint windows with non-null boundaries were available for the product.");
        }

        var orderedWindows = windows.Values
            .OrderBy(static window => window.StartDateUtc)
            .ThenBy(static window => window.EndExclusiveDateUtc)
            .ToArray();

        for (var index = 1; index < orderedWindows.Length; index++)
        {
            var previous = orderedWindows[index - 1];
            var current = orderedWindows[index];
            if (current.StartDateUtc < previous.EndExclusiveDateUtc)
            {
                return ProductSprintCalendarResolution.Failed(
                    $"Product sprint calendar is ambiguous because {previous.StartDateUtc:yyyy-MM-dd}..{previous.EndExclusiveDateUtc:yyyy-MM-dd} overlaps {current.StartDateUtc:yyyy-MM-dd}..{current.EndExclusiveDateUtc:yyyy-MM-dd}.");
            }
        }

        return ProductSprintCalendarResolution.Succeeded(new ProductSprintCalendar(
            orderedWindows,
            orderedWindows
                .Select((window, index) => new KeyValuePair<DateTime, int>(window.StartDateUtc.Date, index))
                .ToDictionary()));
    }

    private PlanningEpicOperationalState CreatePersistedOperationalState(
        ActiveRoadmapEpic epic,
        ProductPlanningIntentRecord intent,
        ProductSprintCalendarResolution calendarResolution)
    {
        var diagnostics = new List<PlanningBoardDiagnosticDto>();
        var intentSource = intent.RecoveryStatus.HasValue
            ? PlanningBoardIntentSource.Recovered
            : PlanningBoardIntentSource.Authored;

        if (intent.RecoveryStatus is ProductPlanningRecoveryStatus.RecoveredExact)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Info",
                "RecoveredExact",
                epic.EpicId,
                "This epic's internal planning intent was recovered exactly from the current TFS projected dates."));
        }
        else if (intent.RecoveryStatus is ProductPlanningRecoveryStatus.RecoveredWithNormalization)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Warning",
                "RecoveredWithNormalization",
                epic.EpicId,
                "This epic's internal planning intent was recovered from TFS projected dates and normalized to canonical sprint boundaries."));
        }

        var (driftStatus, driftDiagnostics) = DetermineDrift(epic, intent, calendarResolution);
        diagnostics.AddRange(driftDiagnostics);

        return new PlanningEpicOperationalState(
            intentSource,
            intent.RecoveryStatus,
            driftStatus,
            CanReconcileProjection: false,
            diagnostics);
    }

    private PlanningEpicOperationalState CreateRecoveredOperationalState(
        ActiveRoadmapEpic epic,
        ProductPlanningIntentRecord recoveredIntent,
        bool normalizedDuringRecovery)
    {
        var code = normalizedDuringRecovery
            ? "RecoveredWithNormalization"
            : "RecoveredExact";
        var message = normalizedDuringRecovery
            ? "Recovered from current TFS projected dates and normalized to canonical sprint boundaries."
            : "Recovered exactly from current TFS projected dates.";

        return new PlanningEpicOperationalState(
            PlanningBoardIntentSource.Recovered,
            recoveredIntent.RecoveryStatus,
            PlanningBoardDriftStatus.NoDrift,
            CanReconcileProjection: false,
            [
                CreateEpicDiagnostic(
                    normalizedDuringRecovery ? "Warning" : "Info",
                    code,
                    epic.EpicId,
                    message)
            ]);
    }

    private PlanningEpicOperationalState CreateBootstrapOperationalState(
        ActiveRoadmapEpic epic,
        ProductSprintCalendarResolution calendarResolution)
    {
        if (!epic.WorkItem.StartDate.HasValue && !epic.WorkItem.TargetDate.HasValue)
        {
            return PlanningEpicOperationalState.Bootstrap;
        }

        var diagnostics = new List<PlanningBoardDiagnosticDto>();
        if (calendarResolution.Calendar is null)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Error",
                "RecoveryFailed",
                epic.EpicId,
                $"Recovery from TFS projected dates is blocked because {calendarResolution.FailureReason}",
                isBlocking: true));
            return new PlanningEpicOperationalState(
                PlanningBoardIntentSource.Bootstrap,
                ProductPlanningRecoveryStatus.RecoveryFailed,
                PlanningBoardDriftStatus.CalendarResolutionFailure,
                CanReconcileProjection: false,
                diagnostics);
        }

        if (!epic.WorkItem.StartDate.HasValue || !epic.WorkItem.TargetDate.HasValue)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Warning",
                "RecoveryFailed",
                epic.EpicId,
                "Recovery from TFS projected dates was skipped because one or both projected dates are missing."));
            return new PlanningEpicOperationalState(
                PlanningBoardIntentSource.Bootstrap,
                ProductPlanningRecoveryStatus.RecoveryFailed,
                null,
                CanReconcileProjection: false,
                diagnostics);
        }

        var startDateOnly = DateOnly.FromDateTime(epic.WorkItem.StartDate.Value.UtcDateTime.Date);
        var targetDateOnly = DateOnly.FromDateTime(epic.WorkItem.TargetDate.Value.UtcDateTime.Date);
        if (startDateOnly < LegacyInvalidStartDateCutoff || targetDateOnly < startDateOnly)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Warning",
                "LegacyInvalidTfsDatesIgnored",
                epic.EpicId,
                "Legacy or invalid TFS projected dates were ignored during recovery."));
            diagnostics.Add(CreateEpicDiagnostic(
                "Warning",
                "RecoveryFailed",
                epic.EpicId,
                "Recovery from TFS projected dates failed because the projected dates are invalid."));
            return new PlanningEpicOperationalState(
                PlanningBoardIntentSource.Bootstrap,
                ProductPlanningRecoveryStatus.RecoveryFailed,
                PlanningBoardDriftStatus.LegacyInvalidTfsDates,
                CanReconcileProjection: false,
                diagnostics);
        }

        if (!TryFindSprintContainingDate(calendarResolution.Calendar, startDateOnly, out _) ||
            !TryFindSprintContainingDate(calendarResolution.Calendar, targetDateOnly, out _))
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Warning",
                "RecoveryFailed",
                epic.EpicId,
                "Recovery from TFS projected dates failed because the projected dates do not resolve cleanly onto the canonical sprint calendar."));
        }

        return new PlanningEpicOperationalState(
            PlanningBoardIntentSource.Bootstrap,
            ProductPlanningRecoveryStatus.RecoveryFailed,
            null,
            CanReconcileProjection: false,
            diagnostics);
    }

    private (PlanningBoardDriftStatus? DriftStatus, IReadOnlyList<PlanningBoardDiagnosticDto> Diagnostics) DetermineDrift(
        ActiveRoadmapEpic epic,
        ProductPlanningIntentRecord intent,
        ProductSprintCalendarResolution calendarResolution)
    {
        var diagnostics = new List<PlanningBoardDiagnosticDto>();
        if (calendarResolution.Calendar is null)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Error",
                "CalendarResolutionFailure",
                epic.EpicId,
                $"Drift could not be evaluated because {calendarResolution.FailureReason}",
                isBlocking: true));
            return (PlanningBoardDriftStatus.CalendarResolutionFailure, diagnostics);
        }

        var projectionStatus = TryCreatePlanningDateWriteRequest(epic.EpicId, intent, calendarResolution.Calendar, out var writeRequest);
        if (projectionStatus == PlanningProjectionStatus.MissingStartBoundary)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Error",
                "CalendarResolutionFailure",
                epic.EpicId,
                $"Internal planning intent references a sprint boundary that is no longer present ({intent.StartSprintStartDateUtc:yyyy-MM-dd}).",
                isBlocking: true));
            return (PlanningBoardDriftStatus.CalendarResolutionFailure, diagnostics);
        }

        if (projectionStatus == PlanningProjectionStatus.InsufficientFutureSprintCoverage)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Warning",
                "InsufficientFutureSprintCoverage",
                epic.EpicId,
                "Current sprint coverage is insufficient to project the full duration of this internal planning intent."));
            return (PlanningBoardDriftStatus.InsufficientFutureSprintCoverage, diagnostics);
        }

        if (!epic.WorkItem.StartDate.HasValue || !epic.WorkItem.TargetDate.HasValue)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Warning",
                "MissingTfsDates",
                epic.EpicId,
                "Internal planning intent exists, but one or both TFS projected dates are missing."));
            return (PlanningBoardDriftStatus.MissingTfsDates, diagnostics);
        }

        var currentStartDate = DateOnly.FromDateTime(epic.WorkItem.StartDate.Value.UtcDateTime.Date);
        var currentTargetDate = DateOnly.FromDateTime(epic.WorkItem.TargetDate.Value.UtcDateTime.Date);
        if (currentStartDate < LegacyInvalidStartDateCutoff || currentTargetDate < currentStartDate)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Warning",
                "LegacyInvalidTfsDatesIgnored",
                epic.EpicId,
                "Current TFS projected dates are legacy-invalid and do not match the internal planning intent projection."));
            return (PlanningBoardDriftStatus.LegacyInvalidTfsDates, diagnostics);
        }

        if (currentStartDate != writeRequest.StartDate || currentTargetDate != writeRequest.TargetDate)
        {
            diagnostics.Add(CreateEpicDiagnostic(
                "Warning",
                "StaleTfsProjection",
                epic.EpicId,
                $"Current TFS projected dates ({currentStartDate:yyyy-MM-dd} → {currentTargetDate:yyyy-MM-dd}) differ from the internal planning intent projection ({writeRequest.StartDate:yyyy-MM-dd} → {writeRequest.TargetDate:yyyy-MM-dd})."));
            return (PlanningBoardDriftStatus.TfsProjectionMismatch, diagnostics);
        }

        return (PlanningBoardDriftStatus.NoDrift, diagnostics);
    }

    private static PlanningBoardDiagnosticDto CreateBoardDiagnostic(
        string severity,
        string code,
        string message,
        bool isBlocking = false,
        bool canReconcileProjection = false)
        => new(severity, code, message, null, isBlocking, canReconcileProjection);

    private static PlanningBoardDiagnosticDto CreateEpicDiagnostic(
        string severity,
        string code,
        int epicId,
        string message,
        bool isBlocking = false,
        bool canReconcileProjection = false)
        => new(severity, code, message, epicId, isBlocking, canReconcileProjection);

    private static string DescribeProjectionFailure(PlanningProjectionStatus projectionStatus, DateTime startSprintStartDateUtc)
        => projectionStatus switch
        {
            PlanningProjectionStatus.MissingStartBoundary => $"The persisted start boundary {startSprintStartDateUtc:yyyy-MM-dd} is not present in the canonical sprint calendar.",
            PlanningProjectionStatus.InsufficientFutureSprintCoverage => "The canonical sprint calendar does not contain enough future coverage for the requested duration.",
            _ => "The planning projection could not be computed."
        };

    private ProductPlanningBoardDto CreateReadModel(
        PlanningContext planningContext,
        PlanningState state,
        IReadOnlyList<PlanningValidationIssue> validationIssues,
        IReadOnlyList<int> changedEpicIds,
        IReadOnlyList<int> affectedEpicIds)
    {
        var issues = MapIssues(validationIssues);
        var issueLookup = issues
            .Where(static issue => issue.EpicId.HasValue)
            .GroupBy(issue => issue.EpicId!.Value)
            .ToDictionary(static group => group.Key, static group => (IReadOnlyList<PlanningBoardIssueDto>)group.ToArray());
        var diagnosticLookup = planningContext.Diagnostics
            .Where(static diagnostic => diagnostic.EpicId.HasValue)
            .GroupBy(diagnostic => diagnostic.EpicId!.Value)
            .ToDictionary(static group => group.Key, static group => (IReadOnlyList<PlanningBoardDiagnosticDto>)group.ToArray());
        var titleLookup = planningContext.Epics.ToDictionary(static epic => epic.EpicId, static epic => epic.EpicTitle);
        var changedEpicIdSet = changedEpicIds.ToHashSet();
        var affectedEpicIdSet = affectedEpicIds.ToHashSet();

        var epicItems = state.Epics
            .Select(epic => new PlanningBoardEpicItemDto(
                epic.EpicId,
                titleLookup.GetValueOrDefault(epic.EpicId, $"Epic {epic.EpicId}"),
                epic.RoadmapOrder,
                epic.TrackIndex,
                epic.PlannedStartSprintIndex,
                epic.ComputedStartSprintIndex,
                epic.DurationInSprints,
                epic.EndSprintIndexExclusive,
                issueLookup.GetValueOrDefault(epic.EpicId, Array.Empty<PlanningBoardIssueDto>()),
                changedEpicIdSet.Contains(epic.EpicId),
                affectedEpicIdSet.Contains(epic.EpicId),
                planningContext.OperationalStateByEpicId.GetValueOrDefault(epic.EpicId)?.IntentSource ?? PlanningBoardIntentSource.Bootstrap,
                planningContext.OperationalStateByEpicId.GetValueOrDefault(epic.EpicId)?.RecoveryStatus,
                planningContext.OperationalStateByEpicId.GetValueOrDefault(epic.EpicId)?.DriftStatus,
                planningContext.OperationalStateByEpicId.GetValueOrDefault(epic.EpicId)?.CanReconcileProjection ?? false,
                MergeDiagnostics(
                    planningContext.OperationalStateByEpicId.GetValueOrDefault(epic.EpicId)?.Diagnostics,
                    diagnosticLookup.GetValueOrDefault(epic.EpicId, Array.Empty<PlanningBoardDiagnosticDto>()))))
            .OrderBy(static epic => epic.RoadmapOrder)
            .ToArray();

        var maxTrackIndex = epicItems.Length == 0 ? 0 : epicItems.Max(static epic => epic.TrackIndex);
        var tracks = Enumerable
            .Range(0, maxTrackIndex + 1)
            .Select(trackIndex => new PlanningBoardTrackDto(
                trackIndex,
                trackIndex == 0,
                epicItems
                    .Where(epic => epic.TrackIndex == trackIndex)
                    .OrderBy(static epic => epic.ComputedStartSprintIndex)
                    .ThenBy(static epic => epic.RoadmapOrder)
                    .Select(static epic => epic.EpicId)
                    .ToArray()))
            .ToArray();

        return new ProductPlanningBoardDto(
            planningContext.ProductId,
            planningContext.ProductName,
            tracks,
            epicItems,
            issues,
            changedEpicIds,
            affectedEpicIds,
            planningContext.Diagnostics);
    }

    private static IReadOnlyList<PlanningBoardDiagnosticDto> MergeDiagnostics(
        IReadOnlyList<PlanningBoardDiagnosticDto>? left,
        IReadOnlyList<PlanningBoardDiagnosticDto>? right)
    {
        if ((left is null || left.Count == 0) && (right is null || right.Count == 0))
        {
            return Array.Empty<PlanningBoardDiagnosticDto>();
        }

        return (left ?? Array.Empty<PlanningBoardDiagnosticDto>())
            .Concat(right ?? Array.Empty<PlanningBoardDiagnosticDto>())
            .ToArray();
    }

    private static IReadOnlyList<PlanningBoardIssueDto> MapIssues(IReadOnlyList<PlanningValidationIssue> issues)
    {
        return issues
            .Select(static issue => new PlanningBoardIssueDto(
                "Validation",
                issue.Code.ToString(),
                issue.Message,
                issue.EpicId))
            .ToArray();
    }

    private static bool IsRoadmapEpic(WorkItemDto workItem)
    {
        return string.Equals(workItem.Type?.Trim(), "Epic", StringComparison.OrdinalIgnoreCase) &&
               HasRoadmapTag(workItem.Tags);
    }

    private static bool HasRoadmapTag(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return false;
        }

        return tags
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(static tag => string.Equals(tag, "roadmap", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record PlanningContext(
        int ProductId,
        string ProductName,
        IReadOnlyList<ActiveRoadmapEpic> Epics,
        PlanningState BaseState,
        ProductSprintCalendarResolution CalendarResolution,
        IReadOnlyDictionary<int, PlanningEpicOperationalState> OperationalStateByEpicId,
        IReadOnlyList<PlanningBoardDiagnosticDto> Diagnostics);

    private sealed record ActiveRoadmapEpic(
        int EpicId,
        string EpicTitle,
        int RoadmapOrder,
        WorkItemDto WorkItem);

    private sealed record ProductSprintCalendarResolution(ProductSprintCalendar? Calendar, string? FailureReason)
    {
        public static ProductSprintCalendarResolution Succeeded(ProductSprintCalendar calendar) => new(calendar, null);

        public static ProductSprintCalendarResolution Failed(string reason) => new(null, reason);
    }

    private sealed record ProductSprintCalendar(
        IReadOnlyList<ProductSprintWindow> Sprints,
        IReadOnlyDictionary<DateTime, int> StartIndexByDate);

    private sealed record ProductSprintWindow(DateTime StartDateUtc, DateTime EndExclusiveDateUtc);

    private sealed record PlanningEpicOperationalState(
        PlanningBoardIntentSource IntentSource,
        ProductPlanningRecoveryStatus? RecoveryStatus,
        PlanningBoardDriftStatus? DriftStatus,
        bool CanReconcileProjection,
        IReadOnlyList<PlanningBoardDiagnosticDto> Diagnostics)
    {
        public static readonly PlanningEpicOperationalState Bootstrap = new(
            PlanningBoardIntentSource.Bootstrap,
            null,
            null,
            false,
            Array.Empty<PlanningBoardDiagnosticDto>());
    }

    private enum PlanningProjectionStatus
    {
        Success,
        MissingStartBoundary,
        InsufficientFutureSprintCoverage
    }

    private readonly record struct PlanningDateWriteRequest(int EpicId, DateOnly StartDate, DateOnly TargetDate);
}
