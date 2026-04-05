using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingValidationService
{
    Task<OnboardingOperationResult<TfsConnectionValidationResultDto>> ValidateConnectionAsync(TfsConnection connection, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<ProjectSourceValidationResultDto>> ValidateProjectSourceAsync(TfsConnection connection, ProjectSource projectSource, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<TeamSourceValidationResultDto>> ValidateTeamSourceAsync(TfsConnection connection, ProjectSource projectSource, TeamSource teamSource, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<PipelineSourceValidationResultDto>> ValidatePipelineSourceAsync(TfsConnection connection, ProjectSource projectSource, PipelineSource pipelineSource, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<ProductRootValidationResultDto>> ValidateProductRootAsync(TfsConnection connection, ProjectSource projectSource, ProductRoot productRoot, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<ProductSourceBindingValidationResultDto>> ValidateProductSourceBindingAsync(
        TfsConnection connection,
        ProjectSource projectSource,
        ProductRoot productRoot,
        ProductSourceBinding binding,
        TeamSource? teamSource,
        PipelineSource? pipelineSource,
        CancellationToken cancellationToken);
}

public sealed class OnboardingValidationService : IOnboardingValidationService
{
    private readonly IOnboardingLiveLookupClient _liveLookupClient;
    private readonly IOnboardingSnapshotMapper _snapshotMapper;
    private readonly IOnboardingObservability _observability;

    public OnboardingValidationService(
        IOnboardingLiveLookupClient liveLookupClient,
        IOnboardingSnapshotMapper snapshotMapper,
        IOnboardingObservability observability)
    {
        _liveLookupClient = liveLookupClient;
        _snapshotMapper = snapshotMapper;
        _observability = observability;
    }

    public async Task<OnboardingOperationResult<TfsConnectionValidationResultDto>> ValidateConnectionAsync(TfsConnection connection, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var projectsResult = await _liveLookupClient.GetProjectsAsync(connection, null, 1, 0, cancellationToken);
        if (projectsResult.Succeeded)
        {
            return Complete("TfsConnection", OnboardingOperationResult<TfsConnectionValidationResultDto>.Success(new TfsConnectionValidationResultDto(
                connection.OrganizationUrl,
                connection.AuthenticationMode,
                connection.TimeoutSeconds,
                connection.ApiVersion,
                CreateValidationState(OnboardingValidationStatus.Valid, utcNow),
                CreateValidationState(OnboardingValidationStatus.Valid, utcNow),
                CreateValidationState(OnboardingValidationStatus.Valid, utcNow),
                utcNow,
                utcNow,
                null,
                "Project enumeration lookup succeeded.")));
        }

        var error = projectsResult.Error!;
        var availabilityState = error.Code == OnboardingErrorCode.TfsUnavailable
            ? CreateValidationState(OnboardingValidationStatus.Unavailable, utcNow, error)
            : CreateValidationState(OnboardingValidationStatus.Invalid, utcNow, error);
        var permissionState = error.Code == OnboardingErrorCode.PermissionDenied
            ? CreateValidationState(OnboardingValidationStatus.PermissionDenied, utcNow, error)
            : error.Code == OnboardingErrorCode.TfsUnavailable
                ? CreateValidationState(OnboardingValidationStatus.Unknown, utcNow, error)
                : CreateValidationState(OnboardingValidationStatus.Invalid, utcNow, error);
        var capabilityState = error.Code == OnboardingErrorCode.PermissionDenied
            ? CreateValidationState(OnboardingValidationStatus.PermissionDenied, utcNow, error)
            : error.Code == OnboardingErrorCode.TfsUnavailable
                ? CreateValidationState(OnboardingValidationStatus.Unknown, utcNow, error)
                : CreateValidationState(OnboardingValidationStatus.Invalid, utcNow, error);

        return Complete("TfsConnection", OnboardingOperationResult<TfsConnectionValidationResultDto>.Failure(error), new TfsConnectionValidationResultDto(
            connection.OrganizationUrl,
            connection.AuthenticationMode,
            connection.TimeoutSeconds,
            connection.ApiVersion,
            availabilityState,
            permissionState,
            capabilityState,
            null,
            utcNow,
            error.Message,
            null));
    }

    public async Task<OnboardingOperationResult<ProjectSourceValidationResultDto>> ValidateProjectSourceAsync(TfsConnection connection, ProjectSource projectSource, CancellationToken cancellationToken)
    {
        var lookup = await _liveLookupClient.GetProjectsAsync(connection, null, int.MaxValue, 0, cancellationToken);
        if (!lookup.Succeeded)
        {
            return Complete("ProjectSource", OnboardingOperationResult<ProjectSourceValidationResultDto>.Failure(lookup.Error!));
        }

        var project = lookup.Data!.FirstOrDefault(item => item.ProjectExternalId.Equals(projectSource.ProjectExternalId, StringComparison.OrdinalIgnoreCase));
        if (project is null)
        {
            return Complete("ProjectSource", OnboardingOperationResult<ProjectSourceValidationResultDto>.Failure(CreateNotFoundError(
                $"Project '{projectSource.ProjectExternalId}' was not found.",
                projectSource.ProjectExternalId)));
        }

        var utcNow = DateTime.UtcNow;
        return Complete("ProjectSource", OnboardingOperationResult<ProjectSourceValidationResultDto>.Success(new ProjectSourceValidationResultDto(
            project.ProjectExternalId,
            _snapshotMapper.MapProjectSnapshot(project, utcNow),
            CreateValidationState(OnboardingValidationStatus.Valid, utcNow))));
    }

    public async Task<OnboardingOperationResult<TeamSourceValidationResultDto>> ValidateTeamSourceAsync(TfsConnection connection, ProjectSource projectSource, TeamSource teamSource, CancellationToken cancellationToken)
    {
        var lookup = await _liveLookupClient.GetTeamsAsync(connection, projectSource.ProjectExternalId, null, int.MaxValue, 0, cancellationToken);
        if (!lookup.Succeeded)
        {
            return Complete("TeamSource", OnboardingOperationResult<TeamSourceValidationResultDto>.Failure(lookup.Error!));
        }

        var team = lookup.Data!.FirstOrDefault(item => item.TeamExternalId.Equals(teamSource.TeamExternalId, StringComparison.OrdinalIgnoreCase));
        if (team is null)
        {
            return Complete("TeamSource", OnboardingOperationResult<TeamSourceValidationResultDto>.Failure(CreateNotFoundError(
                $"Team '{teamSource.TeamExternalId}' was not found.",
                teamSource.TeamExternalId)));
        }

        if (!team.ProjectExternalId.Equals(projectSource.ProjectExternalId, StringComparison.OrdinalIgnoreCase))
        {
            return Complete("TeamSource", OnboardingOperationResult<TeamSourceValidationResultDto>.Failure(CreateValidationError(
                "Team project scope does not match the selected project source.",
                $"team={team.TeamExternalId}; project={team.ProjectExternalId}; expected={projectSource.ProjectExternalId}")));
        }

        if (string.IsNullOrWhiteSpace(team.DefaultAreaPath))
        {
            return Complete("TeamSource", OnboardingOperationResult<TeamSourceValidationResultDto>.Failure(CreateValidationError(
                "Team is missing the required default area path.",
                team.TeamExternalId)));
        }

        var utcNow = DateTime.UtcNow;
        return Complete("TeamSource", OnboardingOperationResult<TeamSourceValidationResultDto>.Success(new TeamSourceValidationResultDto(
            team.TeamExternalId,
            team.ProjectExternalId,
            _snapshotMapper.MapTeamSnapshot(team, utcNow),
            CreateValidationState(OnboardingValidationStatus.Valid, utcNow))));
    }

    public async Task<OnboardingOperationResult<PipelineSourceValidationResultDto>> ValidatePipelineSourceAsync(TfsConnection connection, ProjectSource projectSource, PipelineSource pipelineSource, CancellationToken cancellationToken)
    {
        var lookup = await _liveLookupClient.GetPipelinesAsync(connection, projectSource.ProjectExternalId, null, int.MaxValue, 0, cancellationToken);
        if (!lookup.Succeeded)
        {
            return Complete("PipelineSource", OnboardingOperationResult<PipelineSourceValidationResultDto>.Failure(lookup.Error!));
        }

        var pipeline = lookup.Data!.FirstOrDefault(item => item.PipelineExternalId.Equals(pipelineSource.PipelineExternalId, StringComparison.OrdinalIgnoreCase));
        if (pipeline is null)
        {
            return Complete("PipelineSource", OnboardingOperationResult<PipelineSourceValidationResultDto>.Failure(CreateNotFoundError(
                $"Pipeline '{pipelineSource.PipelineExternalId}' was not found.",
                pipelineSource.PipelineExternalId)));
        }

        if (!pipeline.ProjectExternalId.Equals(projectSource.ProjectExternalId, StringComparison.OrdinalIgnoreCase))
        {
            return Complete("PipelineSource", OnboardingOperationResult<PipelineSourceValidationResultDto>.Failure(CreateValidationError(
                "Pipeline project scope does not match the selected project source.",
                $"pipeline={pipeline.PipelineExternalId}; project={pipeline.ProjectExternalId}; expected={projectSource.ProjectExternalId}")));
        }

        if (string.IsNullOrWhiteSpace(pipeline.Name))
        {
            return Complete("PipelineSource", OnboardingOperationResult<PipelineSourceValidationResultDto>.Failure(CreateValidationError(
                "Pipeline is missing the required name.",
                pipeline.PipelineExternalId)));
        }

        var utcNow = DateTime.UtcNow;
        return Complete("PipelineSource", OnboardingOperationResult<PipelineSourceValidationResultDto>.Success(new PipelineSourceValidationResultDto(
            pipeline.PipelineExternalId,
            pipeline.ProjectExternalId,
            _snapshotMapper.MapPipelineSnapshot(pipeline, utcNow),
            CreateValidationState(OnboardingValidationStatus.Valid, utcNow))));
    }

    public async Task<OnboardingOperationResult<ProductRootValidationResultDto>> ValidateProductRootAsync(TfsConnection connection, ProjectSource projectSource, ProductRoot productRoot, CancellationToken cancellationToken)
    {
        var lookup = await _liveLookupClient.GetWorkItemAsync(connection, productRoot.WorkItemExternalId, cancellationToken);
        if (!lookup.Succeeded)
        {
            return Complete("ProductRoot", OnboardingOperationResult<ProductRootValidationResultDto>.Failure(lookup.Error!));
        }

        var workItem = lookup.Data!;
        if (!workItem.ProjectExternalId.Equals(projectSource.ProjectExternalId, StringComparison.OrdinalIgnoreCase))
        {
            return Complete("ProductRoot", OnboardingOperationResult<ProductRootValidationResultDto>.Failure(CreateValidationError(
                "Work item project scope does not match the selected project source.",
                $"workItem={workItem.WorkItemExternalId}; project={workItem.ProjectExternalId}; expected={projectSource.ProjectExternalId}")));
        }

        if (string.IsNullOrWhiteSpace(workItem.Title) || string.IsNullOrWhiteSpace(workItem.AreaPath))
        {
            return Complete("ProductRoot", OnboardingOperationResult<ProductRootValidationResultDto>.Failure(CreateValidationError(
                "Work item is missing required snapshot fields.",
                workItem.WorkItemExternalId)));
        }

        var utcNow = DateTime.UtcNow;
        return Complete("ProductRoot", OnboardingOperationResult<ProductRootValidationResultDto>.Success(new ProductRootValidationResultDto(
            workItem.WorkItemExternalId,
            _snapshotMapper.MapProductRootSnapshot(workItem, utcNow),
            CreateValidationState(OnboardingValidationStatus.Valid, utcNow))));
    }

    public async Task<OnboardingOperationResult<ProductSourceBindingValidationResultDto>> ValidateProductSourceBindingAsync(
        TfsConnection connection,
        ProjectSource projectSource,
        ProductRoot productRoot,
        ProductSourceBinding binding,
        TeamSource? teamSource,
        PipelineSource? pipelineSource,
        CancellationToken cancellationToken)
    {
        var rootValidation = await ValidateProductRootAsync(connection, projectSource, productRoot, cancellationToken);
        if (!rootValidation.Succeeded)
        {
            return Complete("ProductSourceBinding", OnboardingOperationResult<ProductSourceBindingValidationResultDto>.Failure(rootValidation.Error!));
        }

        switch (binding.SourceType)
        {
            case ProductSourceType.Project:
                if (!binding.ProjectSourceId.Equals(projectSource.Id) || !binding.SourceExternalId.Equals(projectSource.ProjectExternalId, StringComparison.OrdinalIgnoreCase))
                {
                    return Complete("ProductSourceBinding", OnboardingOperationResult<ProductSourceBindingValidationResultDto>.Failure(CreateValidationError(
                        "Project binding identity does not match the selected project source.",
                        binding.SourceExternalId)));
                }
                break;

            case ProductSourceType.Team:
                if (teamSource is null)
                {
                    return Complete("ProductSourceBinding", OnboardingOperationResult<ProductSourceBindingValidationResultDto>.Failure(CreateValidationError(
                        "Team binding requires a team source reference.",
                        binding.SourceExternalId)));
                }

                var teamValidation = await ValidateTeamSourceAsync(connection, projectSource, teamSource, cancellationToken);
                if (!teamValidation.Succeeded)
                {
                    return Complete("ProductSourceBinding", OnboardingOperationResult<ProductSourceBindingValidationResultDto>.Failure(teamValidation.Error!));
                }

                if (!binding.SourceExternalId.Equals(teamSource.TeamExternalId, StringComparison.OrdinalIgnoreCase) || teamSource.ProjectSourceId != projectSource.Id)
                {
                    return Complete("ProductSourceBinding", OnboardingOperationResult<ProductSourceBindingValidationResultDto>.Failure(CreateValidationError(
                        "Team binding scope does not match the selected team source.",
                        binding.SourceExternalId)));
                }
                break;

            case ProductSourceType.Pipeline:
                if (pipelineSource is null)
                {
                    return Complete("ProductSourceBinding", OnboardingOperationResult<ProductSourceBindingValidationResultDto>.Failure(CreateValidationError(
                        "Pipeline binding requires a pipeline source reference.",
                        binding.SourceExternalId)));
                }

                var pipelineValidation = await ValidatePipelineSourceAsync(connection, projectSource, pipelineSource, cancellationToken);
                if (!pipelineValidation.Succeeded)
                {
                    return Complete("ProductSourceBinding", OnboardingOperationResult<ProductSourceBindingValidationResultDto>.Failure(pipelineValidation.Error!));
                }

                if (!binding.SourceExternalId.Equals(pipelineSource.PipelineExternalId, StringComparison.OrdinalIgnoreCase) || pipelineSource.ProjectSourceId != projectSource.Id)
                {
                    return Complete("ProductSourceBinding", OnboardingOperationResult<ProductSourceBindingValidationResultDto>.Failure(CreateValidationError(
                        "Pipeline binding scope does not match the selected pipeline source.",
                        binding.SourceExternalId)));
                }
                break;

            default:
                return Complete("ProductSourceBinding", OnboardingOperationResult<ProductSourceBindingValidationResultDto>.Failure(CreateValidationError(
                    "Unsupported binding source type.",
                    binding.SourceType.ToString())));
        }

        var utcNow = DateTime.UtcNow;
        return Complete("ProductSourceBinding", OnboardingOperationResult<ProductSourceBindingValidationResultDto>.Success(new ProductSourceBindingValidationResultDto(
            productRoot.WorkItemExternalId,
            binding.SourceType switch
            {
                ProductSourceType.Project => OnboardingProductSourceTypeDto.Project,
                ProductSourceType.Team => OnboardingProductSourceTypeDto.Team,
                ProductSourceType.Pipeline => OnboardingProductSourceTypeDto.Pipeline,
                _ => throw new InvalidOperationException("Unexpected source type.")
            },
            binding.SourceExternalId,
            projectSource.ProjectExternalId,
            CreateValidationState(OnboardingValidationStatus.Valid, utcNow))));
    }

    private OnboardingOperationResult<T> Complete<T>(string entityType, OnboardingOperationResult<T> result, object? _ = null)
    {
        _observability.RecordValidationCompleted(entityType, result.Succeeded, result.Error?.Code);
        _observability.LogValidationCompleted(entityType, result.Succeeded, result.Error?.Code);
        return result;
    }

    private static OnboardingValidationStateDto CreateValidationState(
        OnboardingValidationStatus status,
        DateTime utcNow,
        OnboardingErrorDto? error = null,
        string? notFoundExternalId = null)
        => new(
            status,
            utcNow,
            OnboardingValidationSource.Live,
            error?.Code.ToString(),
            error?.Message,
            Array.Empty<string>(),
            null,
            null,
            notFoundExternalId);

    private static OnboardingErrorDto CreateNotFoundError(string message, string details)
        => new(OnboardingErrorCode.NotFound, message, details, false);

    private static OnboardingErrorDto CreateValidationError(string message, string details)
        => new(OnboardingErrorCode.ValidationFailed, message, details, false);
}
