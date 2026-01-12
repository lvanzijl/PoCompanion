using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Api.Repositories;

/// <summary>
/// Development-only in-memory repository that returns Battleship mock data.
/// Used to develop UI without a TFS connection.
/// </summary>
public class DevWorkItemRepository : IWorkItemRepository
{
    private readonly BattleshipMockDataFacade _mockDataFacade;

    public DevWorkItemRepository(BattleshipMockDataFacade mockDataFacade)
    {
        _mockDataFacade = mockDataFacade;
    }

    public Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Get mock data from facade - the facade handles caching internally
        var items = _mockDataFacade.GetMockHierarchy();
        return Task.FromResult(items.AsEnumerable());
    }

    public Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
    {
        var items = _mockDataFacade.GetMockHierarchy();

        if (string.IsNullOrWhiteSpace(filter))
            return Task.FromResult(items.AsEnumerable());

        var f = filter.Trim();
        var result = items.Where(w => w.Title.Contains(f, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(result);
    }

    public Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default)
    {
        var items = _mockDataFacade.GetMockHierarchy();

        if (areaPaths == null || areaPaths.Count == 0)
            return Task.FromResult(items.AsEnumerable());

        // Filter using hierarchical area path matching
        var filtered = items.Where(item =>
            areaPaths.Any(profilePath =>
                item.AreaPath.Equals(profilePath, StringComparison.OrdinalIgnoreCase) ||
                item.AreaPath.StartsWith(profilePath + "\\", StringComparison.OrdinalIgnoreCase)));

        return Task.FromResult(filtered);
    }

    public Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        var items = _mockDataFacade.GetMockHierarchy();
        var item = items.FirstOrDefault(w => w.TfsId == tfsId);
        return Task.FromResult(item);
    }

    public Task ReplaceAllAsync(IEnumerable<WorkItemDto> workItems, CancellationToken cancellationToken = default)
    {
        // For dev repository with mock data, this operation is not meaningful
        // The mock data is always regenerated from the facade
        // In a real scenario, this would persist to a database
        return Task.CompletedTask;
    }

    public Task UpsertManyAsync(IEnumerable<WorkItemDto> workItems, CancellationToken cancellationToken = default)
    {
        // For dev repository with mock data, this operation is not meaningful
        // The mock data is always regenerated from the facade
        return Task.CompletedTask;
    }
}
