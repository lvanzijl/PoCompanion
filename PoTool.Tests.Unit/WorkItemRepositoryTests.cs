using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Repositories;
using PoTool.Shared.WorkItems;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class WorkItemRepositoryTests
{
    private PoToolDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(databaseName: $"{Guid.NewGuid()}")
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
                ParentTfsId: null,
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 1",
                State: "Active",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null,
                    Description: null,
                    Tags: null
            ),
            new WorkItemDto(
                TfsId: 2,
                Type: "Feature",
                Title: "Feature 1",
                ParentTfsId: null,
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 1",
                State: "Active",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null,
                    Description: null,
                    Tags: null
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
                ParentTfsId: null,
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 1",
                State: "Active",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null,
                    Description: null,
                    Tags: null
            )
        };

        var newWorkItems = new[]
        {
            new WorkItemDto(
                TfsId: 2,
                Type: "Feature",
                Title: "New Feature",
                ParentTfsId: null,
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 2",
                State: "Active",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null,
                    Description: null,
                    Tags: null
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
                ParentTfsId: null,
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 1",
                State: "Active",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null,
                    Description: null,
                    Tags: null
            ),
            new WorkItemDto(
                TfsId: 2,
                Type: "Feature",
                Title: "Database Migration",
                ParentTfsId: null,
                AreaPath: "Project\\Team",
                IterationPath: "Sprint 1",
                State: "Active",
                RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null,
                    Description: null,
                    Tags: null
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
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
                Effort: null,
                    Description: null,
                    Tags: null
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

    [TestMethod]
    public async Task ReplaceAllAsync_PreservesCreatedDate()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        var createdDate = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var workItem = new WorkItemDto(
            TfsId: 100,
            Type: "Epic",
            Title: "Epic with CreatedDate",
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: "Test description",
            CreatedDate: createdDate
        );

        // Act
        await repository.ReplaceAllAsync(new[] { workItem });
        var result = await repository.GetByTfsIdAsync(100);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(createdDate, result.CreatedDate, "CreatedDate should be preserved when saving and retrieving work items");
        Assert.AreEqual("Test description", result.Description);
    }

    [TestMethod]
    public async Task UpsertManyAsync_PreservesCreatedDate()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        var createdDate = new DateTimeOffset(2025, 2, 20, 14, 45, 0, TimeSpan.Zero);
        var workItem = new WorkItemDto(
            TfsId: 200,
            Type: "Feature",
            Title: "Feature with CreatedDate",
            ParentTfsId: 100,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 2",
            State: "New",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: 10,
            Description: "Feature description",
            CreatedDate: createdDate
        );

        // Act
        await repository.UpsertManyAsync(new[] { workItem });
        var result = await repository.GetByTfsIdAsync(200);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(createdDate, result.CreatedDate, "CreatedDate should be preserved during upsert operations");
        Assert.AreEqual("Feature description", result.Description);
        Assert.AreEqual(10, result.Effort);
    }

    [TestMethod]
    public async Task UpsertManyAsync_PersistsBusinessValue()
    {
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        var workItem = new WorkItemDto(
            TfsId: 300,
            Type: "Feature",
            Title: "Feature with business value",
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 3",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: 5,
            Description: "Feature description",
            BusinessValue: 13
        );

        await repository.UpsertManyAsync(new[] { workItem });
        var result = await repository.GetByTfsIdAsync(300);

        Assert.IsNotNull(result);
        Assert.AreEqual(13, result.BusinessValue);
    }

    [TestMethod]
    public async Task UpsertManyAsync_PreservesStoryPointsSeparatelyFromEffortAndBusinessValue()
    {
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        var workItem = new WorkItemDto(
            TfsId: 301,
            Type: "Product Backlog Item",
            Title: "PBI with separate estimates",
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 3",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: 8,
            Description: "PBI description",
            BusinessValue: 21,
            StoryPoints: 5
        );

        await repository.UpsertManyAsync(new[] { workItem });
        var result = await repository.GetByTfsIdAsync(301);

        Assert.IsNotNull(result);
        Assert.AreEqual(8, result.Effort);
        Assert.AreEqual(5, result.StoryPoints);
        Assert.AreEqual(21, result.BusinessValue);
    }

    [TestMethod]
    public async Task UpsertManyAsync_UpdatesBacklogPriority_ForExistingEntity()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        var originalWorkItem = new WorkItemDto(
            TfsId: 400,
            Type: "Objective",
            Title: "Objective",
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null,
            BacklogPriority: null
        );

        // Insert the original item (BacklogPriority = null)
        await repository.UpsertManyAsync(new[] { originalWorkItem });

        // Act — upsert again with a BacklogPriority value
        var updatedWorkItem = originalWorkItem with { BacklogPriority = 750.0 };
        await repository.UpsertManyAsync(new[] { updatedWorkItem });

        var result = await repository.GetByTfsIdAsync(400);

        // Assert — BacklogPriority must be updated for the existing entity
        Assert.IsNotNull(result);
        Assert.AreEqual(750.0, result.BacklogPriority, "UpsertManyAsync must update BacklogPriority for existing entities");
    }

    [TestMethod]
    public async Task UpsertManyAsync_ClearsBacklogPriority_WhenUpdatedToNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new WorkItemRepository(context);

        var original = new WorkItemDto(
            TfsId: 401,
            Type: "Objective",
            Title: "Objective",
            ParentTfsId: null,
            AreaPath: "Project\\Team",
            IterationPath: "Sprint 1",
            State: "Active",
            RetrievedAt: DateTimeOffset.UtcNow,
            Effort: null,
            Description: null,
            BacklogPriority: 500.0
        );

        await repository.UpsertManyAsync(new[] { original });

        // Act — upsert with BacklogPriority = null
        var cleared = original with { BacklogPriority = null };
        await repository.UpsertManyAsync(new[] { cleared });

        var result = await repository.GetByTfsIdAsync(401);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNull(result.BacklogPriority, "UpsertManyAsync must clear BacklogPriority when updated to null");
    }
}
