using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public sealed record OnboardingMigrationExecutionRequest(
    string MigrationVersion,
    string EnvironmentRing,
    string TriggerType,
    OnboardingMigrationExecutionMode ExecutionMode);

public interface IOnboardingMigrationExecutionService
{
    Task<OnboardingMigrationRunSummary> ExecuteAsync(OnboardingMigrationExecutionRequest request, CancellationToken cancellationToken);
}

public sealed class OnboardingMigrationExecutionService : IOnboardingMigrationExecutionService
{
    private static readonly IReadOnlyList<OnboardingMigrationUnitPlan> OrderedUnits =
    [
        new("Connection", "connection", 1),
        new("ProjectSource", "projects", 2),
        new("TeamSource", "teams", 3),
        new("PipelineSource", "pipelines", 4),
        new("ProductRoot", "roots", 5),
        new("ProductSourceBinding", "bindings", 6)
    ];

    private readonly PoToolDbContext _dbContext;
    private readonly IOnboardingLegacyMigrationReader _legacyReader;
    private readonly IOnboardingMigrationMapper _mapper;
    private readonly IOnboardingMigrationLedgerService _ledgerService;
    private readonly IOnboardingValidationService _validationService;
    private readonly IOnboardingLiveLookupClient _lookupClient;
    private readonly IOnboardingMigrationRunLock _runLock;

    public OnboardingMigrationExecutionService(
        PoToolDbContext dbContext,
        IOnboardingLegacyMigrationReader legacyReader,
        IOnboardingMigrationMapper mapper,
        IOnboardingMigrationLedgerService ledgerService,
        IOnboardingValidationService validationService,
        IOnboardingLiveLookupClient lookupClient,
        IOnboardingMigrationRunLock runLock)
    {
        _dbContext = dbContext;
        _legacyReader = legacyReader;
        _mapper = mapper;
        _ledgerService = ledgerService;
        _validationService = validationService;
        _lookupClient = lookupClient;
        _runLock = runLock;
    }

