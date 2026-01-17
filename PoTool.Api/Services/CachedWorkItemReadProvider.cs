using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Cached work item read provider that delegates to the existing repository.
/// Used when DataSourceMode is Cached.
/// </summary>
public sealed class CachedWorkItemReadProvider : IWorkItemReadProvider
{
    private readonly IWorkItemRepository _repository;

    public CachedWorkItemReadProvider(IWorkItemRepository repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
    {
        return _repository.GetFilteredAsync(filter, cancellationToken);
    }

    public Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default)
    {
        return _repository.GetByAreaPathsAsync(areaPaths, cancellationToken);
    }

    public Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByTfsIdAsync(tfsId, cancellationToken);
    }
}
