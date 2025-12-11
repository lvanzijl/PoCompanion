using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Repositories;
using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class WorkItemRepositoryTests
{
    private PoToolDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PoToolDbContext(options);
    }

    [TestMethod]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoWorkItems()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public async Task ReplaceAllAsync_AddsWorkItems_Successfully()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        var workItems = new[]
        {
            new WorkItemDto(
                TfsId: 1,
                Type: "Epic",
                Title: "Epic 1",
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 1",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow
            ),
            new WorkItemDto(
                TfsId: 2,
                Type: "Feature",
                Title: "Feature 1",
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 1",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow
            )
        };

        // Act
        await repository.ReplaceAllAsync(workItems);
        var result = await repository.GetAllAsync();

        // Assert
        Assert.AreEqual(2, result.Count());
    }

    [TestMethod]
    public async Task ReplaceAllAsync_ReplacesExistingWorkItems()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        var initialWorkItems = new[]
        {
            new WorkItemDto(
                TfsId: 1,
                Type: "Epic",
                Title: "Initial Epic",
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 1",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow
            )
        };

        var newWorkItems = new[]
        {
            new WorkItemDto(
                TfsId: 2,
                Type: "Feature",
                Title: "New Feature",
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 2",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow
            )
        };

        // Act
        await repository.ReplaceAllAsync(initialWorkItems);
        await repository.ReplaceAllAsync(newWorkItems);
        var result = await repository.GetAllAsync();

        // Assert
        Assert.AreEqual(1, result.Count());
        Assert.AreEqual(2, result.First().TfsId);
        Assert.AreEqual("New Feature", result.First().Title);
    }

    [TestMethod]
    public async Task GetFilteredAsync_ReturnsMatchingWorkItems()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        var workItems = new[]
        {
            new WorkItemDto(
                TfsId: 1,
                Type: "Epic",
                Title: "User Authentication Epic",
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 1",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow
            ),
            new WorkItemDto(
                TfsId: 2,
                Type: "Feature",
                Title: "Database Migration",
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 1",
                State: "Active",
                JsonPayload: "{}",
                RetrievedAt: DateTimeOffset.UtcNow
            )
        };

        await repository.ReplaceAllAsync(workItems);

        // Act
        var result = await repository.GetFilteredAsync("Authentication");

        // Assert
        Assert.AreEqual(1, result.Count());
        Assert.AreEqual("User Authentication Epic", result.First().Title);
    }

    [TestMethod]
    public async Task GetByTfsIdAsync_ReturnsWorkItem_WhenExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        var workItem = new WorkItemDto(
            TfsId: 123,
            Type: "Epic",
            Title: "Test Epic",
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            JsonPayload: "{}",
            RetrievedAt: DateTimeOffset.UtcNow
        );

        await repository.ReplaceAllAsync(new[] { workItem });

        // Act
        var result = await repository.GetByTfsIdAsync(123);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(123, result.TfsId);
        Assert.AreEqual("Test Epic", result.Title);
    }

    [TestMethod]
    public async Task GetByTfsIdAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        // Act
        var result = await repository.GetByTfsIdAsync(999);

        // Assert
        Assert.IsNull(result);
    }
}
