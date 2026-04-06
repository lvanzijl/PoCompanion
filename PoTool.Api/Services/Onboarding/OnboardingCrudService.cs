using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Services.Onboarding;

public interface IOnboardingCrudService
{
    Task<OnboardingOperationResult<IReadOnlyList<OnboardingTfsConnectionDto>>> ListConnectionsAsync(OnboardingConfigurationStatus? status, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingTfsConnectionDto>> GetConnectionAsync(int id, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingTfsConnectionDto>> CreateConnectionAsync(CreateTfsConnectionRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingTfsConnectionDto>> UpdateConnectionAsync(int id, UpdateTfsConnectionRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteConnectionAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken);

    Task<OnboardingOperationResult<IReadOnlyList<OnboardingProjectSourceDto>>> ListProjectsAsync(int? connectionId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingProjectSourceDto>> GetProjectAsync(int id, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingProjectSourceDto>> CreateProjectAsync(CreateProjectSourceRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingProjectSourceDto>> UpdateProjectAsync(int id, UpdateProjectSourceRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteProjectAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken);

    Task<OnboardingOperationResult<IReadOnlyList<OnboardingTeamSourceDto>>> ListTeamsAsync(int? connectionId, int? projectId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingTeamSourceDto>> GetTeamAsync(int id, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingTeamSourceDto>> CreateTeamAsync(CreateTeamSourceRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingTeamSourceDto>> UpdateTeamAsync(int id, UpdateTeamSourceRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteTeamAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken);

    Task<OnboardingOperationResult<IReadOnlyList<OnboardingPipelineSourceDto>>> ListPipelinesAsync(int? connectionId, int? projectId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingPipelineSourceDto>> GetPipelineAsync(int id, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingPipelineSourceDto>> CreatePipelineAsync(CreatePipelineSourceRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingPipelineSourceDto>> UpdatePipelineAsync(int id, UpdatePipelineSourceRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeletePipelineAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken);

    Task<OnboardingOperationResult<IReadOnlyList<OnboardingProductRootDto>>> ListRootsAsync(int? connectionId, int? projectId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingProductRootDto>> GetRootAsync(int id, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingProductRootDto>> CreateRootAsync(CreateProductRootRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingProductRootDto>> UpdateRootAsync(int id, UpdateProductRootRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteRootAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken);

    Task<OnboardingOperationResult<IReadOnlyList<OnboardingProductSourceBindingDto>>> ListBindingsAsync(int? connectionId, int? projectId, int? productRootId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingProductSourceBindingDto>> GetBindingAsync(int id, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingProductSourceBindingDto>> CreateBindingAsync(CreateProductSourceBindingRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingProductSourceBindingDto>> UpdateBindingAsync(int id, UpdateProductSourceBindingRequest request, CancellationToken cancellationToken);
    Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteBindingAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken);
}

public sealed class OnboardingCrudService : IOnboardingCrudService
{
    private readonly PoToolDbContext _dbContext;
    private readonly IOnboardingValidationService _validationService;
    private readonly IOnboardingStatusService _statusService;

    public OnboardingCrudService(
        PoToolDbContext dbContext,
        IOnboardingValidationService validationService,
        IOnboardingStatusService statusService)
    {
        _dbContext = dbContext;
        _validationService = validationService;
        _statusService = statusService;
    }

    public async Task<OnboardingOperationResult<IReadOnlyList<OnboardingTfsConnectionDto>>> ListConnectionsAsync(OnboardingConfigurationStatus? status, CancellationToken cancellationToken)
    {
        var connections = await _dbContext.OnboardingTfsConnections.AsNoTracking().Where(item => !item.IsDeleted).OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var onboardingStatus = await GetStatusAsync(cancellationToken);
        return OnboardingOperationResult<IReadOnlyList<OnboardingTfsConnectionDto>>.Success(connections.Select(connection => MapConnection(connection, onboardingStatus)).Where(item => status is null || item.Status.Status == status.Value).ToList());
    }

    public async Task<OnboardingOperationResult<OnboardingTfsConnectionDto>> GetConnectionAsync(int id, CancellationToken cancellationToken)
    {
        var connection = await FindConnectionAsync(id, cancellationToken);
        return connection is null
            ? Failure<OnboardingTfsConnectionDto>(OnboardingErrorCode.NotFound, "Onboarding connection was not found.", $"connectionId={id}")
            : OnboardingOperationResult<OnboardingTfsConnectionDto>.Success(MapConnection(connection, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingTfsConnectionDto>> CreateConnectionAsync(CreateTfsConnectionRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateCreateConnectionRequest(request);
        if (validationError is not null)
        {
            return Failure<OnboardingTfsConnectionDto>(validationError.Value.Code, validationError.Value.Message, validationError.Value.Details);
        }

        if (await _dbContext.OnboardingTfsConnections.AnyAsync(item => !item.IsDeleted, cancellationToken))
        {
            return Failure<OnboardingTfsConnectionDto>(OnboardingErrorCode.Conflict, "Only one active onboarding connection is supported.", null);
        }

        var connection = new TfsConnection
        {
            ConnectionKey = "connection",
            OrganizationUrl = request.OrganizationUrl.Trim(),
            AuthenticationMode = request.AuthenticationMode.Trim(),
            TimeoutSeconds = request.TimeoutSeconds,
            ApiVersion = request.ApiVersion.Trim()
        };

        var validation = await _validationService.ValidateConnectionAsync(connection, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingTfsConnectionDto, TfsConnectionValidationResultDto>(validation);
        }

        ApplyConnectionValidation(connection, validation.Data!);
        _dbContext.OnboardingTfsConnections.Add(connection);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingTfsConnectionDto>.Success(MapConnection(connection, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingTfsConnectionDto>> UpdateConnectionAsync(int id, UpdateTfsConnectionRequest request, CancellationToken cancellationToken)
    {
        var forbiddenError = ValidateConnectionIdentityMutation(request);
        if (forbiddenError is not null)
        {
            return Failure<OnboardingTfsConnectionDto>(forbiddenError.Value.Code, forbiddenError.Value.Message, forbiddenError.Value.Details);
        }

        var connection = await FindConnectionForUpdateAsync(id, cancellationToken);
        if (connection is null)
        {
            return Failure<OnboardingTfsConnectionDto>(OnboardingErrorCode.NotFound, "Onboarding connection was not found.", $"connectionId={id}");
        }

        if (request.AuthenticationMode is not null)
        {
            if (string.IsNullOrWhiteSpace(request.AuthenticationMode))
            {
                return Failure<OnboardingTfsConnectionDto>(OnboardingErrorCode.ValidationFailed, "Authentication mode is required.", nameof(request.AuthenticationMode));
            }

            connection.AuthenticationMode = request.AuthenticationMode.Trim();
        }

        if (request.TimeoutSeconds.HasValue)
        {
            if (request.TimeoutSeconds.Value <= 0)
            {
                return Failure<OnboardingTfsConnectionDto>(OnboardingErrorCode.ValidationFailed, "TimeoutSeconds must be greater than zero.", nameof(request.TimeoutSeconds));
            }

            connection.TimeoutSeconds = request.TimeoutSeconds.Value;
        }

        if (request.ApiVersion is not null)
        {
            if (string.IsNullOrWhiteSpace(request.ApiVersion))
            {
                return Failure<OnboardingTfsConnectionDto>(OnboardingErrorCode.ValidationFailed, "ApiVersion is required.", nameof(request.ApiVersion));
            }

            connection.ApiVersion = request.ApiVersion.Trim();
        }

        var validation = await _validationService.ValidateConnectionAsync(connection, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingTfsConnectionDto, TfsConnectionValidationResultDto>(validation);
        }

        ApplyConnectionValidation(connection, validation.Data!);
        connection.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingTfsConnectionDto>.Success(MapConnection(connection, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteConnectionAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken)
    {
        var reasonError = ValidateDeleteRequest(request);
        if (reasonError is not null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(reasonError.Value.Code, reasonError.Value.Message, reasonError.Value.Details);
        }

        var connection = await FindConnectionForUpdateAsync(id, cancellationToken);
        if (connection is null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.NotFound, "Onboarding connection was not found.", $"connectionId={id}");
        }

        if (await _dbContext.OnboardingProjectSources.AnyAsync(item => !item.IsDeleted && item.TfsConnectionId == id, cancellationToken))
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.DependencyViolation, "Cannot delete a connection that still has active project sources.", $"connectionId={id}");
        }

        connection.SoftDelete(DateTime.UtcNow, request.Reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingSoftDeleteResultDto>.Success(new OnboardingSoftDeleteResultDto(connection.Id, connection.DeletedAtUtc!.Value, connection.DeletionReason!));
    }

    public async Task<OnboardingOperationResult<IReadOnlyList<OnboardingProjectSourceDto>>> ListProjectsAsync(int? connectionId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.OnboardingProjectSources.AsNoTracking().Where(item => !item.IsDeleted);
        if (connectionId.HasValue)
        {
            query = query.Where(item => item.TfsConnectionId == connectionId.Value);
        }

        var projects = await query.OrderBy(item => item.ProjectExternalId).ToListAsync(cancellationToken);
        var onboardingStatus = await GetStatusAsync(cancellationToken);
        return OnboardingOperationResult<IReadOnlyList<OnboardingProjectSourceDto>>.Success(projects.Select(project => MapProject(project, onboardingStatus)).Where(item => status is null || item.Status.Status == status.Value).ToList());
    }

    public async Task<OnboardingOperationResult<OnboardingProjectSourceDto>> GetProjectAsync(int id, CancellationToken cancellationToken)
    {
        var project = await FindProjectAsync(id, cancellationToken);
        return project is null
            ? Failure<OnboardingProjectSourceDto>(OnboardingErrorCode.NotFound, "Onboarding project source was not found.", $"projectId={id}")
            : OnboardingOperationResult<OnboardingProjectSourceDto>.Success(MapProject(project, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingProjectSourceDto>> CreateProjectAsync(CreateProjectSourceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectExternalId))
        {
            return Failure<OnboardingProjectSourceDto>(OnboardingErrorCode.ValidationFailed, "ProjectExternalId is required.", nameof(request.ProjectExternalId));
        }

        var connection = await FindConnectionForUpdateAsync(request.TfsConnectionId, cancellationToken);
        if (connection is null)
        {
            return Failure<OnboardingProjectSourceDto>(OnboardingErrorCode.DependencyViolation, "Project source requires an active connection.", $"connectionId={request.TfsConnectionId}");
        }

        var projectExternalId = request.ProjectExternalId.Trim();
        if (await _dbContext.OnboardingProjectSources.AnyAsync(item => item.TfsConnectionId == request.TfsConnectionId && item.ProjectExternalId == projectExternalId, cancellationToken))
        {
            return Failure<OnboardingProjectSourceDto>(OnboardingErrorCode.Conflict, "A project source with the same external ID already exists.", projectExternalId);
        }

        var project = new ProjectSource
        {
            TfsConnectionId = request.TfsConnectionId,
            ProjectExternalId = projectExternalId,
            Enabled = request.Enabled
        };

        var validation = await _validationService.ValidateProjectSourceAsync(connection, project, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingProjectSourceDto, ProjectSourceValidationResultDto>(validation);
        }

        ApplyProjectValidation(project, validation.Data!, request.Name, request.Description);
        _dbContext.OnboardingProjectSources.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingProjectSourceDto>.Success(MapProject(project, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingProjectSourceDto>> UpdateProjectAsync(int id, UpdateProjectSourceRequest request, CancellationToken cancellationToken)
    {
        var forbiddenError = ValidateProjectIdentityMutation(request);
        if (forbiddenError is not null)
        {
            return Failure<OnboardingProjectSourceDto>(forbiddenError.Value.Code, forbiddenError.Value.Message, forbiddenError.Value.Details);
        }

        var project = await FindProjectForUpdateAsync(id, cancellationToken);
        if (project is null)
        {
            return Failure<OnboardingProjectSourceDto>(OnboardingErrorCode.NotFound, "Onboarding project source was not found.", $"projectId={id}");
        }

        if (request.Enabled.HasValue)
        {
            project.Enabled = request.Enabled.Value;
        }

        var connection = await FindConnectionAsync(project.TfsConnectionId, cancellationToken);
        if (connection is null)
        {
            return Failure<OnboardingProjectSourceDto>(OnboardingErrorCode.DependencyViolation, "Project source requires an active connection.", $"connectionId={project.TfsConnectionId}");
        }

        var validation = await _validationService.ValidateProjectSourceAsync(connection, project, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingProjectSourceDto, ProjectSourceValidationResultDto>(validation);
        }

        ApplyProjectValidation(project, validation.Data!, request.Name, request.Description);
        project.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingProjectSourceDto>.Success(MapProject(project, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteProjectAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken)
    {
        var reasonError = ValidateDeleteRequest(request);
        if (reasonError is not null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(reasonError.Value.Code, reasonError.Value.Message, reasonError.Value.Details);
        }

        var project = await FindProjectForUpdateAsync(id, cancellationToken);
        if (project is null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.NotFound, "Onboarding project source was not found.", $"projectId={id}");
        }

        var hasDependencies = await _dbContext.OnboardingTeamSources.AnyAsync(item => !item.IsDeleted && item.ProjectSourceId == id, cancellationToken)
            || await _dbContext.OnboardingPipelineSources.AnyAsync(item => !item.IsDeleted && item.ProjectSourceId == id, cancellationToken)
            || await _dbContext.OnboardingProductRoots.AnyAsync(item => !item.IsDeleted && item.ProjectSourceId == id, cancellationToken)
            || await _dbContext.OnboardingProductSourceBindings.AnyAsync(item => !item.IsDeleted && item.ProjectSourceId == id, cancellationToken);
        if (hasDependencies)
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.DependencyViolation, "Cannot delete a project source that still participates in the onboarding graph.", $"projectId={id}");
        }

        project.SoftDelete(DateTime.UtcNow, request.Reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingSoftDeleteResultDto>.Success(new OnboardingSoftDeleteResultDto(project.Id, project.DeletedAtUtc!.Value, project.DeletionReason!));
    }

    public async Task<OnboardingOperationResult<IReadOnlyList<OnboardingTeamSourceDto>>> ListTeamsAsync(int? connectionId, int? projectId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.OnboardingTeamSources.AsNoTracking().Where(item => !item.IsDeleted).Join(_dbContext.OnboardingProjectSources.AsNoTracking().Where(project => !project.IsDeleted), team => team.ProjectSourceId, project => project.Id, (team, project) => new { team, project });
        if (projectId.HasValue)
        {
            query = query.Where(item => item.team.ProjectSourceId == projectId.Value);
        }

        if (connectionId.HasValue)
        {
            query = query.Where(item => item.project.TfsConnectionId == connectionId.Value);
        }

        var teams = await query.OrderBy(item => item.team.TeamExternalId).Select(item => item.team).ToListAsync(cancellationToken);
        var onboardingStatus = await GetStatusAsync(cancellationToken);
        return OnboardingOperationResult<IReadOnlyList<OnboardingTeamSourceDto>>.Success(teams.Select(team => MapTeam(team, onboardingStatus)).Where(item => status is null || item.Status.Status == status.Value).ToList());
    }

    public async Task<OnboardingOperationResult<OnboardingTeamSourceDto>> GetTeamAsync(int id, CancellationToken cancellationToken)
    {
        var team = await FindTeamAsync(id, cancellationToken);
        return team is null
            ? Failure<OnboardingTeamSourceDto>(OnboardingErrorCode.NotFound, "Onboarding team source was not found.", $"teamId={id}")
            : OnboardingOperationResult<OnboardingTeamSourceDto>.Success(MapTeam(team, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingTeamSourceDto>> CreateTeamAsync(CreateTeamSourceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TeamExternalId))
        {
            return Failure<OnboardingTeamSourceDto>(OnboardingErrorCode.ValidationFailed, "TeamExternalId is required.", nameof(request.TeamExternalId));
        }

        var project = await FindProjectAsync(request.ProjectSourceId, cancellationToken);
        if (project is null)
        {
            return Failure<OnboardingTeamSourceDto>(OnboardingErrorCode.DependencyViolation, "Team source requires an active project source.", $"projectId={request.ProjectSourceId}");
        }

        var teamExternalId = request.TeamExternalId.Trim();
        if (await _dbContext.OnboardingTeamSources.AnyAsync(item => item.ProjectSourceId == request.ProjectSourceId && item.TeamExternalId == teamExternalId, cancellationToken))
        {
            return Failure<OnboardingTeamSourceDto>(OnboardingErrorCode.Conflict, "A team source with the same external ID already exists.", teamExternalId);
        }

        var connection = await FindConnectionAsync(project.TfsConnectionId, cancellationToken);
        if (connection is null)
        {
            return Failure<OnboardingTeamSourceDto>(OnboardingErrorCode.DependencyViolation, "Team source requires an active connection.", $"connectionId={project.TfsConnectionId}");
        }

        var team = new TeamSource { ProjectSourceId = request.ProjectSourceId, TeamExternalId = teamExternalId, Enabled = request.Enabled };
        var validation = await _validationService.ValidateTeamSourceAsync(connection, project, team, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingTeamSourceDto, TeamSourceValidationResultDto>(validation);
        }

        ApplyTeamValidation(team, validation.Data!, request.Name, request.DefaultAreaPath, request.Description);
        _dbContext.OnboardingTeamSources.Add(team);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingTeamSourceDto>.Success(MapTeam(team, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingTeamSourceDto>> UpdateTeamAsync(int id, UpdateTeamSourceRequest request, CancellationToken cancellationToken)
    {
        var forbiddenError = ValidateTeamIdentityMutation(request);
        if (forbiddenError is not null)
        {
            return Failure<OnboardingTeamSourceDto>(forbiddenError.Value.Code, forbiddenError.Value.Message, forbiddenError.Value.Details);
        }

        var team = await FindTeamForUpdateAsync(id, cancellationToken);
        if (team is null)
        {
            return Failure<OnboardingTeamSourceDto>(OnboardingErrorCode.NotFound, "Onboarding team source was not found.", $"teamId={id}");
        }

        if (request.Enabled.HasValue)
        {
            team.Enabled = request.Enabled.Value;
        }

        var project = await FindProjectAsync(team.ProjectSourceId, cancellationToken);
        if (project is null)
        {
            return Failure<OnboardingTeamSourceDto>(OnboardingErrorCode.DependencyViolation, "Team source requires an active project source.", $"projectId={team.ProjectSourceId}");
        }

        var connection = await FindConnectionAsync(project.TfsConnectionId, cancellationToken);
        if (connection is null)
        {
            return Failure<OnboardingTeamSourceDto>(OnboardingErrorCode.DependencyViolation, "Team source requires an active connection.", $"connectionId={project.TfsConnectionId}");
        }

        var validation = await _validationService.ValidateTeamSourceAsync(connection, project, team, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingTeamSourceDto, TeamSourceValidationResultDto>(validation);
        }

        ApplyTeamValidation(team, validation.Data!, request.Name, request.DefaultAreaPath, request.Description);
        team.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingTeamSourceDto>.Success(MapTeam(team, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteTeamAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken)
    {
        var reasonError = ValidateDeleteRequest(request);
        if (reasonError is not null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(reasonError.Value.Code, reasonError.Value.Message, reasonError.Value.Details);
        }

        var team = await FindTeamForUpdateAsync(id, cancellationToken);
        if (team is null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.NotFound, "Onboarding team source was not found.", $"teamId={id}");
        }

        if (await _dbContext.OnboardingProductSourceBindings.AnyAsync(item => !item.IsDeleted && item.TeamSourceId == id, cancellationToken))
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.DependencyViolation, "Cannot delete a team source that is used in a binding.", $"teamId={id}");
        }

        team.SoftDelete(DateTime.UtcNow, request.Reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingSoftDeleteResultDto>.Success(new OnboardingSoftDeleteResultDto(team.Id, team.DeletedAtUtc!.Value, team.DeletionReason!));
    }

    public async Task<OnboardingOperationResult<IReadOnlyList<OnboardingPipelineSourceDto>>> ListPipelinesAsync(int? connectionId, int? projectId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.OnboardingPipelineSources.AsNoTracking().Where(item => !item.IsDeleted).Join(_dbContext.OnboardingProjectSources.AsNoTracking().Where(project => !project.IsDeleted), pipeline => pipeline.ProjectSourceId, project => project.Id, (pipeline, project) => new { pipeline, project });
        if (projectId.HasValue)
        {
            query = query.Where(item => item.pipeline.ProjectSourceId == projectId.Value);
        }

        if (connectionId.HasValue)
        {
            query = query.Where(item => item.project.TfsConnectionId == connectionId.Value);
        }

        var pipelines = await query.OrderBy(item => item.pipeline.PipelineExternalId).Select(item => item.pipeline).ToListAsync(cancellationToken);
        var onboardingStatus = await GetStatusAsync(cancellationToken);
        return OnboardingOperationResult<IReadOnlyList<OnboardingPipelineSourceDto>>.Success(pipelines.Select(pipeline => MapPipeline(pipeline, onboardingStatus)).Where(item => status is null || item.Status.Status == status.Value).ToList());
    }

    public async Task<OnboardingOperationResult<OnboardingPipelineSourceDto>> GetPipelineAsync(int id, CancellationToken cancellationToken)
    {
        var pipeline = await FindPipelineAsync(id, cancellationToken);
        return pipeline is null
            ? Failure<OnboardingPipelineSourceDto>(OnboardingErrorCode.NotFound, "Onboarding pipeline source was not found.", $"pipelineId={id}")
            : OnboardingOperationResult<OnboardingPipelineSourceDto>.Success(MapPipeline(pipeline, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingPipelineSourceDto>> CreatePipelineAsync(CreatePipelineSourceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PipelineExternalId))
        {
            return Failure<OnboardingPipelineSourceDto>(OnboardingErrorCode.ValidationFailed, "PipelineExternalId is required.", nameof(request.PipelineExternalId));
        }

        var project = await FindProjectAsync(request.ProjectSourceId, cancellationToken);
        if (project is null)
        {
            return Failure<OnboardingPipelineSourceDto>(OnboardingErrorCode.DependencyViolation, "Pipeline source requires an active project source.", $"projectId={request.ProjectSourceId}");
        }

        var pipelineExternalId = request.PipelineExternalId.Trim();
        if (await _dbContext.OnboardingPipelineSources.AnyAsync(item => item.ProjectSourceId == request.ProjectSourceId && item.PipelineExternalId == pipelineExternalId, cancellationToken))
        {
            return Failure<OnboardingPipelineSourceDto>(OnboardingErrorCode.Conflict, "A pipeline source with the same external ID already exists.", pipelineExternalId);
        }

        var connection = await FindConnectionAsync(project.TfsConnectionId, cancellationToken);
        if (connection is null)
        {
            return Failure<OnboardingPipelineSourceDto>(OnboardingErrorCode.DependencyViolation, "Pipeline source requires an active connection.", $"connectionId={project.TfsConnectionId}");
        }

        var pipeline = new PipelineSource { ProjectSourceId = request.ProjectSourceId, PipelineExternalId = pipelineExternalId, Enabled = request.Enabled };
        var validation = await _validationService.ValidatePipelineSourceAsync(connection, project, pipeline, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingPipelineSourceDto, PipelineSourceValidationResultDto>(validation);
        }

        ApplyPipelineValidation(pipeline, validation.Data!, request.Name, request.Folder, request.YamlPath, request.RepositoryExternalId, request.RepositoryName);
        _dbContext.OnboardingPipelineSources.Add(pipeline);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingPipelineSourceDto>.Success(MapPipeline(pipeline, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingPipelineSourceDto>> UpdatePipelineAsync(int id, UpdatePipelineSourceRequest request, CancellationToken cancellationToken)
    {
        var forbiddenError = ValidatePipelineIdentityMutation(request);
        if (forbiddenError is not null)
        {
            return Failure<OnboardingPipelineSourceDto>(forbiddenError.Value.Code, forbiddenError.Value.Message, forbiddenError.Value.Details);
        }

        var pipeline = await FindPipelineForUpdateAsync(id, cancellationToken);
        if (pipeline is null)
        {
            return Failure<OnboardingPipelineSourceDto>(OnboardingErrorCode.NotFound, "Onboarding pipeline source was not found.", $"pipelineId={id}");
        }

        if (request.Enabled.HasValue)
        {
            pipeline.Enabled = request.Enabled.Value;
        }

        var project = await FindProjectAsync(pipeline.ProjectSourceId, cancellationToken);
        if (project is null)
        {
            return Failure<OnboardingPipelineSourceDto>(OnboardingErrorCode.DependencyViolation, "Pipeline source requires an active project source.", $"projectId={pipeline.ProjectSourceId}");
        }

        var connection = await FindConnectionAsync(project.TfsConnectionId, cancellationToken);
        if (connection is null)
        {
            return Failure<OnboardingPipelineSourceDto>(OnboardingErrorCode.DependencyViolation, "Pipeline source requires an active connection.", $"connectionId={project.TfsConnectionId}");
        }

        var validation = await _validationService.ValidatePipelineSourceAsync(connection, project, pipeline, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingPipelineSourceDto, PipelineSourceValidationResultDto>(validation);
        }

        ApplyPipelineValidation(pipeline, validation.Data!, request.Name, request.Folder, request.YamlPath, request.RepositoryExternalId, request.RepositoryName);
        pipeline.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingPipelineSourceDto>.Success(MapPipeline(pipeline, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeletePipelineAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken)
    {
        var reasonError = ValidateDeleteRequest(request);
        if (reasonError is not null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(reasonError.Value.Code, reasonError.Value.Message, reasonError.Value.Details);
        }

        var pipeline = await FindPipelineForUpdateAsync(id, cancellationToken);
        if (pipeline is null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.NotFound, "Onboarding pipeline source was not found.", $"pipelineId={id}");
        }

        if (await _dbContext.OnboardingProductSourceBindings.AnyAsync(item => !item.IsDeleted && item.PipelineSourceId == id, cancellationToken))
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.DependencyViolation, "Cannot delete a pipeline source that is used in a binding.", $"pipelineId={id}");
        }

        pipeline.SoftDelete(DateTime.UtcNow, request.Reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingSoftDeleteResultDto>.Success(new OnboardingSoftDeleteResultDto(pipeline.Id, pipeline.DeletedAtUtc!.Value, pipeline.DeletionReason!));
    }

    public async Task<OnboardingOperationResult<IReadOnlyList<OnboardingProductRootDto>>> ListRootsAsync(int? connectionId, int? projectId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.OnboardingProductRoots.AsNoTracking().Where(item => !item.IsDeleted).Join(_dbContext.OnboardingProjectSources.AsNoTracking().Where(project => !project.IsDeleted), root => root.ProjectSourceId, project => project.Id, (root, project) => new { root, project });
        if (projectId.HasValue)
        {
            query = query.Where(item => item.root.ProjectSourceId == projectId.Value);
        }

        if (connectionId.HasValue)
        {
            query = query.Where(item => item.project.TfsConnectionId == connectionId.Value);
        }

        var roots = await query.OrderBy(item => item.root.WorkItemExternalId).Select(item => item.root).ToListAsync(cancellationToken);
        var onboardingStatus = await GetStatusAsync(cancellationToken);
        return OnboardingOperationResult<IReadOnlyList<OnboardingProductRootDto>>.Success(roots.Select(root => MapRoot(root, onboardingStatus)).Where(item => status is null || item.Status.Status == status.Value).ToList());
    }

    public async Task<OnboardingOperationResult<OnboardingProductRootDto>> GetRootAsync(int id, CancellationToken cancellationToken)
    {
        var root = await FindRootAsync(id, cancellationToken);
        return root is null
            ? Failure<OnboardingProductRootDto>(OnboardingErrorCode.NotFound, "Onboarding product root was not found.", $"rootId={id}")
            : OnboardingOperationResult<OnboardingProductRootDto>.Success(MapRoot(root, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingProductRootDto>> CreateRootAsync(CreateProductRootRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkItemExternalId))
        {
            return Failure<OnboardingProductRootDto>(OnboardingErrorCode.ValidationFailed, "WorkItemExternalId is required.", nameof(request.WorkItemExternalId));
        }

        var project = await FindProjectAsync(request.ProjectSourceId, cancellationToken);
        if (project is null)
        {
            return Failure<OnboardingProductRootDto>(OnboardingErrorCode.DependencyViolation, "Product root requires an active project source.", $"projectId={request.ProjectSourceId}");
        }

        var workItemExternalId = request.WorkItemExternalId.Trim();
        if (await _dbContext.OnboardingProductRoots.AnyAsync(item => item.ProjectSourceId == request.ProjectSourceId && item.WorkItemExternalId == workItemExternalId, cancellationToken))
        {
            return Failure<OnboardingProductRootDto>(OnboardingErrorCode.Conflict, "A product root with the same external ID already exists.", workItemExternalId);
        }

        var connection = await FindConnectionAsync(project.TfsConnectionId, cancellationToken);
        if (connection is null)
        {
            return Failure<OnboardingProductRootDto>(OnboardingErrorCode.DependencyViolation, "Product root requires an active connection.", $"connectionId={project.TfsConnectionId}");
        }

        var root = new ProductRoot { ProjectSourceId = request.ProjectSourceId, WorkItemExternalId = workItemExternalId, Enabled = request.Enabled };
        var validation = await _validationService.ValidateProductRootAsync(connection, project, root, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingProductRootDto, ProductRootValidationResultDto>(validation);
        }

        ApplyRootValidation(root, validation.Data!, request.Title, request.WorkItemType, request.State, request.AreaPath);
        _dbContext.OnboardingProductRoots.Add(root);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingProductRootDto>.Success(MapRoot(root, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingProductRootDto>> UpdateRootAsync(int id, UpdateProductRootRequest request, CancellationToken cancellationToken)
    {
        var forbiddenError = ValidateRootIdentityMutation(request);
        if (forbiddenError is not null)
        {
            return Failure<OnboardingProductRootDto>(forbiddenError.Value.Code, forbiddenError.Value.Message, forbiddenError.Value.Details);
        }

        var root = await FindRootForUpdateAsync(id, cancellationToken);
        if (root is null)
        {
            return Failure<OnboardingProductRootDto>(OnboardingErrorCode.NotFound, "Onboarding product root was not found.", $"rootId={id}");
        }

        if (request.Enabled.HasValue)
        {
            root.Enabled = request.Enabled.Value;
        }

        var project = await FindProjectAsync(root.ProjectSourceId, cancellationToken);
        if (project is null)
        {
            return Failure<OnboardingProductRootDto>(OnboardingErrorCode.DependencyViolation, "Product root requires an active project source.", $"projectId={root.ProjectSourceId}");
        }

        var connection = await FindConnectionAsync(project.TfsConnectionId, cancellationToken);
        if (connection is null)
        {
            return Failure<OnboardingProductRootDto>(OnboardingErrorCode.DependencyViolation, "Product root requires an active connection.", $"connectionId={project.TfsConnectionId}");
        }

        var validation = await _validationService.ValidateProductRootAsync(connection, project, root, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingProductRootDto, ProductRootValidationResultDto>(validation);
        }

        ApplyRootValidation(root, validation.Data!, request.Title, request.WorkItemType, request.State, request.AreaPath);
        root.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingProductRootDto>.Success(MapRoot(root, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteRootAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken)
    {
        var reasonError = ValidateDeleteRequest(request);
        if (reasonError is not null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(reasonError.Value.Code, reasonError.Value.Message, reasonError.Value.Details);
        }

        var root = await FindRootForUpdateAsync(id, cancellationToken);
        if (root is null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.NotFound, "Onboarding product root was not found.", $"rootId={id}");
        }

        if (await _dbContext.OnboardingProductSourceBindings.AnyAsync(item => !item.IsDeleted && item.ProductRootId == id, cancellationToken))
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.DependencyViolation, "Cannot delete a product root that is used in bindings.", $"rootId={id}");
        }

        root.SoftDelete(DateTime.UtcNow, request.Reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingSoftDeleteResultDto>.Success(new OnboardingSoftDeleteResultDto(root.Id, root.DeletedAtUtc!.Value, root.DeletionReason!));
    }

    public async Task<OnboardingOperationResult<IReadOnlyList<OnboardingProductSourceBindingDto>>> ListBindingsAsync(int? connectionId, int? projectId, int? productRootId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.OnboardingProductSourceBindings.AsNoTracking().Where(item => !item.IsDeleted).Join(_dbContext.OnboardingProjectSources.AsNoTracking().Where(project => !project.IsDeleted), binding => binding.ProjectSourceId, project => project.Id, (binding, project) => new { binding, project });
        if (productRootId.HasValue)
        {
            query = query.Where(item => item.binding.ProductRootId == productRootId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(item => item.binding.ProjectSourceId == projectId.Value);
        }

        if (connectionId.HasValue)
        {
            query = query.Where(item => item.project.TfsConnectionId == connectionId.Value);
        }

        var bindings = await query.OrderBy(item => item.binding.ProductRootId).ThenBy(item => item.binding.SourceType).ThenBy(item => item.binding.SourceExternalId).Select(item => item.binding).ToListAsync(cancellationToken);
        var onboardingStatus = await GetStatusAsync(cancellationToken);
        return OnboardingOperationResult<IReadOnlyList<OnboardingProductSourceBindingDto>>.Success(bindings.Select(binding => MapBinding(binding, onboardingStatus)).Where(item => status is null || item.Status.Status == status.Value).ToList());
    }

    public async Task<OnboardingOperationResult<OnboardingProductSourceBindingDto>> GetBindingAsync(int id, CancellationToken cancellationToken)
    {
        var binding = await FindBindingAsync(id, cancellationToken);
        return binding is null
            ? Failure<OnboardingProductSourceBindingDto>(OnboardingErrorCode.NotFound, "Onboarding product source binding was not found.", $"bindingId={id}")
            : OnboardingOperationResult<OnboardingProductSourceBindingDto>.Success(MapBinding(binding, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingProductSourceBindingDto>> CreateBindingAsync(CreateProductSourceBindingRequest request, CancellationToken cancellationToken)
    {
        var bindingSetup = await ResolveBindingCreationAsync(request, cancellationToken);
        if (!bindingSetup.Succeeded)
        {
            return FailureFromResult<OnboardingProductSourceBindingDto, BindingResolution>(bindingSetup);
        }

        var setup = bindingSetup.Data!;
        if (await _dbContext.OnboardingProductSourceBindings.AnyAsync(item => item.ProductRootId == setup.Root.Id && item.SourceType == setup.SourceType && item.SourceExternalId == setup.SourceExternalId, cancellationToken))
        {
            return Failure<OnboardingProductSourceBindingDto>(OnboardingErrorCode.Conflict, "A product source binding with the same source already exists.", setup.SourceExternalId);
        }

        var binding = new ProductSourceBinding
        {
            ProductRootId = setup.Root.Id,
            ProjectSourceId = setup.Project.Id,
            TeamSourceId = setup.Team?.Id,
            PipelineSourceId = setup.Pipeline?.Id,
            SourceType = setup.SourceType,
            SourceExternalId = setup.SourceExternalId,
            Enabled = request.Enabled
        };

        var validation = await _validationService.ValidateProductSourceBindingAsync(setup.Connection, setup.Project, setup.Root, binding, setup.Team, setup.Pipeline, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingProductSourceBindingDto, ProductSourceBindingValidationResultDto>(validation);
        }

        ApplyBindingValidation(binding, validation.Data!);
        _dbContext.OnboardingProductSourceBindings.Add(binding);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingProductSourceBindingDto>.Success(MapBinding(binding, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingProductSourceBindingDto>> UpdateBindingAsync(int id, UpdateProductSourceBindingRequest request, CancellationToken cancellationToken)
    {
        var forbiddenError = ValidateBindingIdentityMutation(request);
        if (forbiddenError is not null)
        {
            return Failure<OnboardingProductSourceBindingDto>(forbiddenError.Value.Code, forbiddenError.Value.Message, forbiddenError.Value.Details);
        }

        var binding = await FindBindingForUpdateAsync(id, cancellationToken);
        if (binding is null)
        {
            return Failure<OnboardingProductSourceBindingDto>(OnboardingErrorCode.NotFound, "Onboarding product source binding was not found.", $"bindingId={id}");
        }

        if (request.Enabled.HasValue)
        {
            binding.Enabled = request.Enabled.Value;
        }

        var root = await FindRootAsync(binding.ProductRootId, cancellationToken);
        var project = await FindProjectAsync(binding.ProjectSourceId, cancellationToken);
        var connection = project is null ? null : await FindConnectionAsync(project.TfsConnectionId, cancellationToken);
        var team = binding.TeamSourceId.HasValue ? await FindTeamAsync(binding.TeamSourceId.Value, cancellationToken) : null;
        var pipeline = binding.PipelineSourceId.HasValue ? await FindPipelineAsync(binding.PipelineSourceId.Value, cancellationToken) : null;

        if (root is null || project is null || connection is null)
        {
            return Failure<OnboardingProductSourceBindingDto>(OnboardingErrorCode.DependencyViolation, "Binding requires an active root, project, and connection.", $"bindingId={id}");
        }

        var validation = await _validationService.ValidateProductSourceBindingAsync(connection, project, root, binding, team, pipeline, cancellationToken);
        if (!validation.Succeeded)
        {
            return FailureFromResult<OnboardingProductSourceBindingDto, ProductSourceBindingValidationResultDto>(validation);
        }

        ApplyBindingValidation(binding, validation.Data!);
        binding.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingProductSourceBindingDto>.Success(MapBinding(binding, await GetStatusAsync(cancellationToken)));
    }

    public async Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteBindingAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken)
    {
        var reasonError = ValidateDeleteRequest(request);
        if (reasonError is not null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(reasonError.Value.Code, reasonError.Value.Message, reasonError.Value.Details);
        }

        var binding = await FindBindingForUpdateAsync(id, cancellationToken);
        if (binding is null)
        {
            return Failure<OnboardingSoftDeleteResultDto>(OnboardingErrorCode.NotFound, "Onboarding product source binding was not found.", $"bindingId={id}");
        }

        binding.SoftDelete(DateTime.UtcNow, request.Reason);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OnboardingOperationResult<OnboardingSoftDeleteResultDto>.Success(new OnboardingSoftDeleteResultDto(binding.Id, binding.DeletedAtUtc!.Value, binding.DeletionReason!));
    }

    private async Task<OnboardingOperationResult<BindingResolution>> ResolveBindingCreationAsync(CreateProductSourceBindingRequest request, CancellationToken cancellationToken)
    {
        var root = await FindRootAsync(request.ProductRootId, cancellationToken);
        if (root is null)
        {
            return Failure<BindingResolution>(OnboardingErrorCode.DependencyViolation, "Binding requires an active product root.", $"productRootId={request.ProductRootId}");
        }

        var project = await FindProjectAsync(root.ProjectSourceId, cancellationToken);
        if (project is null)
        {
            return Failure<BindingResolution>(OnboardingErrorCode.DependencyViolation, "Binding requires an active project source.", $"projectId={root.ProjectSourceId}");
        }

        if (request.ProjectSourceId.HasValue && request.ProjectSourceId.Value != project.Id)
        {
            return Failure<BindingResolution>(OnboardingErrorCode.ValidationFailed, "Binding project source cannot be changed.", $"expectedProjectId={project.Id}; requestedProjectId={request.ProjectSourceId.Value}");
        }

        var connection = await FindConnectionAsync(project.TfsConnectionId, cancellationToken);
        if (connection is null)
        {
            return Failure<BindingResolution>(OnboardingErrorCode.DependencyViolation, "Binding requires an active connection.", $"connectionId={project.TfsConnectionId}");
        }

        return request.SourceType switch
        {
            OnboardingProductSourceTypeDto.Project => OnboardingOperationResult<BindingResolution>.Success(new BindingResolution(root, project, connection, ProductSourceType.Project, project.ProjectExternalId, null, null)),
            OnboardingProductSourceTypeDto.Team => await ResolveTeamBindingAsync(root, project, connection, request.TeamSourceId, cancellationToken),
            OnboardingProductSourceTypeDto.Pipeline => await ResolvePipelineBindingAsync(root, project, connection, request.PipelineSourceId, cancellationToken),
            _ => Failure<BindingResolution>(OnboardingErrorCode.ValidationFailed, "Unsupported binding source type.", request.SourceType.ToString())
        };
    }

    private async Task<OnboardingOperationResult<BindingResolution>> ResolveTeamBindingAsync(ProductRoot root, ProjectSource project, TfsConnection connection, int? teamSourceId, CancellationToken cancellationToken)
    {
        if (!teamSourceId.HasValue)
        {
            return Failure<BindingResolution>(OnboardingErrorCode.ValidationFailed, "Team bindings require TeamSourceId.", nameof(teamSourceId));
        }

        var team = await FindTeamAsync(teamSourceId.Value, cancellationToken);
        if (team is null)
        {
            return Failure<BindingResolution>(OnboardingErrorCode.DependencyViolation, "Binding requires an active team source.", $"teamId={teamSourceId.Value}");
        }

        if (team.ProjectSourceId != project.Id)
        {
            return Failure<BindingResolution>(OnboardingErrorCode.ValidationFailed, "Team binding project scope does not match the product root project.", $"teamProjectId={team.ProjectSourceId}; rootProjectId={project.Id}");
        }

        return OnboardingOperationResult<BindingResolution>.Success(new BindingResolution(root, project, connection, ProductSourceType.Team, team.TeamExternalId, team, null));
    }

    private async Task<OnboardingOperationResult<BindingResolution>> ResolvePipelineBindingAsync(ProductRoot root, ProjectSource project, TfsConnection connection, int? pipelineSourceId, CancellationToken cancellationToken)
    {
        if (!pipelineSourceId.HasValue)
        {
            return Failure<BindingResolution>(OnboardingErrorCode.ValidationFailed, "Pipeline bindings require PipelineSourceId.", nameof(pipelineSourceId));
        }

        var pipeline = await FindPipelineAsync(pipelineSourceId.Value, cancellationToken);
        if (pipeline is null)
        {
            return Failure<BindingResolution>(OnboardingErrorCode.DependencyViolation, "Binding requires an active pipeline source.", $"pipelineId={pipelineSourceId.Value}");
        }

        if (pipeline.ProjectSourceId != project.Id)
        {
            return Failure<BindingResolution>(OnboardingErrorCode.ValidationFailed, "Pipeline binding project scope does not match the product root project.", $"pipelineProjectId={pipeline.ProjectSourceId}; rootProjectId={project.Id}");
        }

        return OnboardingOperationResult<BindingResolution>.Success(new BindingResolution(root, project, connection, ProductSourceType.Pipeline, pipeline.PipelineExternalId, null, pipeline));
    }

    private static (OnboardingErrorCode Code, string Message, string? Details)? ValidateCreateConnectionRequest(CreateTfsConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationUrl))
        {
            return (OnboardingErrorCode.ValidationFailed, "OrganizationUrl is required.", nameof(request.OrganizationUrl));
        }

        if (string.IsNullOrWhiteSpace(request.AuthenticationMode))
        {
            return (OnboardingErrorCode.ValidationFailed, "AuthenticationMode is required.", nameof(request.AuthenticationMode));
        }

        if (request.TimeoutSeconds <= 0)
        {
            return (OnboardingErrorCode.ValidationFailed, "TimeoutSeconds must be greater than zero.", nameof(request.TimeoutSeconds));
        }

        if (string.IsNullOrWhiteSpace(request.ApiVersion))
        {
            return (OnboardingErrorCode.ValidationFailed, "ApiVersion is required.", nameof(request.ApiVersion));
        }

        return null;
    }

    private static (OnboardingErrorCode Code, string Message, string? Details)? ValidateDeleteRequest(OnboardingSoftDeleteRequest request)
        => string.IsNullOrWhiteSpace(request.Reason)
            ? (OnboardingErrorCode.ValidationFailed, "Deletion reason is required.", nameof(request.Reason))
            : null;

    private static (OnboardingErrorCode Code, string Message, string? Details)? ValidateConnectionIdentityMutation(UpdateTfsConnectionRequest request)
        => request.ConnectionKey is not null || request.OrganizationUrl is not null ? (OnboardingErrorCode.ValidationFailed, "Connection identity fields cannot be updated.", "ConnectionKey, OrganizationUrl") : null;

    private static (OnboardingErrorCode Code, string Message, string? Details)? ValidateProjectIdentityMutation(UpdateProjectSourceRequest request)
        => request.ProjectExternalId is not null || request.TfsConnectionId.HasValue ? (OnboardingErrorCode.ValidationFailed, "Project identity fields cannot be updated.", "ProjectExternalId, TfsConnectionId") : null;

    private static (OnboardingErrorCode Code, string Message, string? Details)? ValidateTeamIdentityMutation(UpdateTeamSourceRequest request)
        => request.TeamExternalId is not null || request.ProjectSourceId.HasValue ? (OnboardingErrorCode.ValidationFailed, "Team identity fields cannot be updated.", "TeamExternalId, ProjectSourceId") : null;

    private static (OnboardingErrorCode Code, string Message, string? Details)? ValidatePipelineIdentityMutation(UpdatePipelineSourceRequest request)
        => request.PipelineExternalId is not null || request.ProjectSourceId.HasValue ? (OnboardingErrorCode.ValidationFailed, "Pipeline identity fields cannot be updated.", "PipelineExternalId, ProjectSourceId") : null;

    private static (OnboardingErrorCode Code, string Message, string? Details)? ValidateRootIdentityMutation(UpdateProductRootRequest request)
        => request.WorkItemExternalId is not null || request.ProjectSourceId.HasValue ? (OnboardingErrorCode.ValidationFailed, "Product root identity fields cannot be updated.", "WorkItemExternalId, ProjectSourceId") : null;

    private static (OnboardingErrorCode Code, string Message, string? Details)? ValidateBindingIdentityMutation(UpdateProductSourceBindingRequest request)
        => request.ProductRootId.HasValue || request.ProjectSourceId.HasValue || request.TeamSourceId.HasValue || request.PipelineSourceId.HasValue || request.SourceType.HasValue || request.SourceExternalId is not null ? (OnboardingErrorCode.ValidationFailed, "Binding identity fields cannot be updated.", "ProductRootId, ProjectSourceId, TeamSourceId, PipelineSourceId, SourceType, SourceExternalId") : null;

    private Task<TfsConnection?> FindConnectionAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingTfsConnections.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<TfsConnection?> FindConnectionForUpdateAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingTfsConnections.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<ProjectSource?> FindProjectAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingProjectSources.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<ProjectSource?> FindProjectForUpdateAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingProjectSources.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<TeamSource?> FindTeamAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingTeamSources.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<TeamSource?> FindTeamForUpdateAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingTeamSources.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<PipelineSource?> FindPipelineAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingPipelineSources.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<PipelineSource?> FindPipelineForUpdateAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingPipelineSources.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<ProductRoot?> FindRootAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingProductRoots.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<ProductRoot?> FindRootForUpdateAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingProductRoots.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<ProductSourceBinding?> FindBindingAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingProductSourceBindings.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private Task<ProductSourceBinding?> FindBindingForUpdateAsync(int id, CancellationToken cancellationToken)
        => _dbContext.OnboardingProductSourceBindings.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);

    private async Task<OnboardingStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var result = await _statusService.GetStatusAsync(cancellationToken);
        return result.Succeeded && result.Data is not null
            ? result.Data
            : throw new InvalidOperationException("Onboarding status service returned an unexpected failure.");
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

    private static void ApplyProjectValidation(ProjectSource entity, ProjectSourceValidationResultDto validation, string? nameOverride, string? descriptionOverride)
    {
        entity.Snapshot = new ProjectSnapshot
        {
            ProjectExternalId = validation.Snapshot.ProjectExternalId,
            Name = NormalizeOverride(nameOverride, validation.Snapshot.Name),
            Description = NormalizeOptionalOverride(descriptionOverride, validation.Snapshot.Description),
            Metadata = MapMetadata(validation.Snapshot.Metadata)
        };
        entity.ValidationState = MapValidationState(validation.ValidationState);
    }

    private static void ApplyTeamValidation(TeamSource entity, TeamSourceValidationResultDto validation, string? nameOverride, string? defaultAreaPathOverride, string? descriptionOverride)
    {
        entity.Snapshot = new TeamSnapshot
        {
            TeamExternalId = validation.Snapshot.TeamExternalId,
            ProjectExternalId = validation.Snapshot.ProjectExternalId,
            Name = NormalizeOverride(nameOverride, validation.Snapshot.Name),
            DefaultAreaPath = NormalizeOverride(defaultAreaPathOverride, validation.Snapshot.DefaultAreaPath),
            Description = NormalizeOptionalOverride(descriptionOverride, validation.Snapshot.Description),
            Metadata = MapMetadata(validation.Snapshot.Metadata)
        };
        entity.ValidationState = MapValidationState(validation.ValidationState);
    }

    private static void ApplyPipelineValidation(PipelineSource entity, PipelineSourceValidationResultDto validation, string? nameOverride, string? folderOverride, string? yamlPathOverride, string? repositoryExternalIdOverride, string? repositoryNameOverride)
    {
        entity.Snapshot = new PipelineSnapshot
        {
            PipelineExternalId = validation.Snapshot.PipelineExternalId,
            ProjectExternalId = validation.Snapshot.ProjectExternalId,
            Name = NormalizeOverride(nameOverride, validation.Snapshot.Name),
            Folder = NormalizeOptionalOverride(folderOverride, validation.Snapshot.Folder),
            YamlPath = NormalizeOptionalOverride(yamlPathOverride, validation.Snapshot.YamlPath),
            RepositoryExternalId = NormalizeOptionalOverride(repositoryExternalIdOverride, validation.Snapshot.RepositoryExternalId),
            RepositoryName = NormalizeOptionalOverride(repositoryNameOverride, validation.Snapshot.RepositoryName),
            Metadata = MapMetadata(validation.Snapshot.Metadata)
        };
        entity.ValidationState = MapValidationState(validation.ValidationState);
    }

    private static void ApplyRootValidation(ProductRoot entity, ProductRootValidationResultDto validation, string? titleOverride, string? workItemTypeOverride, string? stateOverride, string? areaPathOverride)
    {
        entity.Snapshot = new ProductRootSnapshot
        {
            WorkItemExternalId = validation.Snapshot.WorkItemExternalId,
            Title = NormalizeOverride(titleOverride, validation.Snapshot.Title),
            WorkItemType = NormalizeOverride(workItemTypeOverride, validation.Snapshot.WorkItemType),
            State = NormalizeOverride(stateOverride, validation.Snapshot.State),
            ProjectExternalId = validation.Snapshot.ProjectExternalId,
            AreaPath = NormalizeOverride(areaPathOverride, validation.Snapshot.AreaPath),
            Metadata = MapMetadata(validation.Snapshot.Metadata)
        };
        entity.ValidationState = MapValidationState(validation.ValidationState);
    }

    private static void ApplyBindingValidation(ProductSourceBinding entity, ProductSourceBindingValidationResultDto validation)
        => entity.ValidationState = MapValidationState(validation.ValidationState);

    private static OnboardingValidationState MapValidationState(OnboardingValidationStateDto validationState)
        => new() { Status = validationState.Status.ToString(), ValidatedAtUtc = validationState.CheckedAtUtc, ErrorCode = validationState.ErrorCode, Message = validationState.ErrorMessageSanitized, IsRetryable = false };

    private static OnboardingSnapshotMetadata MapMetadata(SnapshotMetadataDto metadata)
        => new() { ConfirmedAtUtc = metadata.ConfirmedAtUtc, LastSeenAtUtc = metadata.LastSeenAtUtc, IsCurrent = metadata.IsCurrent, RenameDetected = metadata.RenameDetected ?? false, StaleReason = metadata.StaleReason };

    private static string NormalizeOverride(string? overrideValue, string fallback)
        => string.IsNullOrWhiteSpace(overrideValue) ? fallback : overrideValue.Trim();

    private static string? NormalizeOptionalOverride(string? overrideValue, string? fallback)
        => overrideValue is null ? fallback : string.IsNullOrWhiteSpace(overrideValue) ? null : overrideValue.Trim();

    private static OnboardingTfsConnectionDto MapConnection(TfsConnection entity, OnboardingStatusDto status)
        => new(entity.Id, entity.ConnectionKey, entity.OrganizationUrl, entity.AuthenticationMode, entity.TimeoutSeconds, entity.ApiVersion, new TfsConnectionValidationResultDto(entity.OrganizationUrl, entity.AuthenticationMode, entity.TimeoutSeconds, entity.ApiVersion, MapValidationStateDto(entity.AvailabilityValidationState), MapValidationStateDto(entity.PermissionValidationState), MapValidationStateDto(entity.CapabilityValidationState), entity.LastSuccessfulValidationAtUtc, entity.LastAttemptedValidationAtUtc, entity.ValidationFailureReason, entity.LastVerifiedCapabilitiesSummary), BuildEntityStatus(status, nameof(TfsConnection), null), new OnboardingAuditDto(entity.CreatedAtUtc, entity.UpdatedAtUtc, entity.DeletedAtUtc, entity.DeletionReason));

    private static OnboardingProjectSourceDto MapProject(ProjectSource entity, OnboardingStatusDto status)
        => new(entity.Id, entity.TfsConnectionId, entity.ProjectExternalId, entity.Enabled, new ProjectSnapshotDto(entity.Snapshot.ProjectExternalId, entity.Snapshot.Name, entity.Snapshot.Description, MapSnapshotMetadataDto(entity.Snapshot.Metadata)), MapValidationStateDto(entity.ValidationState), BuildEntityStatus(status, nameof(ProjectSource), entity.ProjectExternalId), new OnboardingAuditDto(entity.CreatedAtUtc, entity.UpdatedAtUtc, entity.DeletedAtUtc, entity.DeletionReason));

    private static OnboardingTeamSourceDto MapTeam(TeamSource entity, OnboardingStatusDto status)
        => new(entity.Id, entity.ProjectSourceId, entity.TeamExternalId, entity.Enabled, new TeamSnapshotDto(entity.Snapshot.TeamExternalId, entity.Snapshot.ProjectExternalId, entity.Snapshot.Name, entity.Snapshot.DefaultAreaPath, entity.Snapshot.Description, MapSnapshotMetadataDto(entity.Snapshot.Metadata)), MapValidationStateDto(entity.ValidationState), BuildEntityStatus(status, nameof(TeamSource), entity.TeamExternalId), new OnboardingAuditDto(entity.CreatedAtUtc, entity.UpdatedAtUtc, entity.DeletedAtUtc, entity.DeletionReason));

    private static OnboardingPipelineSourceDto MapPipeline(PipelineSource entity, OnboardingStatusDto status)
        => new(entity.Id, entity.ProjectSourceId, entity.PipelineExternalId, entity.Enabled, new PipelineSnapshotDto(entity.Snapshot.PipelineExternalId, entity.Snapshot.ProjectExternalId, entity.Snapshot.Name, entity.Snapshot.Folder, entity.Snapshot.YamlPath, entity.Snapshot.RepositoryExternalId, entity.Snapshot.RepositoryName, MapSnapshotMetadataDto(entity.Snapshot.Metadata)), MapValidationStateDto(entity.ValidationState), BuildEntityStatus(status, nameof(PipelineSource), entity.PipelineExternalId), new OnboardingAuditDto(entity.CreatedAtUtc, entity.UpdatedAtUtc, entity.DeletedAtUtc, entity.DeletionReason));

    private static OnboardingProductRootDto MapRoot(ProductRoot entity, OnboardingStatusDto status)
        => new(entity.Id, entity.ProjectSourceId, entity.WorkItemExternalId, entity.Enabled, new ProductRootSnapshotDto(entity.Snapshot.WorkItemExternalId, entity.Snapshot.Title, entity.Snapshot.WorkItemType, entity.Snapshot.State, entity.Snapshot.ProjectExternalId, entity.Snapshot.AreaPath, MapSnapshotMetadataDto(entity.Snapshot.Metadata)), MapValidationStateDto(entity.ValidationState), BuildEntityStatus(status, nameof(ProductRoot), entity.WorkItemExternalId), new OnboardingAuditDto(entity.CreatedAtUtc, entity.UpdatedAtUtc, entity.DeletedAtUtc, entity.DeletionReason));

    private static OnboardingProductSourceBindingDto MapBinding(ProductSourceBinding entity, OnboardingStatusDto status)
        => new(entity.Id, entity.ProductRootId, entity.ProjectSourceId, entity.TeamSourceId, entity.PipelineSourceId, MapSourceTypeDto(entity.SourceType), entity.SourceExternalId, entity.Enabled, MapValidationStateDto(entity.ValidationState), BuildEntityStatus(status, nameof(ProductSourceBinding), entity.SourceExternalId), new OnboardingAuditDto(entity.CreatedAtUtc, entity.UpdatedAtUtc, entity.DeletedAtUtc, entity.DeletionReason));

    private static OnboardingEntityStatusDto BuildEntityStatus(OnboardingStatusDto status, string entityType, string? entityExternalId)
    {
        var blockers = status.BlockingReasons.Where(item => string.Equals(item.EntityType, entityType, StringComparison.Ordinal) && string.Equals(item.EntityExternalId, entityExternalId, StringComparison.OrdinalIgnoreCase)).ToList();
        var warnings = status.Warnings.Where(item => string.Equals(item.EntityType, entityType, StringComparison.Ordinal) && string.Equals(item.EntityExternalId, entityExternalId, StringComparison.OrdinalIgnoreCase)).ToList();
        return new OnboardingEntityStatusDto(blockers.Count == 0 ? OnboardingConfigurationStatus.Complete : OnboardingConfigurationStatus.PartiallyConfigured, blockers, warnings);
    }

    private static OnboardingValidationStateDto MapValidationStateDto(OnboardingValidationState validationState)
        => new(ParseValidationStatus(validationState.Status), validationState.ValidatedAtUtc, OnboardingValidationSource.Live, validationState.ErrorCode, validationState.Message, Array.Empty<string>(), null, null, null);

    private static SnapshotMetadataDto MapSnapshotMetadataDto(OnboardingSnapshotMetadata metadata)
        => new(metadata.ConfirmedAtUtc, metadata.LastSeenAtUtc, metadata.IsCurrent, metadata.RenameDetected, metadata.StaleReason);

    private static OnboardingValidationStatus ParseValidationStatus(string value)
        => Enum.TryParse<OnboardingValidationStatus>(value, out var status) ? status : OnboardingValidationStatus.Unknown;

    private static OnboardingProductSourceTypeDto MapSourceTypeDto(ProductSourceType sourceType)
        => sourceType switch { ProductSourceType.Project => OnboardingProductSourceTypeDto.Project, ProductSourceType.Team => OnboardingProductSourceTypeDto.Team, ProductSourceType.Pipeline => OnboardingProductSourceTypeDto.Pipeline, _ => throw new InvalidOperationException($"Unsupported source type '{sourceType}'.") };

    private static OnboardingOperationResult<T> Failure<T>(OnboardingErrorCode code, string message, string? details)
        => OnboardingOperationResult<T>.Failure(new OnboardingErrorDto(code, message, details, false));

    private static OnboardingOperationResult<TTarget> FailureFromResult<TTarget, TSource>(OnboardingOperationResult<TSource> source)
        => OnboardingOperationResult<TTarget>.Failure(source.Error!);

    private sealed record BindingResolution(ProductRoot Root, ProjectSource Project, TfsConnection Connection, ProductSourceType SourceType, string SourceExternalId, TeamSource? Team, PipelineSource? Pipeline);
}
