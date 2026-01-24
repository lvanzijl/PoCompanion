using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Services;

/// <summary>
/// Lazy wrapper for IWorkItemReadProvider that delays provider resolution until method calls.
/// This ensures the DataSourceModeMiddleware has set the correct mode before resolving the provider.
/// </summary>
public sealed class LazyWorkItemReadProvider : IWorkItemReadProvider
{
    private readonly DataSourceAwareReadProviderFactory _factory;

    public LazyWorkItemReadProvider(DataSourceAwareReadProviderFactory factory)
    {
        _factory = factory;
    }

    public Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _factory.GetWorkItemReadProvider().GetAllAsync(cancellationToken);
    }

    public Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
    {
        return _factory.GetWorkItemReadProvider().GetFilteredAsync(filter, cancellationToken);
    }

    public Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default)
    {
        return _factory.GetWorkItemReadProvider().GetByAreaPathsAsync(areaPaths, cancellationToken);
    }

    public Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        return _factory.GetWorkItemReadProvider().GetByTfsIdAsync(tfsId, cancellationToken);
    }

    public Task<IEnumerable<WorkItemDto>> GetByRootIdsAsync(int[] rootWorkItemIds, CancellationToken cancellationToken = default)
    {
        return _factory.GetWorkItemReadProvider().GetByRootIdsAsync(rootWorkItemIds, cancellationToken);
    }
}
