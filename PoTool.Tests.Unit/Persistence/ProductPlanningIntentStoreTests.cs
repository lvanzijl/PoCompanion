using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Repositories;
using PoTool.Core.Planning;

namespace PoTool.Tests.Unit.Persistence;

[TestClass]
public sealed class ProductPlanningIntentStoreTests
{
    private static PoToolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var context = new PoToolDbContext(options);
        context.Products.Add(new ProductEntity
        {
            Id = 7,
            Name = "Roadmap Product",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModified = DateTimeOffset.UtcNow
        });
        context.SaveChanges();
        return context;
    }

    [TestMethod]
    public async Task UpsertAndLoad_ReturnsPersistedPlanningIntent()
    {
        await using var context = CreateContext();
        var store = new ProductPlanningIntentStore(context);
        var nowUtc = DateTime.UtcNow;

        await store.UpsertForProductAsync(7,
        [
            new ProductPlanningIntentRecord(7, 1001, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 2, ProductPlanningRecoveryStatus.RecoveredExact, nowUtc)
        ]);

        var intents = await store.GetByProductAsync(7);

        Assert.HasCount(1, intents);
        Assert.AreEqual(1001, intents[0].EpicId);
        Assert.AreEqual(2, intents[0].DurationInSprints);
        Assert.AreEqual(ProductPlanningRecoveryStatus.RecoveredExact, intents[0].RecoveryStatus);
    }

    [TestMethod]
    public async Task UpsertForProductAsync_UpdatesExistingEpicIntent()
    {
        await using var context = CreateContext();
        var store = new ProductPlanningIntentStore(context);

        await store.UpsertForProductAsync(7,
        [
            new ProductPlanningIntentRecord(7, 1001, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 1, null, DateTime.UtcNow)
        ]);

        await store.UpsertForProductAsync(7,
        [
            new ProductPlanningIntentRecord(7, 1001, new DateTime(2026, 1, 19, 0, 0, 0, DateTimeKind.Utc), 3, ProductPlanningRecoveryStatus.RecoveredWithNormalization, DateTime.UtcNow)
        ]);

        var intents = await store.GetByProductAsync(7);

        Assert.HasCount(1, intents);
        Assert.AreEqual(new DateTime(2026, 1, 19, 0, 0, 0, DateTimeKind.Utc), intents[0].StartSprintStartDateUtc);
        Assert.AreEqual(3, intents[0].DurationInSprints);
        Assert.AreEqual(ProductPlanningRecoveryStatus.RecoveredWithNormalization, intents[0].RecoveryStatus);
    }

    [TestMethod]
    public async Task DeleteMissingEpicsAsync_RemovesStaleRows()
    {
        await using var context = CreateContext();
        var store = new ProductPlanningIntentStore(context);

        await store.UpsertForProductAsync(7,
        [
            new ProductPlanningIntentRecord(7, 1001, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), 1, null, DateTime.UtcNow),
            new ProductPlanningIntentRecord(7, 1002, new DateTime(2026, 1, 19, 0, 0, 0, DateTimeKind.Utc), 1, null, DateTime.UtcNow)
        ]);

        await store.DeleteMissingEpicsAsync(7, [1002]);
        var intents = await store.GetByProductAsync(7);

        CollectionAssert.AreEqual(new[] { 1002 }, intents.Select(static intent => intent.EpicId).ToArray());
    }
}
