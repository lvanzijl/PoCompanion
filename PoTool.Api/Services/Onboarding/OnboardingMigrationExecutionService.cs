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

    public OnboardingMigrationExecutionService(
        PoToolDbContext dbContext,
        IOnboardingLegacyMigrationReader legacyReader,
        IOnboardingMigrationMapper mapper,
        IOnboardingMigrationLedgerService ledgerService,
        IOnboardingValidationService validationService,
        IOnboardingLiveLookupClient lookupClient)
    {
        _dbContext = dbContext;
        _legacyReader = legacyReader;
        _mapper = mapper;
        _ledgerService = ledgerService;
        _validationService = validationService;
        _lookupClient = lookupClient;
    }

    public async Task<OnboardingMigrationRunSummary> ExecuteAsync(
        OnboardingMigrationExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MigrationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EnvironmentRing);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TriggerType);

        var legacySnapshot = await _legacyReader.ReadAsync(cancellationToken);
        var hasMatchingFingerprint = await _dbContext.OnboardingMigrationRuns
            .AsNoTracking()
            .AnyAsync(
                run => run.MigrationVersion == request.MigrationVersion.Trim()
                    && run.SourceFingerprint == legacySnapshot.SourceFingerprint,
                cancellationToken);

        var run = await _ledgerService.CreateRunAsync(
            new OnboardingMigrationRunCreateRequest(
                request.MigrationVersion.Trim(),
                request.EnvironmentRing.Trim(),
                request.TriggerType.Trim(),
                request.ExecutionMode,
                legacySnapshot.SourceFingerprint),
            cancellationToken);

        var units = await _ledgerService.CreateUnitsAsync(run.RunIdentifier, OrderedUnits, cancellationToken);
        var state = new ExecutionState(run.RunIdentifier, request.ExecutionMode, hasMatchingFingerprint);

        await ExecuteConnectionUnitAsync(legacySnapshot, units[0], state, cancellationToken);
        await ExecuteProjectUnitAsync(legacySnapshot, units[1], state, cancellationToken);
        await ExecuteTeamUnitAsync(legacySnapshot, units[2], state, cancellationToken);
        await ExecutePipelineUnitAsync(legacySnapshot, units[3], state, cancellationToken);
        await ExecuteProductRootUnitAsync(legacySnapshot, units[4], state, cancellationToken);
        await ExecuteBindingUnitAsync(legacySnapshot, units[5], state, cancellationToken);

        await _ledgerService.FinalizeRunAsync(run.RunIdentifier, cancellationToken);
        return await _ledgerService.GetRunSummaryAsync(run.RunIdentifier, cancellationToken);
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

        var mapped = _mapper.MapConnection(legacySnapshot.Connection);
        var validation = await _validationService.ValidateConnectionAsync(mapped.Entity, cancellationToken);
        if (!validation.Succeeded)
        {
            await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
            await _ledgerService.FailUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(1, 0, 1, 0), cancellationToken);
            return;
        }

        ApplyConnectionValidation(mapped.Entity, validation.Data!);
        state.Connection = state.ExecutionMode == OnboardingMigrationExecutionMode.Live
            ? await UpsertConnectionAsync(mapped.Entity, cancellationToken)
            : mapped.Entity;

        await _ledgerService.CompleteUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(1, 1, 0, 0), cancellationToken);
    }

    private async Task ExecuteProjectUnitAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        MigrationUnit unit,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        if (state.Connection is null)
        {
            await FailDependencyUnitAsync(
                state.RunIdentifier,
                unit,
                nameof(ProjectSource),
                "Validated connection data is required before project migration can run.",
                cancellationToken);
            return;
        }

        var availableProjectsLookup = await _lookupClient.GetProjectsAsync(state.Connection, null, int.MaxValue, 0, cancellationToken);
        if (!availableProjectsLookup.Succeeded)
        {
            await _ledgerService.StartUnitAsync(unit.UnitIdentifier, cancellationToken);
            await RecordBlockingIssueAsync(
                state.RunIdentifier,
                unit.UnitIdentifier,
                "ValidationFailure",
                availableProjectsLookup.Error!.Code.ToString(),
                "ProjectDiscovery",
                nameof(ProjectSource),
                null,
                availableProjectsLookup.Error.Message,
                availableProjectsLookup.Error.Details,
                cancellationToken);
            await _ledgerService.FailUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(1, 0, 1, 0), cancellationToken);
            return;
        }

        var candidateProjects = await ResolveProjectCandidatesAsync(
            legacySnapshot,
            state,
            availableProjectsLookup.Data!,
            cancellationToken);

        if (candidateProjects.Count == 0)
        {
            await _ledgerService.SkipUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 0, 0), cancellationToken);
            return;
        }

        await _ledgerService.StartUnitAsync(unit.UnitIdentifier, cancellationToken);

        var processed = 0;
        var succeeded = 0;
        var failed = 0;
        var hasBlockingIssue = false;

        foreach (var candidate in candidateProjects.Values.OrderBy(item => item.Project.Name, StringComparer.OrdinalIgnoreCase))
        {
            processed++;

            var mapped = _mapper.MapProject(candidate.Reference, candidate.Project);
            mapped.Entity.TfsConnectionId = state.Connection.Id;

            var validation = await _validationService.ValidateProjectSourceAsync(state.Connection, mapped.Entity, cancellationToken);
            if (!validation.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
                continue;
            }

            ApplyProjectValidation(mapped.Entity, validation.Data!);

            var persistedProject = state.ExecutionMode == OnboardingMigrationExecutionMode.Live
                ? await UpsertProjectAsync(mapped.Entity, state.Connection, cancellationToken)
                : mapped.Entity;

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
        if (state.Connection is null || state.ProjectsByExternalId.Count == 0)
        {
            await FailDependencyUnitAsync(
                state.RunIdentifier,
                unit,
                nameof(TeamSource),
                "Validated connection and project sources are required before team migration can run.",
                cancellationToken);
            return;
        }

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

            var mapped = _mapper.MapTeam(team, projectSource.ProjectExternalId);
            mapped.Entity.ProjectSourceId = projectSource.Id;

            var validation = await _validationService.ValidateTeamSourceAsync(state.Connection, projectSource, mapped.Entity, cancellationToken);
            if (!validation.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
                continue;
            }

            ApplyTeamValidation(mapped.Entity, validation.Data!);

            var persistedTeam = state.ExecutionMode == OnboardingMigrationExecutionMode.Live
                ? await UpsertTeamAsync(mapped.Entity, projectSource, cancellationToken)
                : mapped.Entity;

            succeeded++;
            state.TeamsByLegacyId[team.TeamId] = persistedTeam;
            state.TeamsByExternalId[persistedTeam.TeamExternalId] = persistedTeam;
        }

        await CompleteUnitAsync(unit.UnitIdentifier, processed, succeeded, failed, hasBlockingIssue, cancellationToken);
    }

    private async Task ExecutePipelineUnitAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        MigrationUnit unit,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        if (state.Connection is null || state.ProjectsByExternalId.Count == 0)
        {
            await FailDependencyUnitAsync(
                state.RunIdentifier,
                unit,
                nameof(PipelineSource),
                "Validated connection and project sources are required before pipeline migration can run.",
                cancellationToken);
            return;
        }

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

            if (!state.ProjectExternalIdsByPipelineExternalId.TryGetValue(pipeline.PipelineExternalId, out var projectExternalId)
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
                    "A migrated project source is required before the pipeline can be migrated.",
                    null,
                    cancellationToken);
                continue;
            }

            var mapped = _mapper.MapPipeline(pipeline, projectSource.ProjectExternalId);
            mapped.Entity.ProjectSourceId = projectSource.Id;

            var validation = await _validationService.ValidatePipelineSourceAsync(state.Connection, projectSource, mapped.Entity, cancellationToken);
            if (!validation.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
                continue;
            }

            ApplyPipelineValidation(mapped.Entity, validation.Data!);

            var persistedPipeline = state.ExecutionMode == OnboardingMigrationExecutionMode.Live
                ? await UpsertPipelineAsync(mapped.Entity, projectSource, cancellationToken)
                : mapped.Entity;

            succeeded++;
            state.PipelinesByExternalId[persistedPipeline.PipelineExternalId] = persistedPipeline;
        }

        await CompleteUnitAsync(unit.UnitIdentifier, processed, succeeded, failed, hasBlockingIssue, cancellationToken);
    }

    private async Task ExecuteProductRootUnitAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        MigrationUnit unit,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        if (state.Connection is null || state.ProjectsByExternalId.Count == 0)
        {
            await FailDependencyUnitAsync(
                state.RunIdentifier,
                unit,
                nameof(ProductRoot),
                "Validated connection and project sources are required before product root migration can run.",
                cancellationToken);
            return;
        }

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

            if (!state.ResolvedWorkItemsByExternalId.TryGetValue(productRoot.WorkItemExternalId, out var resolvedWorkItem))
            {
                var workItemLookup = await _lookupClient.GetWorkItemAsync(state.Connection!, productRoot.WorkItemExternalId, cancellationToken);
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

                resolvedWorkItem = workItemLookup.Data!;
                state.ResolvedWorkItemsByExternalId[productRoot.WorkItemExternalId] = resolvedWorkItem;
            }

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

            var mapped = _mapper.MapProductRoot(productRoot, projectSource.ProjectExternalId);
            mapped.Entity.ProjectSourceId = projectSource.Id;

            var validation = await _validationService.ValidateProductRootAsync(state.Connection, projectSource, mapped.Entity, cancellationToken);
            if (!validation.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
                continue;
            }

            ApplyProductRootValidation(mapped.Entity, validation.Data!);

            var persistedRoot = state.ExecutionMode == OnboardingMigrationExecutionMode.Live
                ? await UpsertProductRootAsync(mapped.Entity, projectSource, cancellationToken)
                : mapped.Entity;

            succeeded++;
            state.ProductRootsByExternalId[persistedRoot.WorkItemExternalId] = persistedRoot;

            if (!state.ProductRootsByProductId.TryGetValue(productRoot.ProductId, out var roots))
            {
                roots = [];
                state.ProductRootsByProductId[productRoot.ProductId] = roots;
            }

            if (roots.All(root => !root.WorkItemExternalId.Equals(persistedRoot.WorkItemExternalId, StringComparison.OrdinalIgnoreCase)))
            {
                roots.Add(persistedRoot);
            }
        }

        await CompleteUnitAsync(unit.UnitIdentifier, processed, succeeded, failed, hasBlockingIssue, cancellationToken);
    }

    private async Task ExecuteBindingUnitAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        MigrationUnit unit,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        if (state.Connection is null || state.ProjectsByExternalId.Count == 0 || state.ProductRootsByProductId.Count == 0)
        {
            await FailDependencyUnitAsync(
                state.RunIdentifier,
                unit,
                nameof(ProductSourceBinding),
                "Validated connection, project sources, and product roots are required before binding migration can run.",
                cancellationToken);
            return;
        }

        var bindingActions = BuildBindingActions(legacySnapshot, state);
        if (bindingActions.Count == 0)
        {
            await _ledgerService.SkipUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 0, 0), cancellationToken);
            return;
        }

        await _ledgerService.StartUnitAsync(unit.UnitIdentifier, cancellationToken);

        var processed = 0;
        var succeeded = 0;
        var failed = 0;
        var hasBlockingIssue = false;

        foreach (var bindingAction in bindingActions)
        {
            processed++;

            if (!state.ProjectsByExternalId.TryGetValue(bindingAction.ProjectExternalId, out var projectSource))
            {
                failed++;
                hasBlockingIssue = true;
                var mappedContext = bindingAction.Map().Context;
                await RecordBlockingIssueAsync(
                    state.RunIdentifier,
                    unit.UnitIdentifier,
                    "DependencyViolation",
                    "DependencyViolation",
                    mappedContext.SourceLegacyReference,
                    nameof(ProductSourceBinding),
                    mappedContext.TargetExternalIdentity,
                    "A migrated project source is required before the binding can be created.",
                    bindingAction.ProjectExternalId,
                    cancellationToken);
                continue;
            }

            var mapped = bindingAction.Map();
            var validation = await _validationService.ValidateProductSourceBindingAsync(
                state.Connection,
                projectSource,
                bindingAction.ProductRoot,
                mapped.Entity,
                bindingAction.TeamSource,
                bindingAction.PipelineSource,
                cancellationToken);

            if (!validation.Succeeded)
            {
                failed++;
                hasBlockingIssue = true;
                await RecordValidationIssueAsync(state.RunIdentifier, unit.UnitIdentifier, mapped.Context, validation.Error!, cancellationToken);
                continue;
            }

            ApplyBindingValidation(mapped.Entity, validation.Data!);

            if (state.ExecutionMode == OnboardingMigrationExecutionMode.Live)
            {
                await UpsertBindingAsync(
                    mapped.Entity,
                    bindingAction.ProductRoot,
                    projectSource,
                    bindingAction.TeamSource,
                    bindingAction.PipelineSource,
                    cancellationToken);
            }

            succeeded++;
        }

        await CompleteUnitAsync(unit.UnitIdentifier, processed, succeeded, failed, hasBlockingIssue, cancellationToken);
    }

    private async Task<Dictionary<string, ResolvedProjectCandidate>> ResolveProjectCandidatesAsync(
        LegacyOnboardingMigrationSnapshot legacySnapshot,
        ExecutionState state,
        IReadOnlyList<ProjectLookupResultDto> availableProjects,
        CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<string, ResolvedProjectCandidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in legacySnapshot.ProjectReferences)
        {
            var project = availableProjects.FirstOrDefault(item => item.Name.Equals(reference.ProjectName, StringComparison.OrdinalIgnoreCase));
            if (project is not null)
            {
                candidates.TryAdd(project.ProjectExternalId, new ResolvedProjectCandidate(reference, project));
            }
        }

        foreach (var productRoot in legacySnapshot.ProductRoots)
        {
            var workItemLookup = await _lookupClient.GetWorkItemAsync(state.Connection!, productRoot.WorkItemExternalId, cancellationToken);
            if (!workItemLookup.Succeeded)
            {
                continue;
            }

            var resolvedWorkItem = workItemLookup.Data!;
            state.ResolvedWorkItemsByExternalId[productRoot.WorkItemExternalId] = resolvedWorkItem;
            var project = availableProjects.FirstOrDefault(item => item.ProjectExternalId.Equals(resolvedWorkItem.ProjectExternalId, StringComparison.OrdinalIgnoreCase))
                ?? new ProjectLookupResultDto(resolvedWorkItem.ProjectExternalId, resolvedWorkItem.ProjectExternalId, null);

            candidates.TryAdd(
                project.ProjectExternalId,
                new ResolvedProjectCandidate(new LegacyProjectReference(productRoot.SourceLegacyReference, project.Name), project));
        }

        foreach (var project in availableProjects)
        {
            var pipelinesLookup = await _lookupClient.GetPipelinesAsync(state.Connection!, project.ProjectExternalId, null, int.MaxValue, 0, cancellationToken);
            if (!pipelinesLookup.Succeeded)
            {
                continue;
            }

            foreach (var pipeline in pipelinesLookup.Data!)
            {
                if (!legacySnapshot.Pipelines.Any(item => item.PipelineExternalId == pipeline.PipelineExternalId))
                {
                    continue;
                }

                state.ProjectExternalIdsByPipelineExternalId[pipeline.PipelineExternalId] = project.ProjectExternalId;
                candidates.TryAdd(
                    project.ProjectExternalId,
                    new ResolvedProjectCandidate(new LegacyProjectReference($"PipelineDiscovery:{pipeline.PipelineExternalId}", project.Name), project));
            }
        }

        return candidates;
    }

    private async Task FailDependencyUnitAsync(
        Guid runIdentifier,
        MigrationUnit unit,
        string targetEntityType,
        string message,
        CancellationToken cancellationToken)
    {
        await _ledgerService.StartUnitAsync(unit.UnitIdentifier, cancellationToken);
        await RecordBlockingIssueAsync(
            runIdentifier,
            unit.UnitIdentifier,
            "DependencyViolation",
            "DependencyViolation",
            unit.UnitName,
            targetEntityType,
            null,
            message,
            null,
            cancellationToken);
        await _ledgerService.FailUnitAsync(unit.UnitIdentifier, new OnboardingMigrationUnitOutcome(0, 0, 1, 0), cancellationToken);
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
        await _ledgerService.RecordIssueAsync(
            runIdentifier,
            new OnboardingMigrationIssueCreateRequest(
                unitIdentifier,
                "ValidationFailure",
                error.Code.ToString(),
                OnboardingMigrationIssueSeverity.Blocking,
                context.SourceLegacyReference,
                context.TargetEntityType,
                context.TargetExternalIdentity,
                error.Message,
                error.Details,
                true),
            cancellationToken);
    }

    private async Task RecordBlockingIssueAsync(
        Guid runIdentifier,
        Guid unitIdentifier,
        string issueType,
        string issueCategory,
        string sourceLegacyReference,
        string targetEntityType,
        string? targetExternalIdentity,
        string message,
        string? details,
        CancellationToken cancellationToken)
    {
        await _ledgerService.RecordIssueAsync(
            runIdentifier,
            new OnboardingMigrationIssueCreateRequest(
                unitIdentifier,
                issueType,
                issueCategory,
                OnboardingMigrationIssueSeverity.Blocking,
                sourceLegacyReference,
                targetEntityType,
                targetExternalIdentity,
                message,
                details,
                true),
            cancellationToken);
    }

    private async Task<TfsConnection> UpsertConnectionAsync(TfsConnection candidate, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.OnboardingTfsConnections
            .SingleOrDefaultAsync(item => item.ConnectionKey == candidate.ConnectionKey, cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            _dbContext.OnboardingTfsConnections.Add(entity);
        }
        else
        {
            entity.OrganizationUrl = candidate.OrganizationUrl;
            entity.AuthenticationMode = candidate.AuthenticationMode;
            entity.TimeoutSeconds = candidate.TimeoutSeconds;
            entity.ApiVersion = candidate.ApiVersion;
            entity.AvailabilityValidationState = candidate.AvailabilityValidationState;
            entity.PermissionValidationState = candidate.PermissionValidationState;
            entity.CapabilityValidationState = candidate.CapabilityValidationState;
            entity.LastSuccessfulValidationAtUtc = candidate.LastSuccessfulValidationAtUtc;
            entity.LastAttemptedValidationAtUtc = candidate.LastAttemptedValidationAtUtc;
            entity.ValidationFailureReason = candidate.ValidationFailureReason;
            entity.LastVerifiedCapabilitiesSummary = candidate.LastVerifiedCapabilitiesSummary;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<ProjectSource> UpsertProjectAsync(ProjectSource candidate, TfsConnection connection, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.OnboardingProjectSources
            .SingleOrDefaultAsync(item => item.ProjectExternalId == candidate.ProjectExternalId, cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            entity.TfsConnectionId = connection.Id;
            _dbContext.OnboardingProjectSources.Add(entity);
        }
        else
        {
            entity.TfsConnectionId = connection.Id;
            entity.Enabled = candidate.Enabled;
            entity.Snapshot = candidate.Snapshot;
            entity.ValidationState = candidate.ValidationState;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<TeamSource> UpsertTeamAsync(TeamSource candidate, ProjectSource projectSource, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.OnboardingTeamSources
            .SingleOrDefaultAsync(item => item.TeamExternalId == candidate.TeamExternalId, cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            entity.ProjectSourceId = projectSource.Id;
            _dbContext.OnboardingTeamSources.Add(entity);
        }
        else
        {
            entity.ProjectSourceId = projectSource.Id;
            entity.Enabled = candidate.Enabled;
            entity.Snapshot = candidate.Snapshot;
            entity.ValidationState = candidate.ValidationState;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<PipelineSource> UpsertPipelineAsync(PipelineSource candidate, ProjectSource projectSource, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.OnboardingPipelineSources
            .SingleOrDefaultAsync(item => item.PipelineExternalId == candidate.PipelineExternalId, cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            entity.ProjectSourceId = projectSource.Id;
            _dbContext.OnboardingPipelineSources.Add(entity);
        }
        else
        {
            entity.ProjectSourceId = projectSource.Id;
            entity.Enabled = candidate.Enabled;
            entity.Snapshot = candidate.Snapshot;
            entity.ValidationState = candidate.ValidationState;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<ProductRoot> UpsertProductRootAsync(ProductRoot candidate, ProjectSource projectSource, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.OnboardingProductRoots
            .SingleOrDefaultAsync(item => item.WorkItemExternalId == candidate.WorkItemExternalId, cancellationToken);

        if (entity is null)
        {
            entity = candidate;
            entity.ProjectSourceId = projectSource.Id;
            _dbContext.OnboardingProductRoots.Add(entity);
        }
        else
        {
            entity.ProjectSourceId = projectSource.Id;
            entity.Enabled = candidate.Enabled;
            entity.Snapshot = candidate.Snapshot;
            entity.ValidationState = candidate.ValidationState;
            entity.UpdatedAtUtc = DateTime.UtcNow;
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
        CancellationToken cancellationToken)
    {
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
            _dbContext.OnboardingProductSourceBindings.Add(entity);
        }
        else
        {
            entity.ProductRootId = productRoot.Id;
            entity.ProjectSourceId = projectSource.Id;
            entity.TeamSourceId = teamSource?.Id;
            entity.PipelineSourceId = pipelineSource?.Id;
            entity.Enabled = candidate.Enabled;
            entity.ValidationState = candidate.ValidationState;
            entity.UpdatedAtUtc = DateTime.UtcNow;
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

    private List<BindingAction> BuildBindingActions(LegacyOnboardingMigrationSnapshot legacySnapshot, ExecutionState state)
    {
        var actions = new List<BindingAction>();

        foreach (var (productId, roots) in state.ProductRootsByProductId)
        {
            foreach (var root in roots)
            {
                var projectReference = new LegacyProductRootMigrationRecord($"ProductBacklogRootEntity:{productId}:{root.WorkItemExternalId}", productId, root.WorkItemExternalId);
                actions.Add(new BindingAction(
                    root.Snapshot.ProjectExternalId,
                    root,
                    null,
                    null,
                    () => _mapper.MapProjectBinding(projectReference, root, state.ProjectsByExternalId[root.Snapshot.ProjectExternalId])));
            }
        }

        foreach (var binding in legacySnapshot.TeamBindings)
        {
            if (!state.ProductRootsByProductId.TryGetValue(binding.ProductId, out var roots)
                || !state.TeamsByLegacyId.TryGetValue(binding.TeamId, out var teamSource))
            {
                continue;
            }

            foreach (var root in roots)
            {
                actions.Add(new BindingAction(
                    root.Snapshot.ProjectExternalId,
                    root,
                    teamSource,
                    null,
                    () => _mapper.MapTeamBinding(binding, root, state.ProjectsByExternalId[root.Snapshot.ProjectExternalId], teamSource)));
            }
        }

        foreach (var binding in legacySnapshot.PipelineBindings)
        {
            if (!state.ProductRootsByProductId.TryGetValue(binding.ProductId, out var roots)
                || !state.PipelinesByExternalId.TryGetValue(binding.PipelineExternalId, out var pipelineSource))
            {
                continue;
            }

            foreach (var root in roots)
            {
                actions.Add(new BindingAction(
                    root.Snapshot.ProjectExternalId,
                    root,
                    null,
                    pipelineSource,
                    () => _mapper.MapPipelineBinding(binding, root, state.ProjectsByExternalId[root.Snapshot.ProjectExternalId], pipelineSource)));
            }
        }

        return actions;
    }

    private sealed class ExecutionState
    {
        public ExecutionState(Guid runIdentifier, OnboardingMigrationExecutionMode executionMode, bool hasMatchingFingerprint)
        {
            RunIdentifier = runIdentifier;
            ExecutionMode = executionMode;
            HasMatchingFingerprint = hasMatchingFingerprint;
        }

        public Guid RunIdentifier { get; }

        public OnboardingMigrationExecutionMode ExecutionMode { get; }

        public bool HasMatchingFingerprint { get; }

        public TfsConnection? Connection { get; set; }

        public Dictionary<string, ProjectSource> ProjectsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, ProjectSource> ProjectsByExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<int, TeamSource> TeamsByLegacyId { get; } = new();

        public Dictionary<string, TeamSource> TeamsByExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, PipelineSource> PipelinesByExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<int, List<ProductRoot>> ProductRootsByProductId { get; } = new();

        public Dictionary<string, ProductRoot> ProductRootsByExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, WorkItemLookupResultDto> ResolvedWorkItemsByExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> ProjectExternalIdsByPipelineExternalId { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record ResolvedProjectCandidate(
        LegacyProjectReference Reference,
        ProjectLookupResultDto Project);

    private sealed record BindingAction(
        string ProjectExternalId,
        ProductRoot ProductRoot,
        TeamSource? TeamSource,
        PipelineSource? PipelineSource,
        Func<MappedOnboardingEntity<ProductSourceBinding>> Map);
}
