using PoTool.Api.Services.Onboarding;
using PoTool.Shared.Onboarding;

namespace PoTool.Api.Handlers.Onboarding;

public interface IOnboardingCrudHandler
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

public sealed class OnboardingCrudHandler : IOnboardingCrudHandler
{
    private readonly IOnboardingCrudService _crudService;

    public OnboardingCrudHandler(IOnboardingCrudService crudService)
    {
        _crudService = crudService;
    }

    public Task<OnboardingOperationResult<IReadOnlyList<OnboardingTfsConnectionDto>>> ListConnectionsAsync(OnboardingConfigurationStatus? status, CancellationToken cancellationToken) => _crudService.ListConnectionsAsync(status, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingTfsConnectionDto>> GetConnectionAsync(int id, CancellationToken cancellationToken) => _crudService.GetConnectionAsync(id, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingTfsConnectionDto>> CreateConnectionAsync(CreateTfsConnectionRequest request, CancellationToken cancellationToken) => _crudService.CreateConnectionAsync(request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingTfsConnectionDto>> UpdateConnectionAsync(int id, UpdateTfsConnectionRequest request, CancellationToken cancellationToken) => _crudService.UpdateConnectionAsync(id, request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteConnectionAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken) => _crudService.DeleteConnectionAsync(id, request, cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<OnboardingProjectSourceDto>>> ListProjectsAsync(int? connectionId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken) => _crudService.ListProjectsAsync(connectionId, status, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingProjectSourceDto>> GetProjectAsync(int id, CancellationToken cancellationToken) => _crudService.GetProjectAsync(id, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingProjectSourceDto>> CreateProjectAsync(CreateProjectSourceRequest request, CancellationToken cancellationToken) => _crudService.CreateProjectAsync(request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingProjectSourceDto>> UpdateProjectAsync(int id, UpdateProjectSourceRequest request, CancellationToken cancellationToken) => _crudService.UpdateProjectAsync(id, request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteProjectAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken) => _crudService.DeleteProjectAsync(id, request, cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<OnboardingTeamSourceDto>>> ListTeamsAsync(int? connectionId, int? projectId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken) => _crudService.ListTeamsAsync(connectionId, projectId, status, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingTeamSourceDto>> GetTeamAsync(int id, CancellationToken cancellationToken) => _crudService.GetTeamAsync(id, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingTeamSourceDto>> CreateTeamAsync(CreateTeamSourceRequest request, CancellationToken cancellationToken) => _crudService.CreateTeamAsync(request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingTeamSourceDto>> UpdateTeamAsync(int id, UpdateTeamSourceRequest request, CancellationToken cancellationToken) => _crudService.UpdateTeamAsync(id, request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteTeamAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken) => _crudService.DeleteTeamAsync(id, request, cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<OnboardingPipelineSourceDto>>> ListPipelinesAsync(int? connectionId, int? projectId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken) => _crudService.ListPipelinesAsync(connectionId, projectId, status, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingPipelineSourceDto>> GetPipelineAsync(int id, CancellationToken cancellationToken) => _crudService.GetPipelineAsync(id, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingPipelineSourceDto>> CreatePipelineAsync(CreatePipelineSourceRequest request, CancellationToken cancellationToken) => _crudService.CreatePipelineAsync(request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingPipelineSourceDto>> UpdatePipelineAsync(int id, UpdatePipelineSourceRequest request, CancellationToken cancellationToken) => _crudService.UpdatePipelineAsync(id, request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeletePipelineAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken) => _crudService.DeletePipelineAsync(id, request, cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<OnboardingProductRootDto>>> ListRootsAsync(int? connectionId, int? projectId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken) => _crudService.ListRootsAsync(connectionId, projectId, status, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingProductRootDto>> GetRootAsync(int id, CancellationToken cancellationToken) => _crudService.GetRootAsync(id, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingProductRootDto>> CreateRootAsync(CreateProductRootRequest request, CancellationToken cancellationToken) => _crudService.CreateRootAsync(request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingProductRootDto>> UpdateRootAsync(int id, UpdateProductRootRequest request, CancellationToken cancellationToken) => _crudService.UpdateRootAsync(id, request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteRootAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken) => _crudService.DeleteRootAsync(id, request, cancellationToken);

    public Task<OnboardingOperationResult<IReadOnlyList<OnboardingProductSourceBindingDto>>> ListBindingsAsync(int? connectionId, int? projectId, int? productRootId, OnboardingConfigurationStatus? status, CancellationToken cancellationToken) => _crudService.ListBindingsAsync(connectionId, projectId, productRootId, status, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingProductSourceBindingDto>> GetBindingAsync(int id, CancellationToken cancellationToken) => _crudService.GetBindingAsync(id, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingProductSourceBindingDto>> CreateBindingAsync(CreateProductSourceBindingRequest request, CancellationToken cancellationToken) => _crudService.CreateBindingAsync(request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingProductSourceBindingDto>> UpdateBindingAsync(int id, UpdateProductSourceBindingRequest request, CancellationToken cancellationToken) => _crudService.UpdateBindingAsync(id, request, cancellationToken);
    public Task<OnboardingOperationResult<OnboardingSoftDeleteResultDto>> DeleteBindingAsync(int id, OnboardingSoftDeleteRequest request, CancellationToken cancellationToken) => _crudService.DeleteBindingAsync(id, request, cancellationToken);
}
