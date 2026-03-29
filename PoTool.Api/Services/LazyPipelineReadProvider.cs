using PoTool.Core.Contracts;
using PoTool.Core.Pipelines;
using PoTool.Shared.Pipelines;

namespace PoTool.Api.Services;

/// <summary>
/// Lazy wrapper for IPipelineReadProvider that delays provider resolution until method calls.
/// This ensures the DataSourceModeMiddleware has set the correct mode before resolving the provider.
/// </summary>
public sealed class LazyPipelineReadProvider : IPipelineReadProvider
{
    private readonly DataSourceAwareReadProviderFactory _factory;

    public LazyPipelineReadProvider(DataSourceAwareReadProviderFactory factory)
    {
        _factory = factory;
    }

    public Task<IEnumerable<PipelineDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _factory.GetPipelineReadProvider().GetAllAsync(cancellationToken);
    }

    public Task<PipelineDto?> GetByIdAsync(int pipelineId, CancellationToken cancellationToken = default)
    {
        return _factory.GetPipelineReadProvider().GetByIdAsync(pipelineId, cancellationToken);
    }

    public Task<IEnumerable<PipelineDto>> GetByIdsAsync(IEnumerable<int> pipelineIds, CancellationToken cancellationToken = default)
    {
        return _factory.GetPipelineReadProvider().GetByIdsAsync(pipelineIds, cancellationToken);
    }

    public Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default)
    {
        return _factory.GetPipelineReadProvider().GetRunsAsync(pipelineId, top, cancellationToken);
    }

    public Task<IEnumerable<PipelineRunDto>> GetAllRunsAsync(CancellationToken cancellationToken = default)
    {
        return _factory.GetPipelineReadProvider().GetAllRunsAsync(cancellationToken);
    }

    public Task<IEnumerable<PipelineRunDto>> GetRunsForPipelinesAsync(
        IEnumerable<int> pipelineIds,
        string? branchName = null,
        DateTimeOffset? minStartTime = null,
        DateTimeOffset? maxStartTime = null,
        IReadOnlyList<PoTool.Core.Pipelines.Filters.PipelineBranchScope>? branchScope = null,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        return _factory.GetPipelineReadProvider().GetRunsForPipelinesAsync(
            pipelineIds, branchName, minStartTime, maxStartTime, branchScope, top, cancellationToken);
    }

    public Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByProductIdAsync(int productId, CancellationToken cancellationToken = default)
    {
        return _factory.GetPipelineReadProvider().GetDefinitionsByProductIdAsync(productId, cancellationToken);
    }

    public Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default)
    {
        return _factory.GetPipelineReadProvider().GetDefinitionsByRepositoryIdAsync(repositoryId, cancellationToken);
    }
}
