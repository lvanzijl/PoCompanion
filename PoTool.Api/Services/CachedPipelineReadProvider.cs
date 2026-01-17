using PoTool.Core.Contracts;
using PoTool.Core.Pipelines;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Services;

/// <summary>
/// Cached pipeline read provider that delegates to the existing repository.
/// Used when DataSourceMode is Cached.
/// </summary>
public sealed class CachedPipelineReadProvider : IPipelineReadProvider
{
    private readonly IPipelineRepository _repository;

    public CachedPipelineReadProvider(IPipelineRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<PipelineDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task<PipelineDto?> GetByIdAsync(int pipelineId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(pipelineId, cancellationToken);
    }

    public Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default)
    {
        return _repository.GetRunsAsync(pipelineId, top, cancellationToken);
    }

    public Task<IEnumerable<PipelineRunDto>> GetAllRunsAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllRunsAsync(cancellationToken);
    }

    public Task<IEnumerable<PipelineDefinitionDto>> GetAllDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllDefinitionsAsync(cancellationToken);
    }

    public Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByProductIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        return _repository.GetDefinitionsByProductIdAsync(productId, cancellationToken);
    }

    public Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default)
    {
        return _repository.GetDefinitionsByRepositoryIdAsync(repositoryId, cancellationToken);
    }
}
