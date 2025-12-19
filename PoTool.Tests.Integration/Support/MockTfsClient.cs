using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Integration.Support;

/// <summary>
/// Mock TFS client for integration testing.
/// Uses file-based test data instead of real TFS API calls.
/// </summary>
public class MockTfsClient : ITfsClient
{
    private readonly List<WorkItemDto> _mockWorkItems = new();

    public MockTfsClient()
    {
        // Initialize with sample test data
        _mockWorkItems = new List<WorkItemDto>
        {
            new WorkItemDto(
                TfsId: 1000,
                Type: "Goal",
                Title: "Test Goal",
                ParentTfsId: null,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null
            ),
            new WorkItemDto(
                TfsId: 1001,
                Type: "Objective",
                Title: "Test Objective",
                ParentTfsId: 1000,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null
            ),
            new WorkItemDto(
                TfsId: 1002,
                Type: "Epic",
                Title: "Test Epic",
                ParentTfsId: 1001,
                AreaPath: "\\TestArea",
                IterationPath: "\\TestIteration",
                State: "New",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null
            )
        };
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        // Always return true for integration tests
        return Task.FromResult(true);
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string areaPath, CancellationToken cancellationToken = default)
    {
        // Return mock work items
        return Task.FromResult<IEnumerable<WorkItemDto>>(_mockWorkItems);
    }

    /// <summary>
    /// Adds a mock work item for testing purposes.
    /// </summary>
    public void AddMockWorkItem(WorkItemDto workItem)
    {
        _mockWorkItems.Add(workItem);
    }

    /// <summary>
    /// Clears all mock work items.
    /// </summary>
    public void ClearMockWorkItems()
    {
        _mockWorkItems.Clear();
    }
}
