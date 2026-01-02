using PoTool.Api.Services.MockData;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;

namespace PoTool.Api.Repositories;

/// <summary>
/// Development-only in-memory repository that returns Battleship mock data.
/// Used to develop UI without a TFS connection.
/// </summary>
public class DevWorkItemRepository : IWorkItemRepository
{
    private readonly BattleshipMockDataFacade _mockDataFacade;
    private List<WorkItemDto> _items;

    public DevWorkItemRepository(BattleshipMockDataFacade mockDataFacade)
    {
        _mockDataFacade = mockDataFacade;
        _items = _mockDataFacade.GetMockHierarchy();
    }

    public Task<IEnumerable<WorkItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Always get fresh data from mock facade to ensure latest generated data
        _items = _mockDataFacade.GetMockHierarchy();
        return Task.FromResult(_items.AsEnumerable());
    }

    public Task<IEnumerable<WorkItemDto>> GetFilteredAsync(string filter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return Task.FromResult(_items.AsEnumerable());

        var f = filter.Trim();
        var result = _items.Where(w => w.Title.Contains(f, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(result);
    }

    public Task<IEnumerable<WorkItemDto>> GetByAreaPathsAsync(List<string> areaPaths, CancellationToken cancellationToken = default)
    {
        if (areaPaths == null || areaPaths.Count == 0)
            return Task.FromResult(_items.AsEnumerable());

        // Filter using hierarchical area path matching
        var filtered = _items.Where(item => 
            areaPaths.Any(profilePath => 
                item.AreaPath.Equals(profilePath, StringComparison.OrdinalIgnoreCase) ||
                item.AreaPath.StartsWith(profilePath + "\\", StringComparison.OrdinalIgnoreCase)));

        return Task.FromResult(filtered);
    }

    public Task<WorkItemDto?> GetByTfsIdAsync(int tfsId, CancellationToken cancellationToken = default)
    {
        var item = _items.FirstOrDefault(w => w.TfsId == tfsId);
        return Task.FromResult(item);
    }

    public Task ReplaceAllAsync(IEnumerable<WorkItemDto> workItems, CancellationToken cancellationToken = default)
    {
        // For dev repository, just replace the in-memory list
        // Note: This shouldn't typically be used in dev mode since we use generated mock data
        _items.Clear();
        _items.AddRange(workItems);
        return Task.CompletedTask;
    }
}