    public async Task<OnboardingMigrationRunSummary> ExecuteAsync(
        OnboardingMigrationExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MigrationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EnvironmentRing);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TriggerType);

        var legacySnapshot = await _legacyReader.ReadAsync(cancellationToken);
        var lockKey = CreateRunLockKey(legacySnapshot.Connection);
        await using var lockLease = await _runLock.TryAcquireAsync(lockKey, cancellationToken)
            ?? throw new InvalidOperationException($"An onboarding migration is already running for '{lockKey}'.");

        var migrationVersion = request.MigrationVersion.Trim();
        var hasMatchingFingerprint = !string.IsNullOrWhiteSpace(legacySnapshot.SourceFingerprint)
            && await _dbContext.OnboardingMigrationRuns
                .AsNoTracking()
                .AnyAsync(
                    run => run.MigrationVersion == migrationVersion
                        && run.SourceFingerprint == legacySnapshot.SourceFingerprint
                        && (run.Status == OnboardingMigrationRunStatus.Succeeded || run.Status == OnboardingMigrationRunStatus.NoOp),
                    cancellationToken);

        var run = await _ledgerService.CreateRunAsync(
            new OnboardingMigrationRunCreateRequest(
                migrationVersion,
                request.EnvironmentRing.Trim(),
                request.TriggerType.Trim(),
                request.ExecutionMode,
                legacySnapshot.SourceFingerprint),
            cancellationToken);

        var units = await _ledgerService.CreateUnitsAsync(run.RunIdentifier, OrderedUnits, cancellationToken);
        var state = new ExecutionState(run.RunIdentifier, request.ExecutionMode, run.CreatedAtUtc);

        if (hasMatchingFingerprint)
        {
            await ExecuteNoOpAsync(units, state, cancellationToken);
        }
        else
        {
            await ExecuteConnectionUnitAsync(legacySnapshot, units[0], state, cancellationToken);
            await ExecuteProjectUnitAsync(legacySnapshot, units[1], state, cancellationToken);
            await ExecuteTeamUnitAsync(legacySnapshot, units[2], state, cancellationToken);
            await ExecutePipelineUnitAsync(legacySnapshot, units[3], state, cancellationToken);
            await ExecuteProductRootUnitAsync(legacySnapshot, units[4], state, cancellationToken);
            await ExecuteBindingUnitAsync(legacySnapshot, units[5], state, cancellationToken);
        }

        await _ledgerService.FinalizeRunAsync(run.RunIdentifier, cancellationToken);
        return await _ledgerService.GetRunSummaryAsync(run.RunIdentifier, cancellationToken);
    }

    private async Task ExecuteNoOpAsync(
        IReadOnlyList<MigrationUnit> units,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        await RecordIssueAsync(
            state.RunIdentifier,
            null,
            "FingerprintUnchanged",
            "NoOp",
            OnboardingMigrationIssueSeverity.Info,
            "SourceFingerprint",
            nameof(MigrationRun),
            null,
            "Legacy onboarding source fingerprint matched a prior run. Execution skipped.",
            null,
            isBlocking: false,
            cancellationToken);

        foreach (var unit in units.OrderBy(item => item.ExecutionOrder))
        {
            await _ledgerService.SkipUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 0, 0), cancellationToken);
        }
    }

    private async Task ExecuteConnectionUnitAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        MigrationUnit unit,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        await _ledgerService.StartUnitAsync(unit.UnitIdentifier, cancellationToken);

        if (legacySnapshot.Connection is null)
        {
            await RecordBlockingIssueAsync(
                state.RunIdentifier,
                unit.UnitIdentifier,
                "MissingRequiredLegacyField",
                "MappingFailure",
                "TfsConfigEntity",
                nameof(TfsConnection),
                "connection",
                "Legacy onboarding connection configuration is required.",
                null,
                cancellationToken);

            await _ledgerService.FailUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(1, 0, 1, 0), cancellationToken);
            return;
        }

        var mapped = _mapper.MapConnection(legacySnapshot.Connection, state.MigrationTimestampUtc);
        var projectsLookup = await GetProjectsLookupAsync(state, mapped.Entity, cancellationToken);
        var validation = await _validationService.ValidateConnectionAsync(
            mapped.Entity,
            cancellationToken,
            projectsLookup,
            state.MigrationTimestampUtc);

        if (!validation.Succeeded)
        {
            await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
            await _ledgerService.FailUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(1, 0, 1, 0), cancellationToken);
            return;
        }

        ApplyConnectionValidation(mapped.Entity, validation.Data!);
        state.Connection = await UpsertConnectionAsync(mapped.Entity, state, cancellationToken);

        await _ledgerService.CompleteUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(1, 1, 0, 0), cancellationToken);
    }

    private async Task ExecuteProjectUnitAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        MigrationUnit unit,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        var projectSeedCount = legacySnapshot.ProjectReferences.Count + legacySnapshot.ProductRoots.Count + legacySnapshot.Pipelines.Count;
        if (projectSeedCount == 0)
        {
            await _ledgerService.SkipUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 0, 0), cancellationToken);
            return;
        }

        await _ledgerService.StartUnitAsync(unit.UnitIdentifier, cancellationToken);

        if (state.Connection is null)
        {
            var dependencyFailedCount = 0;

            foreach (var reference in legacySnapshot.ProjectReferences)
            {
                dependencyFailedCount++;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    reference.SourceLegacyReference,
                    nameof(ProjectSource),
                    null,
                    "A validated connection is required before project migration can run.",
                    reference.ProjectName,
                    cancellationToken);
            }

            foreach (var productRoot in legacySnapshot.ProductRoots)
            {
                dependencyFailedCount++;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    productRoot.SourceLegacyReference,
                    nameof(ProjectSource),
                    productRoot.WorkItemExternalId,
                    "A validated connection is required before project discovery can resolve product root scope.",
                    productRoot.WorkItemExternalId,
                    cancellationToken);
            }

            foreach (var pipeline in legacySnapshot.Pipelines)
            {
                dependencyFailedCount++;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    pipeline.SourceLegacyReference,
                    nameof(ProjectSource),
                    pipeline.PipelineExternalId,
                    "A validated connection is required before project discovery can resolve pipeline scope.",
                    pipeline.PipelineExternalId,
                    cancellationToken);
            }

            await _ledgerService.FailUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(dependencyFailedCount, 0, dependencyFailedCount, 0), cancellationToken);
            return;
        }

        var availableProjectsLookup = await GetProjectsLookupAsync(state, state.Connection, cancellationToken);
        if (!availableProjectsLookup.Succeeded)
        {
            await RecordBlockingIssueAsync(
                state.RunIdentifier,
                unit.UnitIdentifier,
                "DiscoveryFailure",
                availableProjectsLookup.Error!.Code.ToString(),
                "ProjectDiscovery",
                nameof(ProjectSource),
                null,
                availableProjectsLookup.Error.Message,
                availableProjectsLookup.Error.Details,
                cancellationToken);
            await _ledgerService.FailUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 0, 0), cancellationToken);
            return;
        }

        var resolution = await ResolveProjectCandidatesAsync(
            legacySnapshot,
            unit.UnitIdentifier,
            state,
            availableProjectsLookup.Data!,
            cancellationToken);

        if (resolution.Candidates.Count == 0)
        {
            if (!resolution.HasBlockingIssue)
            {
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DiscoveryFailure",
                    "DependencyViolation",
                    "ProjectDiscovery",
                    nameof(ProjectSource),
                    null,
                    "Project discovery did not resolve any migration candidates from legacy sources.",
                    null,
                    cancellationToken);
            }

            await _ledgerService.FailUnitAsync(
                unit.UnitIdentifier,
                new OnboardingMigrationUnitOutcome(resolution.FailedEntityCount, 0, resolution.FailedEntityCount, 0),
                cancellationToken);
            return;
        }

        var processed = resolution.FailedEntityCount;
        var succeeded = 0;
        var failed = resolution.FailedEntityCount;
        var hasBlockingIssue = resolution.HasBlockingIssue;

        foreach (var candidate in resolution.Candidates
                     .OrderBy(item => item.Project.ProjectExternalId, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Reference.SourceLegacyReference, StringComparer.Ordinal))
        {
            processed++;

            var mapped = _mapper.MapProject(candidate.Reference, candidate.Project, state.MigrationTimestampUtc);
            mapped.Entity.TfsConnectionId = state.Connection.Id;

            var validation = await _validationService.ValidateProjectSourceAsync(
                state.Connection,
                mapped.Entity,
                cancellationToken,
                availableProjectsLookup.Data!,
                state.MigrationTimestampUtc);
            if (!validation.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
                continue;
            }

            ApplyProjectValidation(mapped.Entity, validation.Data!);

            var persistedProject = await UpsertProjectAsync(mapped.Entity, state.Connection, state, cancellationToken);
            succeeded++;
            state.ProjectsByExternalId[persistedProject.ProjectExternalId] = persistedProject;
            state.ProjectsByName[candidate.Project.Name] = persistedProject;
        }

        await CompleteUnitAsync(unit.UnitIdentifier, processed, succeeded, failed, hasBlockingIssue, cancellationToken);
    }

    private async Task ExecuteTeamUnitAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        MigrationUnit unit,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        if (legacySnapshot.Teams.Count == 0)
        {
            await _ledgerService.SkipUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 0, 0), cancellationToken);
            return;
        }

        await _ledgerService.StartUnitAsync(unit.UnitIdentifier, cancellationToken);

        var processed = 0;
        var succeeded = 0;
        var failed = 0;
        var hasBlockingIssue = false;

        foreach (var team in legacySnapshot.Teams)
        {
            processed++;

            if (state.Connection is null || state.ProjectsByExternalId.Count == 0)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    team.SourceLegacyReference,
                    nameof(TeamSource),
                    team.TeamExternalId?.Trim(),
                    "Validated connection and migrated project sources are required before team migration can run.",
                    team.ProjectName,
                    cancellationToken);
                continue;
            }

            if (string.IsNullOrWhiteSpace(team.TeamExternalId))
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "MissingRequiredLegacyField",
                    "MappingFailure",
                    team.SourceLegacyReference,
                    nameof(TeamSource),
                    null,
                    "Legacy team rows require a stable TFS team identity before migration.",
                    "Missing TfsTeamId.",
                    cancellationToken);
                continue;
            }

            if (string.IsNullOrWhiteSpace(team.ProjectName) || !state.ProjectsByName.TryGetValue(team.ProjectName.Trim(), out var projectSource))
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    team.SourceLegacyReference,
                    nameof(TeamSource),
                    team.TeamExternalId.Trim(),
                    "A migrated project source is required before the team can be migrated.",
                    team.ProjectName,
                    cancellationToken);
                continue;
            }

            var mapped = _mapper.MapTeam(team, projectSource.ProjectExternalId, state.MigrationTimestampUtc);
            mapped.Entity.ProjectSourceId = projectSource.Id;

            var teamsLookup = await GetTeamsLookupAsync(state, state.Connection, projectSource.ProjectExternalId, cancellationToken);
            if (!teamsLookup.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, teamsLookup.Error!, cancellationToken);
                continue;
            }

            var validation = await _validationService.ValidateTeamSourceAsync(
                state.Connection,
                projectSource,
                mapped.Entity,
                cancellationToken,
                teamsLookup.Data!,
                state.MigrationTimestampUtc);
            if (!validation.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
                continue;
            }

            ApplyTeamValidation(mapped.Entity, validation.Data!);

            var persistedTeam = await UpsertTeamAsync(mapped.Entity, projectSource, state, cancellationToken);
            succeeded++;
            state.TeamsByLegacyId[team.TeamId] = persistedTeam;
            state.TeamsByScopedKey[CreateScopedTeamKey(projectSource.Id, persistedTeam.TeamExternalId)] = persistedTeam;
        }

        await CompleteUnitAsync(unit.UnitIdentifier, processed, succeeded, failed, hasBlockingIssue, cancellationToken);
    }

    private async Task ExecutePipelineUnitAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        MigrationUnit unit,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        if (legacySnapshot.Pipelines.Count == 0)
        {
            await _ledgerService.SkipUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 0, 0), cancellationToken);
            return;
        }

        await _ledgerService.StartUnitAsync(unit.UnitIdentifier, cancellationToken);

        var processed = 0;
        var succeeded = 0;
        var failed = 0;
        var hasBlockingIssue = false;

        foreach (var pipeline in legacySnapshot.Pipelines)
        {
            processed++;

            if (state.Connection is null || state.ProjectsByExternalId.Count == 0)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    pipeline.SourceLegacyReference,
                    nameof(PipelineSource),
                    pipeline.PipelineExternalId,
                    "Validated connection and migrated project sources are required before pipeline migration can run.",
                    null,
                    cancellationToken);
                continue;
            }

            if (!state.ProjectExternalIdByLegacyPipeline.TryGetValue(CreateLegacyPipelineKey(pipeline.ProductId, pipeline.PipelineExternalId), out var projectExternalId)
                || !state.ProjectsByExternalId.TryGetValue(projectExternalId, out var projectSource))
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    pipeline.SourceLegacyReference,
                    nameof(PipelineSource),
                    pipeline.PipelineExternalId,
                    "A discoverable migrated project source is required before the pipeline can be migrated.",
                    null,
                    cancellationToken);
                continue;
            }

            var mapped = _mapper.MapPipeline(pipeline, projectSource.ProjectExternalId, state.MigrationTimestampUtc);
            mapped.Entity.ProjectSourceId = projectSource.Id;

            var pipelinesLookup = await GetPipelinesLookupAsync(state, state.Connection, projectSource.ProjectExternalId, unit.UnitIdentifier, cancellationToken);
            if (!pipelinesLookup.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, pipelinesLookup.Error!, cancellationToken);
                continue;
            }

            var validation = await _validationService.ValidatePipelineSourceAsync(
                state.Connection,
                projectSource,
                mapped.Entity,
                cancellationToken,
                pipelinesLookup.Data!,
                state.MigrationTimestampUtc);
            if (!validation.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
                continue;
            }

            ApplyPipelineValidation(mapped.Entity, validation.Data!);

            var persistedPipeline = await UpsertPipelineAsync(mapped.Entity, projectSource, state, cancellationToken);
            succeeded++;
            state.PipelinesByScopedKey[CreateScopedPipelineKey(projectSource.Id, persistedPipeline.PipelineExternalId)] = persistedPipeline;
        }

        await CompleteUnitAsync(unit.UnitIdentifier, processed, succeeded, failed, hasBlockingIssue, cancellationToken);
    }

    private async Task ExecuteProductRootUnitAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        MigrationUnit unit,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        if (legacySnapshot.ProductRoots.Count == 0)
        {
            await _ledgerService.SkipUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 0, 0), cancellationToken);
            return;
        }

        await _ledgerService.StartUnitAsync(unit.UnitIdentifier, cancellationToken);

        var processed = 0;
        var succeeded = 0;
        var failed = 0;
        var hasBlockingIssue = false;

        foreach (var productRoot in legacySnapshot.ProductRoots)
        {
            processed++;

            if (state.Connection is null || state.ProjectsByExternalId.Count == 0)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    productRoot.SourceLegacyReference,
                    nameof(ProductRoot),
                    productRoot.WorkItemExternalId,
                    "Validated connection and migrated project sources are required before product root migration can run.",
                    productRoot.WorkItemExternalId,
                    cancellationToken);
                continue;
            }

            var workItemLookup = await GetWorkItemLookupAsync(state, state.Connection, productRoot.WorkItemExternalId, cancellationToken);
            if (!workItemLookup.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    new OnboardingMigrationMappingContext(productRoot.SourceLegacyReference, nameof(ProductRoot), productRoot.WorkItemExternalId),
                    workItemLookup.Error!,
                    cancellationToken);
                continue;
            }

            var resolvedWorkItem = workItemLookup.Data!;
            if (!state.ProjectsByExternalId.TryGetValue(resolvedWorkItem.ProjectExternalId, out var projectSource))
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    productRoot.SourceLegacyReference,
                    nameof(ProductRoot),
                    productRoot.WorkItemExternalId,
                    "A migrated project source is required before the product root can be migrated.",
                    resolvedWorkItem.ProjectExternalId,
                    cancellationToken);
                continue;
            }

            var mapped = _mapper.MapProductRoot(productRoot, projectSource.ProjectExternalId, state.MigrationTimestampUtc);
            mapped.Entity.ProjectSourceId = projectSource.Id;

            var validation = await _validationService.ValidateProductRootAsync(
                state.Connection,
                projectSource,
                mapped.Entity,
                cancellationToken,
                workItemLookup,
                state.MigrationTimestampUtc);
            if (!validation.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
                continue;
            }

            ApplyProductRootValidation(mapped.Entity, validation.Data!);

            var persistedRoot = await UpsertProductRootAsync(mapped.Entity, projectSource, state, cancellationToken);
            succeeded++;
            state.ProductRootsByLegacyKey[CreateLegacyProductRootKey(productRoot.ProductId, persistedRoot.WorkItemExternalId)] = persistedRoot;
        }

        await CompleteUnitAsync(unit.UnitIdentifier, processed, succeeded, failed, hasBlockingIssue, cancellationToken);
    }

    private async Task ExecuteBindingUnitAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        MigrationUnit unit,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        var bindingWorkItems = BuildBindingWorkItems(legacySnapshot, state).ToArray();
        if (bindingWorkItems.Length == 0)
        {
            await _ledgerService.SkipUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 0, 0), cancellationToken);
            return;
        }

        await _ledgerService.StartUnitAsync(unit.UnitIdentifier, cancellationToken);

        var processed = 0;
        var succeeded = 0;
        var failed = 0;
        var hasBlockingIssue = false;

        foreach (var workItem in bindingWorkItems)
        {
            processed++;

            if (workItem.Issue is not null)
            {
                failed++;
                hasBlockingIssue |= workItem.Issue.IsBlocking;
                await RecordIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    workItem.Issue.IssueType,
                    workItem.Issue.IssueCategory,
                    workItem.Issue.Severity,
                    workItem.Issue.SourceLegacyReference,
                    workItem.Issue.TargetEntityType,
                    workItem.Issue.TargetExternalIdentity,
                    workItem.Issue.SanitizedMessage,
                    workItem.Issue.SanitizedDetails,
                    workItem.Issue.IsBlocking,
                    cancellationToken);
                continue;
            }

            if (state.Connection is null || workItem.ProductRoot is null || workItem.ProjectSource is null)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    workItem.SourceLegacyReference,
                    nameof(ProductSourceBinding),
                    workItem.TargetExternalIdentity,
                    "Validated connection, migrated product root, and migrated project source are required before the binding can be created.",
                    null,
                    cancellationToken);
                continue;
            }

            var mapped = workItem.Map!();
            var validation = await _validationService.ValidateProductSourceBindingAsync(
                state.Connection,
                workItem.ProjectSource,
                workItem.ProductRoot,
                mapped.Entity,
                workItem.TeamSource,
                workItem.PipelineSource,
                cancellationToken,
                await GetWorkItemLookupAsync(state, state.Connection, workItem.ProductRoot.WorkItemExternalId, cancellationToken),
                workItem.TeamSource is null ? null : (await GetTeamsLookupAsync(state, state.Connection, workItem.ProjectSource.ProjectExternalId, cancellationToken)).Data,
                workItem.PipelineSource is null ? null : (await GetPipelinesLookupAsync(state, state.Connection, workItem.ProjectSource.ProjectExternalId, unit.UnitIdentifier, cancellationToken)).Data,
                state.MigrationTimestampUtc);

            if (!validation.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
                continue;
            }

            ApplyBindingValidation(mapped.Entity, validation.Data!);
            await UpsertBindingAsync(mapped.Entity, workItem.ProductRoot, workItem.ProjectSource, workItem.TeamSource, workItem.PipelineSource, state, cancellationToken);
            succeeded++;
        }

        await CompleteUnitAsync(unit.UnitIdentifier, processed, succeeded, failed, hasBlockingIssue, cancellationToken);
    }

    private async Task<ProjectResolutionResult> ResolveProjectCandidatesAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        Guid unitIdentifier,
        ExecutionState state,
        IReadOnlyList<ProjectLookupResultDto> availableProjects,
        CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<string, ResolvedProjectCandidate>(StringComparer.OrdinalIgnoreCase);
        var failed = 0;
        var hasBlockingIssue = false;

        foreach (var reference in legacySnapshot.ProjectReferences)
        {
            var matches = availableProjects
                .Where(item => item.Name.Equals(reference.ProjectName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.ProjectExternalId, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (matches.Length == 0)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unitIdentifier,
                    "DiscoveryFailure",
                    "NotFound",
                    reference.SourceLegacyReference,
                    nameof(ProjectSource),
                    null,
                    "No TFS project matched the legacy project reference.",
                    reference.ProjectName,
                    cancellationToken);
                continue;
            }

            if (matches.Length > 1)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unitIdentifier,
                    "InconsistentLegacyReference",
                    "DependencyViolation",
                    reference.SourceLegacyReference,
                    nameof(ProjectSource),
                    null,
                    "The legacy project reference resolved to multiple TFS projects.",
                    string.Join(",", matches.Select(item => item.ProjectExternalId)),
                    cancellationToken);
                continue;
            }

            candidates.TryAdd(matches[0].ProjectExternalId, new ResolvedProjectCandidate(reference, matches[0]));
        }

        foreach (var productRoot in legacySnapshot.ProductRoots)
        {
            var workItemLookup = await GetWorkItemLookupAsync(state, state.Connection!, productRoot.WorkItemExternalId, cancellationToken);
            if (!workItemLookup.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(
                    state.RunIdentifier,
                    unitIdentifier,
                    new OnboardingMigrationMappingContext(productRoot.SourceLegacyReference, nameof(ProjectSource), productRoot.WorkItemExternalId),
                    workItemLookup.Error!,
                    cancellationToken);
                continue;
            }

            var resolvedWorkItem = workItemLookup.Data!;
            var project = availableProjects.FirstOrDefault(item => item.ProjectExternalId.Equals(resolvedWorkItem.ProjectExternalId, StringComparison.OrdinalIgnoreCase))
                ?? new ProjectLookupResultDto(resolvedWorkItem.ProjectExternalId, resolvedWorkItem.ProjectExternalId, null);

            candidates.TryAdd(
                project.ProjectExternalId,
                new ResolvedProjectCandidate(new LegacyProjectReference(productRoot.SourceLegacyReference, project.Name), project));
        }

        foreach (var project in availableProjects.OrderBy(item => item.ProjectExternalId, StringComparer.OrdinalIgnoreCase))
        {
            var pipelinesLookup = await GetPipelinesLookupAsync(state, state.Connection!, project.ProjectExternalId, unitIdentifier, cancellationToken);
            if (!pipelinesLookup.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
            }
        }

        foreach (var pipeline in legacySnapshot.Pipelines)
        {
            var matchingProjects = new List<ProjectLookupResultDto>();

            foreach (var availableProject in availableProjects.OrderBy(item => item.ProjectExternalId, StringComparer.OrdinalIgnoreCase))
            {
                var pipelinesLookup = await GetPipelinesLookupAsync(state, state.Connection!, availableProject.ProjectExternalId, unitIdentifier, cancellationToken);
                if (!pipelinesLookup.Succeeded)
                {
                    continue;
                }

                if (pipelinesLookup.Data!.Any(item => item.PipelineExternalId.Equals(pipeline.PipelineExternalId, StringComparison.OrdinalIgnoreCase)))
                {
                    matchingProjects.Add(availableProject);
                }
            }

            if (matchingProjects.Count == 0)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unitIdentifier,
                    "DiscoveryFailure",
                    "NotFound",
                    pipeline.SourceLegacyReference,
                    nameof(ProjectSource),
                    pipeline.PipelineExternalId,
                    "No TFS project could be resolved for the legacy pipeline reference.",
                    pipeline.PipelineExternalId,
                    cancellationToken);
                continue;
            }

            if (matchingProjects.Count > 1)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unitIdentifier,
                    "InconsistentLegacyReference",
                    "DependencyViolation",
                    pipeline.SourceLegacyReference,
                    nameof(ProjectSource),
                    pipeline.PipelineExternalId,
                    "The legacy pipeline reference resolved to multiple TFS projects.",
                    string.Join(",", matchingProjects.Select(item => item.ProjectExternalId)),
                    cancellationToken);
                continue;
            }

            var project = matchingProjects[0];
            state.ProjectExternalIdByLegacyPipeline[CreateLegacyPipelineKey(pipeline.ProductId, pipeline.PipelineExternalId)] = project.ProjectExternalId;
            candidates.TryAdd(
                project.ProjectExternalId,
                new ResolvedProjectCandidate(new LegacyProjectReference(pipeline.SourceLegacyReference, project.Name), project));
        }

        return new ProjectResolutionResult(candidates.Values.ToArray(), failed, hasBlockingIssue);
    }

    private IEnumerable<BindingWorkItem> BuildBindingWorkItems(LegacyOnboardingMigrationSnapshot legacySnapshot, ExecutionState state)
    {
        var workItems = new List<BindingWorkItem>();
        var legacyRootsByProduct = legacySnapshot.ProductRoots
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.WorkItemExternalId, StringComparer.Ordinal).ToArray());

        foreach (var productRoot in legacySnapshot.ProductRoots)
        {
            if (!state.ProductRootsByLegacyKey.TryGetValue(CreateLegacyProductRootKey(productRoot.ProductId, productRoot.WorkItemExternalId), out var migratedRoot))
            {
                workItems.Add(BindingWorkItem.Invalid(
                    productRoot.SourceLegacyReference,
                    productRoot.WorkItemExternalId,
                    new OnboardingMigrationDryRunIssuePlan(
                        "DependencyViolation",
                        "DependencyViolation",
                        OnboardingMigrationIssueSeverity.Blocking,
                        productRoot.SourceLegacyReference,
                        nameof(ProductSourceBinding),
                        productRoot.WorkItemExternalId,
                        "The product root was not migrated, so the required project binding could not be created.",
                        productRoot.WorkItemExternalId,
                        true)));
                continue;
            }

            if (!state.ProjectsByExternalId.TryGetValue(migratedRoot.Snapshot.ProjectExternalId, out var projectSource))
            {
                workItems.Add(BindingWorkItem.Invalid(
                    productRoot.SourceLegacyReference,
                    migratedRoot.WorkItemExternalId,
                    new OnboardingMigrationDryRunIssuePlan(
                        "DependencyViolation",
                        "DependencyViolation",
                        OnboardingMigrationIssueSeverity.Blocking,
                        productRoot.SourceLegacyReference,
                        nameof(ProductSourceBinding),
                        migratedRoot.WorkItemExternalId,
                        "The migrated project source required for the project binding is missing.",
                        migratedRoot.Snapshot.ProjectExternalId,
                        true)));
                continue;
            }

            var bindingRoot = migratedRoot;
            var bindingProject = projectSource;
            workItems.Add(BindingWorkItem.Valid(
                productRoot.SourceLegacyReference,
                bindingRoot.WorkItemExternalId,
                bindingRoot,
                bindingProject,
                null,
                null,
                () => _mapper.MapProjectBinding(productRoot, bindingRoot, bindingProject, state.MigrationTimestampUtc)));
        }

        foreach (var binding in legacySnapshot.TeamBindings)
        {
            if (!legacyRootsByProduct.TryGetValue(binding.ProductId, out var legacyRoots) || legacyRoots.Length == 0)
            {
                workItems.Add(BindingWorkItem.Invalid(
                    binding.SourceLegacyReference,
                    null,
                    new OnboardingMigrationDryRunIssuePlan(
                        "InconsistentLegacyReference",
                        "DependencyViolation",
                        OnboardingMigrationIssueSeverity.Blocking,
                        binding.SourceLegacyReference,
                        nameof(ProductSourceBinding),
                        null,
                        "The legacy team binding does not have any product roots to bind.",
                        binding.ProductId.ToString(),
                        true)));
                continue;
            }

            foreach (var legacyRoot in legacyRoots)
            {
                if (!state.ProductRootsByLegacyKey.TryGetValue(CreateLegacyProductRootKey(legacyRoot.ProductId, legacyRoot.WorkItemExternalId), out var migratedRoot))
                {
                    workItems.Add(BindingWorkItem.Invalid(
                        binding.SourceLegacyReference,
                        legacyRoot.WorkItemExternalId,
                        new OnboardingMigrationDryRunIssuePlan(
                            "DependencyViolation",
                            "DependencyViolation",
                            OnboardingMigrationIssueSeverity.Blocking,
                            binding.SourceLegacyReference,
                            nameof(ProductSourceBinding),
                            legacyRoot.WorkItemExternalId,
                            "The migrated product root required for the team binding is missing.",
                            legacyRoot.WorkItemExternalId,
                            true)));
                    continue;
                }

                if (!state.TeamsByLegacyId.TryGetValue(binding.TeamId, out var teamSource))
                {
                    workItems.Add(BindingWorkItem.Invalid(
                        binding.SourceLegacyReference,
                        legacyRoot.WorkItemExternalId,
                        new OnboardingMigrationDryRunIssuePlan(
                            "DependencyViolation",
                            "DependencyViolation",
                            OnboardingMigrationIssueSeverity.Blocking,
                            binding.SourceLegacyReference,
                            nameof(ProductSourceBinding),
                            legacyRoot.WorkItemExternalId,
                            "The migrated team source required for the team binding is missing.",
                            binding.TeamId.ToString(),
                            true)));
                    continue;
                }

                if (!state.ProjectsByExternalId.TryGetValue(migratedRoot.Snapshot.ProjectExternalId, out var projectSource))
                {
                    workItems.Add(BindingWorkItem.Invalid(
                        binding.SourceLegacyReference,
                        legacyRoot.WorkItemExternalId,
                        new OnboardingMigrationDryRunIssuePlan(
                            "DependencyViolation",
                            "DependencyViolation",
                            OnboardingMigrationIssueSeverity.Blocking,
                            binding.SourceLegacyReference,
                            nameof(ProductSourceBinding),
                            legacyRoot.WorkItemExternalId,
                            "The migrated project source required for the team binding is missing.",
                            migratedRoot.Snapshot.ProjectExternalId,
                            true)));
                    continue;
                }

                var bindingRoot = migratedRoot;
                var bindingProject = projectSource;
                var bindingTeam = teamSource;
                workItems.Add(BindingWorkItem.Valid(
                    binding.SourceLegacyReference,
                    legacyRoot.WorkItemExternalId,
                    bindingRoot,
                    bindingProject,
                    bindingTeam,
                    null,
                    () => _mapper.MapTeamBinding(binding, bindingRoot, bindingProject, bindingTeam, state.MigrationTimestampUtc)));
            }
        }

        foreach (var binding in legacySnapshot.PipelineBindings)
        {
            if (!legacyRootsByProduct.TryGetValue(binding.ProductId, out var legacyRoots) || legacyRoots.Length == 0)
            {
                workItems.Add(BindingWorkItem.Invalid(
                    binding.SourceLegacyReference,
                    binding.PipelineExternalId,
                    new OnboardingMigrationDryRunIssuePlan(
                        "InconsistentLegacyReference",
                        "DependencyViolation",
                        OnboardingMigrationIssueSeverity.Blocking,
                        binding.SourceLegacyReference,
                        nameof(ProductSourceBinding),
                        binding.PipelineExternalId,
                        "The legacy pipeline binding does not have any product roots to bind.",
                        binding.ProductId.ToString(),
                        true)));
                continue;
            }

            foreach (var legacyRoot in legacyRoots)
            {
                if (!state.ProductRootsByLegacyKey.TryGetValue(CreateLegacyProductRootKey(legacyRoot.ProductId, legacyRoot.WorkItemExternalId), out var migratedRoot))
                {
                    workItems.Add(BindingWorkItem.Invalid(
                        binding.SourceLegacyReference,
                        legacyRoot.WorkItemExternalId,
                        new OnboardingMigrationDryRunIssuePlan(
                            "DependencyViolation",
                            "DependencyViolation",
                            OnboardingMigrationIssueSeverity.Blocking,
                            binding.SourceLegacyReference,
                            nameof(ProductSourceBinding),
                            legacyRoot.WorkItemExternalId,
                            "The migrated product root required for the pipeline binding is missing.",
                            legacyRoot.WorkItemExternalId,
                            true)));
                    continue;
                }

                if (!state.ProjectsByExternalId.TryGetValue(migratedRoot.Snapshot.ProjectExternalId, out var projectSource))
                {
                    workItems.Add(BindingWorkItem.Invalid(
                        binding.SourceLegacyReference,
                        legacyRoot.WorkItemExternalId,
                        new OnboardingMigrationDryRunIssuePlan(
                            "DependencyViolation",
                            "DependencyViolation",
                            OnboardingMigrationIssueSeverity.Blocking,
                            binding.SourceLegacyReference,
                            nameof(ProductSourceBinding),
                            legacyRoot.WorkItemExternalId,
                            "The migrated project source required for the pipeline binding is missing.",
                            migratedRoot.Snapshot.ProjectExternalId,
                            true)));
                    continue;
                }

                if (!state.PipelinesByScopedKey.TryGetValue(CreateScopedPipelineKey(projectSource.Id, binding.PipelineExternalId), out var pipelineSource))
                {
                    workItems.Add(BindingWorkItem.Invalid(
                        binding.SourceLegacyReference,
                        legacyRoot.WorkItemExternalId,
                        new OnboardingMigrationDryRunIssuePlan(
                            "DependencyViolation",
                            "DependencyViolation",
                            OnboardingMigrationIssueSeverity.Blocking,
                            binding.SourceLegacyReference,
                            nameof(ProductSourceBinding),
                            binding.PipelineExternalId,
                            "The migrated pipeline source required for the pipeline binding is missing.",
                            binding.PipelineExternalId,
                            true)));
                    continue;
                }

                var bindingRoot = migratedRoot;
                var bindingProject = projectSource;
                var bindingPipeline = pipelineSource;
                workItems.Add(BindingWorkItem.Valid(
                    binding.SourceLegacyReference,
                    binding.PipelineExternalId,
                    bindingRoot,
                    bindingProject,
                    null,
                    bindingPipeline,
                    () => _mapper.MapPipelineBinding(binding, bindingRoot, bindingProject, bindingPipeline, state.MigrationTimestampUtc)));
            }
        }

        return workItems
            .OrderBy(item => item.ProductRoot?.WorkItemExternalId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SourceLegacyReference, StringComparer.Ordinal)
            .ThenBy(item => item.TargetExternalIdentity ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private async Task CompleteUnitAsync(
        Guid unitIdentifier,
        int processed,
        int succeeded,
        int failed,
        bool hasBlockingIssue,
        CancellationToken cancellationToken)
    {
        var outcome = new OnboardingMigrationUnitOutcome(processed, succeeded, failed, 0);
        if (hasBlockingIssue)
        {
            await _ledgerService.FailUnitAsync(unitIdentifier, outcome, cancellationToken);
        }
        else
        {
            await _ledgerService.CompleteUnitAsync(unitIdentifier, outcome, cancellationToken);
        }
    }

    private async Task RecordValidationIssueAsync(
        Guid runIdentifier,
        Guid unitIdentifier,
        OnboardingMigrationMappingContext context,
        OnboardingErrorDto error,
        CancellationToken cancellationToken)
    {
        await RecordIssueAsync(
            runIdentifier,
            unitIdentifier,
            "ValidationFailure",
            error.Code.ToString(),
            OnboardingMigrationIssueSeverity.Blocking,
            context.SourceLegacyReference,
            context.TargetEntityType,
            context.TargetExternalIdentity,
            error.Message,
            error.Details,
            isBlocking: true,
            cancellationToken);
    }

    private Task RecordBlockingIssueAsync(
        Guid runIdentifier,
        Guid? unitIdentifier,
        string issueType,
        string issueCategory,
        string sourceLegacyReference,
        string targetEntityType,
        string? targetExternalIdentity,
        string message,
        string? details,
        CancellationToken cancellationToken)
        => RecordIssueAsync(
            runIdentifier,
            unitIdentifier,
            issueType,
            issueCategory,
            OnboardingMigrationIssueSeverity.Blocking,
            sourceLegacyReference,
            targetEntityType,
            targetExternalIdentity,
            message,
            details,
            isBlocking: true,
            cancellationToken);

    private async Task RecordIssueAsync(
        Guid runIdentifier,
        Guid? unitIdentifier,
        string issueType,
        string issueCategory,
        OnboardingMigrationIssueSeverity severity,
        string sourceLegacyReference,
        string targetEntityType,
        string? targetExternalIdentity,
        string message,
        string? details,
        bool isBlocking,
        CancellationToken cancellationToken)
    {
        await _ledgerService.RecordIssueAsync(
            runIdentifier,
            new OnboardingMigrationIssueCreateRequest(
                unitIdentifier,
                issueType,
                issueCategory,
                severity,
                sourceLegacyReference,
                targetEntityType,
                targetExternalIdentity,
                message,
                details,
                isBlocking),
            cancellationToken);
    }

    private async Task<OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>> GetProjectsLookupAsync(
        ExecutionState state,
        TfsConnection connection,
        CancellationToken cancellationToken)
    {
        if (state.ProjectsLookup is not null)
        {
            return state.ProjectsLookup;
        }

        var result = await _lookupClient.GetProjectsAsync(connection, null, int.MaxValue, 0, cancellationToken);
        state.ProjectsLookup = result.Succeeded
            ? OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>.Success(
                result.Data!
                    .OrderBy(item => item.ProjectExternalId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray())
            : result;
        return state.ProjectsLookup;
    }

    private async Task<OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> GetTeamsLookupAsync(
        ExecutionState state,
        TfsConnection connection,
        string projectExternalId,
        CancellationToken cancellationToken)
    {
        if (state.TeamLookupsByProjectExternalId.TryGetValue(projectExternalId, out var lookup))
        {
            return lookup;
        }

        var result = await _lookupClient.GetTeamsAsync(connection, projectExternalId, null, int.MaxValue, 0, cancellationToken);
        lookup = result.Succeeded
            ? OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>.Success(
                result.Data!
                    .OrderBy(item => item.TeamExternalId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray())
            : result;
        state.TeamLookupsByProjectExternalId[projectExternalId] = lookup;
        return lookup;
    }

    private async Task<OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> GetPipelinesLookupAsync(
        ExecutionState state,
        TfsConnection connection,
        string projectExternalId,
        Guid unitIdentifier,
        CancellationToken cancellationToken)
    {
        if (state.PipelineLookupsByProjectExternalId.TryGetValue(projectExternalId, out var lookup))
        {
            return lookup;
        }

        var result = await _lookupClient.GetPipelinesAsync(connection, projectExternalId, null, int.MaxValue, 0, cancellationToken);
        lookup = result.Succeeded
            ? OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>.Success(
                result.Data!
                    .OrderBy(item => item.PipelineExternalId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.ProjectExternalId, StringComparer.OrdinalIgnoreCase)
                    .ToArray())
            : result;
        state.PipelineLookupsByProjectExternalId[projectExternalId] = lookup;

        if (!lookup.Succeeded)
        {
            await RecordBlockingIssueAsync(
                state.RunIdentifier,
                unitIdentifier,
                "DiscoveryFailure",
                lookup.Error!.Code.ToString(),
                $"PipelineDiscovery:{projectExternalId}",
                nameof(ProjectSource),
                projectExternalId,
                lookup.Error.Message,
                lookup.Error.Details,
                cancellationToken);
        }

        return lookup;
    }

    private async Task<OnboardingOperationResult<WorkItemLookupResultDto>> GetWorkItemLookupAsync(
        ExecutionState state,
        TfsConnection connection,
        string workItemExternalId,
        CancellationToken cancellationToken)
    {
        if (state.WorkItemLookupsByExternalId.TryGetValue(workItemExternalId, out var lookup))
        {
            return lookup;
        }

        lookup = await _lookupClient.GetWorkItemAsync(connection, workItemExternalId, cancellationToken);
        state.WorkItemLookupsByExternalId[workItemExternalId] = lookup;
        return lookup;
    }

    private async Task<TfsConnection> UpsertConnectionAsync(TfsConnection candidate, ExecutionState state, CancellationToken cancellationToken)
    {
        if (state.IsDryRun)
        {
            if (!state.SimulatedConnectionsByKey.TryGetValue(candidate.ConnectionKey, out var simulated))
            {
                simulated = candidate;
                simulated.Id = state.NextSyntheticId++;
                state.SimulatedConnectionsByKey[candidate.ConnectionKey] = simulated;
            }
            else
            {
                CopyConnection(candidate, simulated, state.MigrationTimestampUtc);
            }

            return simulated;
        }

        var entity = await _dbContext.OnboardingTfsConnections
            .SingleOrDefaultAsync(item => item.ConnectionKey == candidate.ConnectionKey, cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            entity.CreatedAtUtc = state.MigrationTimestampUtc;
            entity.UpdatedAtUtc = state.MigrationTimestampUtc;
            _dbContext.OnboardingTfsConnections.Add(entity);
        }
        else
        {
            CopyConnection(candidate, entity, state.MigrationTimestampUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<ProjectSource> UpsertProjectAsync(ProjectSource candidate, TfsConnection connection, ExecutionState state, CancellationToken cancellationToken)
    {
        if (state.IsDryRun)
        {
            var key = CreateScopedProjectKey(connection.Id, candidate.ProjectExternalId);
            if (!state.SimulatedProjectsByKey.TryGetValue(key, out var simulated))
            {
                simulated = candidate;
                simulated.Id = state.NextSyntheticId++;
                simulated.TfsConnectionId = connection.Id;
                state.SimulatedProjectsByKey[key] = simulated;
            }
            else
            {
                CopyProject(candidate, simulated, connection.Id, state.MigrationTimestampUtc);
            }

            return simulated;
        }

        var entity = await _dbContext.OnboardingProjectSources
            .SingleOrDefaultAsync(
                item => item.TfsConnectionId == connection.Id && item.ProjectExternalId == candidate.ProjectExternalId,
                cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            entity.TfsConnectionId = connection.Id;
            entity.CreatedAtUtc = state.MigrationTimestampUtc;
            entity.UpdatedAtUtc = state.MigrationTimestampUtc;
            _dbContext.OnboardingProjectSources.Add(entity);
        }
        else
        {
            CopyProject(candidate, entity, connection.Id, state.MigrationTimestampUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<TeamSource> UpsertTeamAsync(TeamSource candidate, ProjectSource projectSource, ExecutionState state, CancellationToken cancellationToken)
    {
        if (state.IsDryRun)
        {
            var key = CreateScopedTeamKey(projectSource.Id, candidate.TeamExternalId);
            if (!state.SimulatedTeamsByKey.TryGetValue(key, out var simulated))
            {
                simulated = candidate;
                simulated.Id = state.NextSyntheticId++;
                simulated.ProjectSourceId = projectSource.Id;
                state.SimulatedTeamsByKey[key] = simulated;
            }
            else
            {
                CopyTeam(candidate, simulated, projectSource.Id, state.MigrationTimestampUtc);
            }

            return simulated;
        }

        var entity = await _dbContext.OnboardingTeamSources
            .SingleOrDefaultAsync(
                item => item.ProjectSourceId == projectSource.Id && item.TeamExternalId == candidate.TeamExternalId,
                cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            entity.ProjectSourceId = projectSource.Id;
            entity.CreatedAtUtc = state.MigrationTimestampUtc;
            entity.UpdatedAtUtc = state.MigrationTimestampUtc;
            _dbContext.OnboardingTeamSources.Add(entity);
        }
        else
        {
            CopyTeam(candidate, entity, projectSource.Id, state.MigrationTimestampUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<PipelineSource> UpsertPipelineAsync(PipelineSource candidate, ProjectSource projectSource, ExecutionState state, CancellationToken cancellationToken)
    {
        if (state.IsDryRun)
        {
            var key = CreateScopedPipelineKey(projectSource.Id, candidate.PipelineExternalId);
            if (!state.SimulatedPipelinesByKey.TryGetValue(key, out var simulated))
            {
                simulated = candidate;
                simulated.Id = state.NextSyntheticId++;
                simulated.ProjectSourceId = projectSource.Id;
                state.SimulatedPipelinesByKey[key] = simulated;
            }
            else
            {
                CopyPipeline(candidate, simulated, projectSource.Id, state.MigrationTimestampUtc);
            }

            return simulated;
        }

        var entity = await _dbContext.OnboardingPipelineSources
            .SingleOrDefaultAsync(
                item => item.ProjectSourceId == projectSource.Id && item.PipelineExternalId == candidate.PipelineExternalId,
                cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            entity.ProjectSourceId = projectSource.Id;
            entity.CreatedAtUtc = state.MigrationTimestampUtc;
            entity.UpdatedAtUtc = state.MigrationTimestampUtc;
            _dbContext.OnboardingPipelineSources.Add(entity);
        }
        else
        {
            CopyPipeline(candidate, entity, projectSource.Id, state.MigrationTimestampUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<ProductRoot> UpsertProductRootAsync(ProductRoot candidate, ProjectSource projectSource, ExecutionState state, CancellationToken cancellationToken)
    {
        if (state.IsDryRun)
        {
            var key = CreateScopedProductRootKey(projectSource.Id, candidate.WorkItemExternalId);
            if (!state.SimulatedProductRootsByKey.TryGetValue(key, out var simulated))
            {
                simulated = candidate;
                simulated.Id = state.NextSyntheticId++;
                simulated.ProjectSourceId = projectSource.Id;
                state.SimulatedProductRootsByKey[key] = simulated;
            }
            else
            {
                CopyProductRoot(candidate, simulated, projectSource.Id, state.MigrationTimestampUtc);
            }

            return simulated;
        }

        var entity = await _dbContext.OnboardingProductRoots
            .SingleOrDefaultAsync(
                item => item.ProjectSourceId == projectSource.Id && item.WorkItemExternalId == candidate.WorkItemExternalId,
                cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            entity.ProjectSourceId = projectSource.Id;
            entity.CreatedAtUtc = state.MigrationTimestampUtc;
            entity.UpdatedAtUtc = state.MigrationTimestampUtc;
            _dbContext.OnboardingProductRoots.Add(entity);
        }
        else
        {
            CopyProductRoot(candidate, entity, projectSource.Id, state.MigrationTimestampUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<ProductSourceBinding> UpsertBindingAsync(
        ProductSourceBinding candidate,
        ProductRoot productRoot,
        ProjectSource projectSource,
        TeamSource? teamSource,
        PipelineSource? pipelineSource,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        if (state.IsDryRun)
        {
            var key = CreateBindingKey(productRoot.Id, candidate.SourceType, candidate.SourceExternalId);
            if (!state.SimulatedBindingsByKey.TryGetValue(key, out var simulated))
            {
                simulated = candidate;
                simulated.Id = state.NextSyntheticId++;
                simulated.ProductRootId = productRoot.Id;
                simulated.ProjectSourceId = projectSource.Id;
                simulated.TeamSourceId = teamSource?.Id;
                simulated.PipelineSourceId = pipelineSource?.Id;
                state.SimulatedBindingsByKey[key] = simulated;
            }
            else
            {
                CopyBinding(candidate, simulated, productRoot.Id, projectSource.Id, teamSource?.Id, pipelineSource?.Id, state.MigrationTimestampUtc);
            }

            return simulated;
        }

        var entity = await _dbContext.OnboardingProductSourceBindings
            .SingleOrDefaultAsync(
                item => item.ProductRootId == productRoot.Id
                    && item.SourceType == candidate.SourceType
                    && item.SourceExternalId == candidate.SourceExternalId,
                cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            entity.ProductRootId = productRoot.Id;
            entity.ProjectSourceId = projectSource.Id;
            entity.TeamSourceId = teamSource?.Id;
            entity.PipelineSourceId = pipelineSource?.Id;
            entity.CreatedAtUtc = state.MigrationTimestampUtc;
            entity.UpdatedAtUtc = state.MigrationTimestampUtc;
            _dbContext.OnboardingProductSourceBindings.Add(entity);
        }
        else
        {
            CopyBinding(candidate, entity, productRoot.Id, projectSource.Id, teamSource?.Id, pipelineSource?.Id, state.MigrationTimestampUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private static void ApplyConnectionValidation(TfsConnection entity, TfsConnectionValidationResultDto validation)
    {
        entity.AvailabilityValidationState = MapValidationState(validation.AvailabilityValidationState);
        entity.PermissionValidationState = MapValidationState(validation.PermissionValidationState);
        entity.CapabilityValidationState = MapValidationState(validation.CapabilityValidationState);
        entity.LastSuccessfulValidationAtUtc = validation.LastSuccessfulValidationAtUtc;
        entity.LastAttemptedValidationAtUtc = validation.LastAttemptedValidationAtUtc;
        entity.ValidationFailureReason = validation.ValidationFailureReason;
        entity.LastVerifiedCapabilitiesSummary = validation.LastVerifiedCapabilitiesSummary;
    }

    private static void ApplyProjectValidation(ProjectSource entity, ProjectSourceValidationResultDto validation)
    {
        entity.ProjectExternalId = validation.ProjectExternalId;
        entity.Snapshot = new ProjectSnapshot
        {
            ProjectExternalId = validation.Snapshot.ProjectExternalId,
            Name = validation.Snapshot.Name,
            Description = validation.Snapshot.Description,
            Metadata = MapMetadata(validation.Snapshot.Metadata)
        };
        entity.ValidationState = MapValidationState(validation.ValidationState);
    }

    private static void ApplyTeamValidation(TeamSource entity, TeamSourceValidationResultDto validation)
    {
        entity.TeamExternalId = validation.TeamExternalId;
        entity.Snapshot = new TeamSnapshot
        {
            TeamExternalId = validation.Snapshot.TeamExternalId,
            ProjectExternalId = validation.Snapshot.ProjectExternalId,
            Name = validation.Snapshot.Name,
            DefaultAreaPath = validation.Snapshot.DefaultAreaPath,
            Description = validation.Snapshot.Description,
            Metadata = MapMetadata(validation.Snapshot.Metadata)
        };
        entity.ValidationState = MapValidationState(validation.ValidationState);
    }

    private static void ApplyPipelineValidation(PipelineSource entity, PipelineSourceValidationResultDto validation)
    {
        entity.PipelineExternalId = validation.PipelineExternalId;
        entity.Snapshot = new PipelineSnapshot
        {
            PipelineExternalId = validation.Snapshot.PipelineExternalId,
            ProjectExternalId = validation.Snapshot.ProjectExternalId,
            Name = validation.Snapshot.Name,
            Folder = validation.Snapshot.Folder,
            YamlPath = validation.Snapshot.YamlPath,
            RepositoryExternalId = validation.Snapshot.RepositoryExternalId,
            RepositoryName = validation.Snapshot.RepositoryName,
            Metadata = MapMetadata(validation.Snapshot.Metadata)
        };
        entity.ValidationState = MapValidationState(validation.ValidationState);
    }

    private static void ApplyProductRootValidation(ProductRoot entity, ProductRootValidationResultDto validation)
    {
        entity.WorkItemExternalId = validation.WorkItemExternalId;
        entity.Snapshot = new ProductRootSnapshot
        {
            WorkItemExternalId = validation.Snapshot.WorkItemExternalId,
            Title = validation.Snapshot.Title,
            WorkItemType = validation.Snapshot.WorkItemType,
            State = validation.Snapshot.State,
            ProjectExternalId = validation.Snapshot.ProjectExternalId,
            AreaPath = validation.Snapshot.AreaPath,
            Metadata = MapMetadata(validation.Snapshot.Metadata)
        };
        entity.ValidationState = MapValidationState(validation.ValidationState);
    }

    private static void ApplyBindingValidation(ProductSourceBinding entity, ProductSourceBindingValidationResultDto validation)
    {
        entity.SourceExternalId = validation.SourceExternalId;
        entity.ValidationState = MapValidationState(validation.ValidationState);
    }

    private static OnboardingValidationState MapValidationState(OnboardingValidationStateDto validationState)
        => new()
        {
            Status = validationState.Status.ToString(),
            ValidatedAtUtc = validationState.CheckedAtUtc,
            ErrorCode = validationState.ErrorCode,
            Message = validationState.ErrorMessageSanitized,
            IsRetryable = false
        };

    private static OnboardingSnapshotMetadata MapMetadata(SnapshotMetadataDto metadata)
        => new()
        {
            ConfirmedAtUtc = metadata.ConfirmedAtUtc,
            LastSeenAtUtc = metadata.LastSeenAtUtc,
            IsCurrent = metadata.IsCurrent,
            RenameDetected = metadata.RenameDetected ?? false,
            StaleReason = metadata.StaleReason
        };

    private static void CopyConnection(TfsConnection source, TfsConnection target, DateTime migrationTimestampUtc)
    {
        target.OrganizationUrl = source.OrganizationUrl;
        target.AuthenticationMode = source.AuthenticationMode;
        target.TimeoutSeconds = source.TimeoutSeconds;
        target.ApiVersion = source.ApiVersion;
        target.AvailabilityValidationState = source.AvailabilityValidationState;
        target.PermissionValidationState = source.PermissionValidationState;
        target.CapabilityValidationState = source.CapabilityValidationState;
        target.LastSuccessfulValidationAtUtc = source.LastSuccessfulValidationAtUtc;
        target.LastAttemptedValidationAtUtc = source.LastAttemptedValidationAtUtc;
        target.ValidationFailureReason = source.ValidationFailureReason;
        target.LastVerifiedCapabilitiesSummary = source.LastVerifiedCapabilitiesSummary;
        target.UpdatedAtUtc = migrationTimestampUtc;
    }

    private static void CopyProject(ProjectSource source, ProjectSource target, int connectionId, DateTime migrationTimestampUtc)
    {
        target.TfsConnectionId = connectionId;
        target.Enabled = source.Enabled;
        target.Snapshot = source.Snapshot;
        target.ValidationState = source.ValidationState;
        target.UpdatedAtUtc = migrationTimestampUtc;
    }

    private static void CopyTeam(TeamSource source, TeamSource target, int projectSourceId, DateTime migrationTimestampUtc)
    {
        target.ProjectSourceId = projectSourceId;
        target.Enabled = source.Enabled;
        target.Snapshot = source.Snapshot;
        target.ValidationState = source.ValidationState;
        target.UpdatedAtUtc = migrationTimestampUtc;
    }

    private static void CopyPipeline(PipelineSource source, PipelineSource target, int projectSourceId, DateTime migrationTimestampUtc)
    {
        target.ProjectSourceId = projectSourceId;
        target.Enabled = source.Enabled;
        target.Snapshot = source.Snapshot;
        target.ValidationState = source.ValidationState;
        target.UpdatedAtUtc = migrationTimestampUtc;
    }

    private static void CopyProductRoot(ProductRoot source, ProductRoot target, int projectSourceId, DateTime migrationTimestampUtc)
    {
        target.ProjectSourceId = projectSourceId;
        target.Enabled = source.Enabled;
        target.Snapshot = source.Snapshot;
        target.ValidationState = source.ValidationState;
        target.UpdatedAtUtc = migrationTimestampUtc;
    }

    private static void CopyBinding(
        ProductSourceBinding source,
        ProductSourceBinding target,
        int productRootId,
        int projectSourceId,
        int? teamSourceId,
        int? pipelineSourceId,
        DateTime migrationTimestampUtc)
    {
        target.ProductRootId = productRootId;
        target.ProjectSourceId = projectSourceId;
        target.TeamSourceId = teamSourceId;
        target.PipelineSourceId = pipelineSourceId;
        target.Enabled = source.Enabled;
        target.ValidationState = source.ValidationState;
        target.UpdatedAtUtc = migrationTimestampUtc;
    }

    private static string CreateRunLockKey(TfsConfigEntity? connection)
        => $"onboarding-migration:{(connection?.Url?.Trim() ?? "missing-connection")}";

    private static ScopedProjectKey CreateScopedProjectKey(int connectionId, string projectExternalId)
        => new(connectionId, NormalizeKey(projectExternalId));

    private static ScopedTeamKey CreateScopedTeamKey(int projectSourceId, string teamExternalId)
        => new(projectSourceId, NormalizeKey(teamExternalId));

    private static ScopedPipelineKey CreateScopedPipelineKey(int projectSourceId, string pipelineExternalId)
        => new(projectSourceId, NormalizeKey(pipelineExternalId));

    private static ScopedProductRootKey CreateScopedProductRootKey(int projectSourceId, string workItemExternalId)
        => new(projectSourceId, NormalizeKey(workItemExternalId));

    private static BindingKey CreateBindingKey(int productRootId, ProductSourceType sourceType, string sourceExternalId)
        => new(productRootId, sourceType, NormalizeKey(sourceExternalId));

    private static LegacyProductRootKey CreateLegacyProductRootKey(int productId, string workItemExternalId)
        => new(productId, NormalizeKey(workItemExternalId));

    private static LegacyPipelineKey CreateLegacyPipelineKey(int productId, string pipelineExternalId)
        => new(productId, NormalizeKey(pipelineExternalId));

    private static string NormalizeKey(string value)
        => value.Trim().ToUpperInvariant();

    private sealed class ExecutionState
    {
        public ExecutionState(Guid runIdentifier, OnboardingMigrationExecutionMode executionMode, DateTime migrationTimestampUtc)
        {
            RunIdentifier = runIdentifier;
            ExecutionMode = executionMode;
            MigrationTimestampUtc = migrationTimestampUtc;
        }

        public Guid RunIdentifier { get; }

        public OnboardingMigrationExecutionMode ExecutionMode { get; }

        public DateTime MigrationTimestampUtc { get; }

        public bool IsDryRun => ExecutionMode == OnboardingMigrationExecutionMode.DryRun;

        public int NextSyntheticId { get; set; } = 1;

        public TfsConnection? Connection { get; set; }

        public OnboardingOperationResult<IReadOnlyList<ProjectLookupResultDto>>? ProjectsLookup { get; set; }

        public Dictionary<string, OnboardingOperationResult<IReadOnlyList<TeamLookupResultDto>>> TeamLookupsByProjectExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, OnboardingOperationResult<IReadOnlyList<PipelineLookupResultDto>>> PipelineLookupsByProjectExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, OnboardingOperationResult<WorkItemLookupResultDto>> WorkItemLookupsByExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ProjectSource> ProjectsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ProjectSource> ProjectsByExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<int, TeamSource> TeamsByLegacyId { get; } = new();

        public Dictionary<ScopedTeamKey, TeamSource> TeamsByScopedKey { get; } = new();

        public Dictionary<ScopedPipelineKey, PipelineSource> PipelinesByScopedKey { get; } = new();

        public Dictionary<LegacyProductRootKey, ProductRoot> ProductRootsByLegacyKey { get; } = new();

        public Dictionary<LegacyPipelineKey, string> ProjectExternalIdByLegacyPipeline { get; } = new();

        public Dictionary<string, TfsConnection> SimulatedConnectionsByKey { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<ScopedProjectKey, ProjectSource> SimulatedProjectsByKey { get; } = new();

        public Dictionary<ScopedTeamKey, TeamSource> SimulatedTeamsByKey { get; } = new();

        public Dictionary<ScopedPipelineKey, PipelineSource> SimulatedPipelinesByKey { get; } = new();

        public Dictionary<ScopedProductRootKey, ProductRoot> SimulatedProductRootsByKey { get; } = new();

        public Dictionary<BindingKey, ProductSourceBinding> SimulatedBindingsByKey { get; } = new();
    }

    private sealed record ProjectResolutionResult(
        IReadOnlyList<ResolvedProjectCandidate> Candidates,
        int FailedEntityCount,
        bool HasBlockingIssue);

    private sealed record ResolvedProjectCandidate(
        LegacyProjectReference Reference,
        ProjectLookupResultDto Project);

    private sealed record BindingWorkItem(
        string SourceLegacyReference,
        string? TargetExternalIdentity,
        ProductRoot? ProductRoot,
        ProjectSource? ProjectSource,
        TeamSource? TeamSource,
        PipelineSource? PipelineSource,
        Func<MappedOnboardingEntity<ProductSourceBinding>>? Map,
        OnboardingMigrationDryRunIssuePlan? Issue)
    {
        public static BindingWorkItem Valid(
            string sourceLegacyReference,
            string? targetExternalIdentity,
            ProductRoot productRoot,
            ProjectSource projectSource,
            TeamSource? teamSource,
            PipelineSource? pipelineSource,
            Func<MappedOnboardingEntity<ProductSourceBinding>> map)
            => new(sourceLegacyReference, targetExternalIdentity, productRoot, projectSource, teamSource, pipelineSource, map, null);

        public static BindingWorkItem Invalid(
            string sourceLegacyReference,
            string? targetExternalIdentity,
            OnboardingMigrationDryRunIssuePlan issue)
            => new(sourceLegacyReference, targetExternalIdentity, null, null, null, null, null, issue);
    }

    private readonly record struct ScopedProjectKey(int ConnectionId, string ProjectExternalId);
    private readonly record struct ScopedTeamKey(int ProjectSourceId, string TeamExternalId);
    private readonly record struct ScopedPipelineKey(int ProjectSourceId, string PipelineExternalId);
    private readonly record struct ScopedProductRootKey(int ProjectSourceId, string WorkItemExternalId);
    private readonly record struct BindingKey(int ProductRootId, ProductSourceType SourceType, string SourceExternalId);
    private readonly record struct LegacyProductRootKey(int ProductId, string WorkItemExternalId);
    private readonly record struct LegacyPipelineKey(int ProductId, string PipelineExternalId);
}
