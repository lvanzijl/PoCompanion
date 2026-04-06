using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingStatusService
{
    Task<OnboardingOperationResult<OnboardingStatusDto>> GetStatusAsync(CancellationToken cancellationToken);
}

public sealed class OnboardingStatusService : IOnboardingStatusService
{
    private const string BlockerSeverity = "Blocker";
    private const string WarningSeverity = "Warning";

    private readonly PoToolDbContext _dbContext;
    private readonly IOnboardingObservability _observability;

    public OnboardingStatusService(PoToolDbContext dbContext, IOnboardingObservability observability)
    {
        _dbContext = dbContext;
        _observability = observability;
    }

    public async Task<OnboardingOperationResult<OnboardingStatusDto>> GetStatusAsync(CancellationToken cancellationToken)
    {
        var connection = await OnboardingReadQueries.ActiveConnections(_dbContext)
            .OrderBy(connectionEntity => connectionEntity.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var projectSources = await OnboardingReadQueries.ActiveProjects(_dbContext)
            .OrderBy(project => project.ProjectExternalId)
            .ToListAsync(cancellationToken);

        var teamSources = await OnboardingReadQueries.ActiveTeams(_dbContext)
            .OrderBy(team => team.TeamExternalId)
            .ToListAsync(cancellationToken);

        var pipelineSources = await OnboardingReadQueries.ActivePipelines(_dbContext)
            .OrderBy(pipeline => pipeline.PipelineExternalId)
            .ToListAsync(cancellationToken);

        var productRoots = await OnboardingReadQueries.ActiveRoots(_dbContext)
            .OrderBy(root => root.WorkItemExternalId)
            .ToListAsync(cancellationToken);

        var bindings = await OnboardingReadQueries.ActiveBindings(_dbContext)
            .OrderBy(binding => binding.ProductRootId)
            .ThenBy(binding => binding.SourceType)
            .ThenBy(binding => binding.SourceExternalId)
            .ToListAsync(cancellationToken);

        var status = BuildStatus(connection, projectSources, teamSources, pipelineSources, productRoots, bindings);

        _observability.RecordStatusComputed(status.OverallStatus.ToString(), status.BlockingReasons.Count, status.Warnings.Count);
        _observability.LogStatusComputed(status.OverallStatus.ToString(), status.BlockingReasons.Count, status.Warnings.Count);

        foreach (var blocker in status.BlockingReasons)
        {
            _observability.LogStatusIssue(BlockerSeverity, blocker.Code, blocker.EntityType, blocker.EntityExternalId);
        }

        foreach (var warning in status.Warnings)
        {
            _observability.LogStatusIssue(WarningSeverity, warning.Code, warning.EntityType, warning.EntityExternalId);
        }

        return OnboardingOperationResult<OnboardingStatusDto>.Success(status);
    }

    private static OnboardingStatusDto BuildStatus(
        TfsConnection? connection,
        IReadOnlyList<ProjectSource> projectSources,
        IReadOnlyList<TeamSource> teamSources,
        IReadOnlyList<PipelineSource> pipelineSources,
        IReadOnlyList<ProductRoot> productRoots,
        IReadOnlyList<ProductSourceBinding> bindings)
    {
        var blockers = new List<OnboardingStatusIssueDto>();
        var warnings = new List<OnboardingStatusIssueDto>();

        var projectsById = projectSources.ToDictionary(project => project.Id);
        var teamsById = teamSources.ToDictionary(team => team.Id);
        var pipelinesById = pipelineSources.ToDictionary(pipeline => pipeline.Id);
        var rootsById = productRoots.ToDictionary(root => root.Id);

        var validProjectIds = projectSources
            .Where(project => project.Enabled && IsValidationStateValid(project.ValidationState))
            .Select(project => project.Id)
            .ToHashSet();

        var validTeamIds = teamSources
            .Where(team => team.Enabled && IsValidationStateValid(team.ValidationState) && validProjectIds.Contains(team.ProjectSourceId))
            .Select(team => team.Id)
            .ToHashSet();

        var validPipelineIds = pipelineSources
            .Where(pipeline => pipeline.Enabled && IsValidationStateValid(pipeline.ValidationState) && validProjectIds.Contains(pipeline.ProjectSourceId))
            .Select(pipeline => pipeline.Id)
            .ToHashSet();

        var validRootIds = productRoots
            .Where(root => root.Enabled && IsValidationStateValid(root.ValidationState) && validProjectIds.Contains(root.ProjectSourceId))
            .Select(root => root.Id)
            .ToHashSet();

        var validProjectBindingIds = bindings
            .Where(binding => IsValidProjectBinding(binding, rootsById, projectsById, validRootIds, validProjectIds))
            .Select(binding => binding.Id)
            .ToHashSet();

        var validProjectBindingScopes = bindings
            .Where(binding => validProjectBindingIds.Contains(binding.Id))
            .Select(binding => (binding.ProductRootId, binding.ProjectSourceId))
            .ToHashSet();

        var validTeamBindingIds = bindings
            .Where(binding => IsValidTeamBinding(binding, rootsById, teamsById, validRootIds, validProjectIds, validTeamIds, validProjectBindingScopes))
            .Select(binding => binding.Id)
            .ToHashSet();

        var validPipelineBindingIds = bindings
            .Where(binding => IsValidPipelineBinding(binding, rootsById, pipelinesById, validRootIds, validProjectIds, validPipelineIds, validProjectBindingScopes))
            .Select(binding => binding.Id)
            .ToHashSet();

        var validBindingIds = validProjectBindingIds
            .Concat(validTeamBindingIds)
            .Concat(validPipelineBindingIds)
            .ToHashSet();

        AddConnectionBlockers(blockers, connection);
        AddProjectBlockers(blockers, projectSources, validProjectIds);
        AddTeamBlockers(blockers, teamSources, projectsById, validProjectIds, validTeamIds);
        AddPipelineBlockers(blockers, pipelineSources, projectsById, validProjectIds, validPipelineIds);
        AddProductRootBlockers(blockers, productRoots, projectsById, validProjectIds, validRootIds, validProjectBindingScopes);
        AddBindingBlockers(blockers, bindings, rootsById, projectsById, teamsById, pipelinesById, validRootIds, validProjectIds, validTeamIds, validPipelineIds, validProjectBindingScopes, validBindingIds);

        AddSnapshotWarnings(warnings, projectSources.Select(project => ("ProjectSource", project.ProjectExternalId, project.Snapshot.Metadata)));
        AddSnapshotWarnings(warnings, teamSources.Select(team => ("TeamSource", team.TeamExternalId, team.Snapshot.Metadata)));
        AddSnapshotWarnings(warnings, pipelineSources.Select(pipeline => ("PipelineSource", pipeline.PipelineExternalId, pipeline.Snapshot.Metadata)));
        AddSnapshotWarnings(warnings, productRoots.Select(root => ("ProductRoot", root.WorkItemExternalId, root.Snapshot.Metadata)));

        var connectionValid = IsConnectionValid(connection);
        var anyOnboardingEntityExists = connection is not null
            || projectSources.Count > 0
            || teamSources.Count > 0
            || pipelineSources.Count > 0
            || productRoots.Count > 0
            || bindings.Count > 0;

        var connectionStatus = connection is null
            ? OnboardingConfigurationStatus.NotConfigured
            : connectionValid
                ? OnboardingConfigurationStatus.Complete
                : OnboardingConfigurationStatus.PartiallyConfigured;

        var enabledInvalidProjects = projectSources.Any(project => project.Enabled && !validProjectIds.Contains(project.Id));
        var enabledInvalidTeams = teamSources.Any(team => team.Enabled && !validTeamIds.Contains(team.Id));
        var enabledInvalidPipelines = pipelineSources.Any(pipeline => pipeline.Enabled && !validPipelineIds.Contains(pipeline.Id));
        var dataSourceStatus = projectSources.Count == 0 && teamSources.Count == 0 && pipelineSources.Count == 0
            ? OnboardingConfigurationStatus.NotConfigured
            : connectionValid && validProjectIds.Count > 0 && !enabledInvalidProjects && !enabledInvalidTeams && !enabledInvalidPipelines
                ? OnboardingConfigurationStatus.Complete
                : OnboardingConfigurationStatus.PartiallyConfigured;

        var enabledInvalidRoots = productRoots.Any(root => root.Enabled && !validRootIds.Contains(root.Id));
        var enabledInvalidBindings = bindings.Any(binding => binding.Enabled && !validBindingIds.Contains(binding.Id));
        var anyEnabledRootMissingProjectBinding = productRoots
            .Where(root => root.Enabled && validRootIds.Contains(root.Id))
            .Any(root => !validProjectBindingScopes.Contains((root.Id, root.ProjectSourceId)));

        var domainStatus = productRoots.Count == 0 && bindings.Count == 0
            ? OnboardingConfigurationStatus.NotConfigured
            : connectionValid && validRootIds.Count > 0 && !enabledInvalidRoots && !enabledInvalidBindings && !anyEnabledRootMissingProjectBinding
                ? OnboardingConfigurationStatus.Complete
                : OnboardingConfigurationStatus.PartiallyConfigured;

        var overallStatus = !anyOnboardingEntityExists
            ? OnboardingConfigurationStatus.NotConfigured
            : connectionStatus == OnboardingConfigurationStatus.Complete
              && dataSourceStatus == OnboardingConfigurationStatus.Complete
              && domainStatus == OnboardingConfigurationStatus.Complete
                ? OnboardingConfigurationStatus.Complete
                : OnboardingConfigurationStatus.PartiallyConfigured;

        return new OnboardingStatusDto(
            overallStatus,
            connectionStatus,
            dataSourceStatus,
            domainStatus,
            blockers,
            warnings,
            new OnboardingStatusCountsDto(
                projectSources.Count,
                validProjectIds.Count,
                teamSources.Count,
                validTeamIds.Count,
                pipelineSources.Count,
                validPipelineIds.Count,
                productRoots.Count,
                validRootIds.Count,
                bindings.Count,
                validBindingIds.Count));
    }

    private static void AddConnectionBlockers(List<OnboardingStatusIssueDto> blockers, TfsConnection? connection)
    {
        if (connection is null)
        {
            blockers.Add(new OnboardingStatusIssueDto(
                "CONNECTION_REQUIRED",
                "A validated TFS connection is required.",
                "TfsConnection",
                null));
            return;
        }

        AddConnectionValidationBlocker(
            blockers,
            connection.AvailabilityValidationState,
            "CONNECTION_UNAVAILABLE",
            "The TFS connection is unavailable.",
            "CONNECTION_INVALID",
            "The TFS connection is invalid.");

        AddConnectionValidationBlocker(
            blockers,
            connection.PermissionValidationState,
            "CONNECTION_PERMISSION_DENIED",
            "The TFS connection is missing required permissions.",
            "CONNECTION_INVALID",
            "The TFS connection is invalid.");

        AddConnectionValidationBlocker(
            blockers,
            connection.CapabilityValidationState,
            "CONNECTION_CAPABILITY_DENIED",
            "The TFS connection is missing required capabilities.",
            "CONNECTION_INVALID",
            "The TFS connection is invalid.");
    }

    private static void AddProjectBlockers(
        List<OnboardingStatusIssueDto> blockers,
        IReadOnlyList<ProjectSource> projectSources,
        IReadOnlySet<int> validProjectIds)
    {
        if (validProjectIds.Count == 0)
        {
            blockers.Add(new OnboardingStatusIssueDto(
                "PROJECT_SOURCE_REQUIRED",
                "At least one enabled valid project source is required.",
                "ProjectSource",
                null));
        }

        foreach (var project in projectSources.Where(project => project.Enabled && !validProjectIds.Contains(project.Id)))
        {
            blockers.Add(new OnboardingStatusIssueDto(
                "PROJECT_SOURCE_INVALID",
                "The project source is enabled but not valid.",
                "ProjectSource",
                project.ProjectExternalId));
        }
    }

    private static void AddTeamBlockers(
        List<OnboardingStatusIssueDto> blockers,
        IReadOnlyList<TeamSource> teamSources,
        IReadOnlyDictionary<int, ProjectSource> projectsById,
        IReadOnlySet<int> validProjectIds,
        IReadOnlySet<int> validTeamIds)
    {
        foreach (var team in teamSources.Where(team => team.Enabled && !validTeamIds.Contains(team.Id)))
        {
            var code = !projectsById.ContainsKey(team.ProjectSourceId)
                ? "TEAM_PROJECT_MISSING"
                : !validProjectIds.Contains(team.ProjectSourceId)
                    ? "TEAM_PROJECT_INVALID"
                    : "TEAM_SOURCE_INVALID";

            var message = code switch
            {
                "TEAM_PROJECT_MISSING" => "The team source references a missing project source.",
                "TEAM_PROJECT_INVALID" => "The team source references a project source that is not enabled and valid.",
                _ => "The team source is enabled but not valid."
            };

            blockers.Add(new OnboardingStatusIssueDto(code, message, "TeamSource", team.TeamExternalId));
        }
    }

    private static void AddPipelineBlockers(
        List<OnboardingStatusIssueDto> blockers,
        IReadOnlyList<PipelineSource> pipelineSources,
        IReadOnlyDictionary<int, ProjectSource> projectsById,
        IReadOnlySet<int> validProjectIds,
        IReadOnlySet<int> validPipelineIds)
    {
        foreach (var pipeline in pipelineSources.Where(pipeline => pipeline.Enabled && !validPipelineIds.Contains(pipeline.Id)))
        {
            var code = !projectsById.ContainsKey(pipeline.ProjectSourceId)
                ? "PIPELINE_PROJECT_MISSING"
                : !validProjectIds.Contains(pipeline.ProjectSourceId)
                    ? "PIPELINE_PROJECT_INVALID"
                    : "PIPELINE_SOURCE_INVALID";

            var message = code switch
            {
                "PIPELINE_PROJECT_MISSING" => "The pipeline source references a missing project source.",
                "PIPELINE_PROJECT_INVALID" => "The pipeline source references a project source that is not enabled and valid.",
                _ => "The pipeline source is enabled but not valid."
            };

            blockers.Add(new OnboardingStatusIssueDto(code, message, "PipelineSource", pipeline.PipelineExternalId));
        }
    }

    private static void AddProductRootBlockers(
        List<OnboardingStatusIssueDto> blockers,
        IReadOnlyList<ProductRoot> productRoots,
        IReadOnlyDictionary<int, ProjectSource> projectsById,
        IReadOnlySet<int> validProjectIds,
        IReadOnlySet<int> validRootIds,
        IReadOnlySet<(int ProductRootId, int ProjectSourceId)> validProjectBindingScopes)
    {
        if (validRootIds.Count == 0)
        {
            blockers.Add(new OnboardingStatusIssueDto(
                "PRODUCT_ROOT_REQUIRED",
                "At least one enabled valid product root is required.",
                "ProductRoot",
                null));
        }

        foreach (var root in productRoots.Where(root => root.Enabled && !validRootIds.Contains(root.Id)))
        {
            var code = !projectsById.ContainsKey(root.ProjectSourceId)
                ? "PRODUCT_ROOT_PROJECT_MISSING"
                : !validProjectIds.Contains(root.ProjectSourceId)
                    ? "PRODUCT_ROOT_PROJECT_INVALID"
                    : "PRODUCT_ROOT_INVALID";

            var message = code switch
            {
                "PRODUCT_ROOT_PROJECT_MISSING" => "The product root references a missing project source.",
                "PRODUCT_ROOT_PROJECT_INVALID" => "The product root references a project source that is not enabled and valid.",
                _ => "The product root is enabled but not valid."
            };

            blockers.Add(new OnboardingStatusIssueDto(code, message, "ProductRoot", root.WorkItemExternalId));
        }

        foreach (var root in productRoots.Where(root => root.Enabled && validRootIds.Contains(root.Id)))
        {
            if (!validProjectBindingScopes.Contains((root.Id, root.ProjectSourceId)))
            {
                blockers.Add(new OnboardingStatusIssueDto(
                    "PRODUCT_ROOT_PROJECT_BINDING_REQUIRED",
                    "Every enabled valid product root requires an enabled valid project binding in the same project scope.",
                    "ProductRoot",
                    root.WorkItemExternalId));
            }
        }
    }

    private static void AddBindingBlockers(
        List<OnboardingStatusIssueDto> blockers,
        IReadOnlyList<ProductSourceBinding> bindings,
        IReadOnlyDictionary<int, ProductRoot> rootsById,
        IReadOnlyDictionary<int, ProjectSource> projectsById,
        IReadOnlyDictionary<int, TeamSource> teamsById,
        IReadOnlyDictionary<int, PipelineSource> pipelinesById,
        IReadOnlySet<int> validRootIds,
        IReadOnlySet<int> validProjectIds,
        IReadOnlySet<int> validTeamIds,
        IReadOnlySet<int> validPipelineIds,
        IReadOnlySet<(int ProductRootId, int ProjectSourceId)> validProjectBindingScopes,
        IReadOnlySet<int> validBindingIds)
    {
        foreach (var binding in bindings.Where(binding => binding.Enabled && !validBindingIds.Contains(binding.Id)))
        {
            if (!rootsById.TryGetValue(binding.ProductRootId, out var root))
            {
                blockers.Add(new OnboardingStatusIssueDto(
                    "BINDING_PRODUCT_ROOT_MISSING",
                    "The binding references a missing product root.",
                    "ProductSourceBinding",
                    binding.SourceExternalId));
                continue;
            }

            if (!validRootIds.Contains(binding.ProductRootId))
            {
                blockers.Add(new OnboardingStatusIssueDto(
                    "BINDING_PRODUCT_ROOT_INVALID",
                    "The binding references a product root that is not enabled and valid.",
                    "ProductSourceBinding",
                    binding.SourceExternalId));
                continue;
            }

            if (!projectsById.ContainsKey(binding.ProjectSourceId) || !validProjectIds.Contains(binding.ProjectSourceId))
            {
                blockers.Add(new OnboardingStatusIssueDto(
                    "BINDING_PROJECT_SOURCE_INVALID",
                    "The binding references a project source that is not enabled and valid.",
                    "ProductSourceBinding",
                    binding.SourceExternalId));
                continue;
            }

            if (binding.ProjectSourceId != root.ProjectSourceId)
            {
                blockers.Add(new OnboardingStatusIssueDto(
                    "BINDING_SCOPE_MISMATCH",
                    "The binding project scope does not match the product root project scope.",
                    "ProductSourceBinding",
                    binding.SourceExternalId));
                continue;
            }

            switch (binding.SourceType)
            {
                case ProductSourceType.Project:
                    blockers.Add(new OnboardingStatusIssueDto(
                        "PROJECT_BINDING_INVALID",
                        "The project binding is enabled but not valid.",
                        "ProductSourceBinding",
                        binding.SourceExternalId));
                    break;

                case ProductSourceType.Team:
                    if (binding.TeamSourceId is null || !teamsById.TryGetValue(binding.TeamSourceId.Value, out var team) || !validTeamIds.Contains(team.Id))
                    {
                        blockers.Add(new OnboardingStatusIssueDto(
                            "TEAM_BINDING_SOURCE_INVALID",
                            "The team binding references a team source that is not enabled and valid.",
                            "ProductSourceBinding",
                            binding.SourceExternalId));
                    }
                    else if (team.ProjectSourceId != binding.ProjectSourceId)
                    {
                        blockers.Add(new OnboardingStatusIssueDto(
                            "TEAM_BINDING_SCOPE_MISMATCH",
                            "The team binding does not match the team source project scope.",
                            "ProductSourceBinding",
                            binding.SourceExternalId));
                    }
                    else
                    {
                        blockers.Add(new OnboardingStatusIssueDto(
                            "TEAM_BINDING_PROJECT_BINDING_REQUIRED",
                            "A team binding requires an enabled valid project binding in the same project scope.",
                            "ProductSourceBinding",
                            binding.SourceExternalId));
                    }
                    break;

                case ProductSourceType.Pipeline:
                    if (binding.PipelineSourceId is null || !pipelinesById.TryGetValue(binding.PipelineSourceId.Value, out var pipeline) || !validPipelineIds.Contains(pipeline.Id))
                    {
                        blockers.Add(new OnboardingStatusIssueDto(
                            "PIPELINE_BINDING_SOURCE_INVALID",
                            "The pipeline binding references a pipeline source that is not enabled and valid.",
                            "ProductSourceBinding",
                            binding.SourceExternalId));
                    }
                    else if (pipeline.ProjectSourceId != binding.ProjectSourceId)
                    {
                        blockers.Add(new OnboardingStatusIssueDto(
                            "PIPELINE_BINDING_SCOPE_MISMATCH",
                            "The pipeline binding does not match the pipeline source project scope.",
                            "ProductSourceBinding",
                            binding.SourceExternalId));
                    }
                    else if (!validProjectBindingScopes.Contains((binding.ProductRootId, binding.ProjectSourceId)))
                    {
                        blockers.Add(new OnboardingStatusIssueDto(
                            "PIPELINE_BINDING_PROJECT_BINDING_REQUIRED",
                            "A pipeline binding requires an enabled valid project binding in the same project scope.",
                            "ProductSourceBinding",
                            binding.SourceExternalId));
                    }
                    else
                    {
                        blockers.Add(new OnboardingStatusIssueDto(
                            "PIPELINE_BINDING_INVALID",
                            "The pipeline binding is enabled but not valid.",
                            "ProductSourceBinding",
                            binding.SourceExternalId));
                    }
                    break;
            }
        }
    }

    private static void AddSnapshotWarnings(
        List<OnboardingStatusIssueDto> warnings,
        IEnumerable<(string EntityType, string EntityExternalId, OnboardingSnapshotMetadata Metadata)> snapshots)
    {
        foreach (var (entityType, entityExternalId, metadata) in snapshots)
        {
            if (metadata.RenameDetected)
            {
                warnings.Add(new OnboardingStatusIssueDto(
                    "SNAPSHOT_RENAME_DETECTED",
                    "The latest snapshot indicates the external entity was renamed.",
                    entityType,
                    entityExternalId));
            }

            if (!metadata.IsCurrent || !string.IsNullOrWhiteSpace(metadata.StaleReason))
            {
                warnings.Add(new OnboardingStatusIssueDto(
                    "SNAPSHOT_STALE",
                    "The latest snapshot is stale and should be refreshed.",
                    entityType,
                    entityExternalId));
            }
        }
    }

    private static void AddConnectionValidationBlocker(
        List<OnboardingStatusIssueDto> blockers,
        OnboardingValidationState state,
        string unavailableCode,
        string unavailableMessage,
        string invalidCode,
        string invalidMessage)
    {
        if (IsValidationState(state, OnboardingValidationStatus.Valid))
        {
            return;
        }

        if (IsValidationState(state, OnboardingValidationStatus.Unavailable))
        {
            blockers.Add(new OnboardingStatusIssueDto(unavailableCode, unavailableMessage, "TfsConnection", null));
            return;
        }

        if (IsValidationState(state, OnboardingValidationStatus.PermissionDenied))
        {
            blockers.Add(new OnboardingStatusIssueDto(
                unavailableCode.Contains("CAPABILITY", StringComparison.Ordinal) ? unavailableCode : invalidCode == "CONNECTION_INVALID" ? "CONNECTION_PERMISSION_DENIED" : invalidCode,
                unavailableCode.Contains("CAPABILITY", StringComparison.Ordinal) ? unavailableMessage : "The TFS connection is missing required permissions.",
                "TfsConnection",
                null));
            return;
        }

        if (IsValidationState(state, OnboardingValidationStatus.CapabilityDenied))
        {
            blockers.Add(new OnboardingStatusIssueDto(
                "CONNECTION_CAPABILITY_DENIED",
                "The TFS connection is missing required capabilities.",
                "TfsConnection",
                null));
            return;
        }

        blockers.Add(new OnboardingStatusIssueDto(invalidCode, invalidMessage, "TfsConnection", null));
    }

    private static bool IsValidProjectBinding(
        ProductSourceBinding binding,
        IReadOnlyDictionary<int, ProductRoot> rootsById,
        IReadOnlyDictionary<int, ProjectSource> projectsById,
        IReadOnlySet<int> validRootIds,
        IReadOnlySet<int> validProjectIds)
    {
        if (!binding.Enabled || !IsValidationStateValid(binding.ValidationState) || binding.SourceType != ProductSourceType.Project)
        {
            return false;
        }

        return rootsById.TryGetValue(binding.ProductRootId, out var root)
            && projectsById.TryGetValue(binding.ProjectSourceId, out var project)
            && validRootIds.Contains(root.Id)
            && validProjectIds.Contains(project.Id)
            && root.ProjectSourceId == binding.ProjectSourceId
            && binding.SourceExternalId.Equals(project.ProjectExternalId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidTeamBinding(
        ProductSourceBinding binding,
        IReadOnlyDictionary<int, ProductRoot> rootsById,
        IReadOnlyDictionary<int, TeamSource> teamsById,
        IReadOnlySet<int> validRootIds,
        IReadOnlySet<int> validProjectIds,
        IReadOnlySet<int> validTeamIds,
        IReadOnlySet<(int ProductRootId, int ProjectSourceId)> validProjectBindingScopes)
    {
        if (!binding.Enabled
            || !IsValidationStateValid(binding.ValidationState)
            || binding.SourceType != ProductSourceType.Team
            || binding.TeamSourceId is null)
        {
            return false;
        }

        return rootsById.TryGetValue(binding.ProductRootId, out var root)
            && teamsById.TryGetValue(binding.TeamSourceId.Value, out var team)
            && validRootIds.Contains(root.Id)
            && validProjectIds.Contains(binding.ProjectSourceId)
            && validTeamIds.Contains(team.Id)
            && root.ProjectSourceId == binding.ProjectSourceId
            && team.ProjectSourceId == binding.ProjectSourceId
            && binding.SourceExternalId.Equals(team.TeamExternalId, StringComparison.OrdinalIgnoreCase)
            && validProjectBindingScopes.Contains((binding.ProductRootId, binding.ProjectSourceId));
    }

    private static bool IsValidPipelineBinding(
        ProductSourceBinding binding,
        IReadOnlyDictionary<int, ProductRoot> rootsById,
        IReadOnlyDictionary<int, PipelineSource> pipelinesById,
        IReadOnlySet<int> validRootIds,
        IReadOnlySet<int> validProjectIds,
        IReadOnlySet<int> validPipelineIds,
        IReadOnlySet<(int ProductRootId, int ProjectSourceId)> validProjectBindingScopes)
    {
        if (!binding.Enabled
            || !IsValidationStateValid(binding.ValidationState)
            || binding.SourceType != ProductSourceType.Pipeline
            || binding.PipelineSourceId is null)
        {
            return false;
        }

        return rootsById.TryGetValue(binding.ProductRootId, out var root)
            && pipelinesById.TryGetValue(binding.PipelineSourceId.Value, out var pipeline)
            && validRootIds.Contains(root.Id)
            && validProjectIds.Contains(binding.ProjectSourceId)
            && validPipelineIds.Contains(pipeline.Id)
            && root.ProjectSourceId == binding.ProjectSourceId
            && pipeline.ProjectSourceId == binding.ProjectSourceId
            && binding.SourceExternalId.Equals(pipeline.PipelineExternalId, StringComparison.OrdinalIgnoreCase)
            && validProjectBindingScopes.Contains((binding.ProductRootId, binding.ProjectSourceId));
    }

    private static bool IsConnectionValid(TfsConnection? connection)
        => connection is not null
           && IsValidationState(connection.AvailabilityValidationState, OnboardingValidationStatus.Valid)
           && IsValidationState(connection.PermissionValidationState, OnboardingValidationStatus.Valid)
           && IsValidationState(connection.CapabilityValidationState, OnboardingValidationStatus.Valid);

    private static bool IsValidationStateValid(OnboardingValidationState validationState)
        => IsValidationState(validationState, OnboardingValidationStatus.Valid);

    private static bool IsValidationState(OnboardingValidationState validationState, OnboardingValidationStatus expectedStatus)
        => string.Equals(validationState.Status, expectedStatus.ToString(), StringComparison.OrdinalIgnoreCase);
}
