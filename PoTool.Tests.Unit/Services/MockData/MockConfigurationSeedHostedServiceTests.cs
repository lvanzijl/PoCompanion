using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Services.MockData;

[TestClass]
public sealed class MockConfigurationSeedHostedServiceTests
{
    [TestMethod]
    public async Task StartAsync_WhenDatabaseIsEmpty_SeedsUsableMockConfiguration()
    {
        await using var provider = CreateServiceProvider();
        var service = provider.GetRequiredService<MockConfigurationSeedHostedService>();

        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var profiles = await context.Profiles
            .Include(profile => profile.Products)
            .OrderBy(profile => profile.Id)
            .ToListAsync();
        var products = await context.Products
            .Include(product => product.BacklogRoots)
            .Include(product => product.ProductTeamLinks)
            .ToListAsync();
        var repositories = await context.Repositories
            .OrderBy(repository => repository.ProductId)
            .ThenBy(repository => repository.Name)
            .ToListAsync();
        var teams = await context.Teams.ToListAsync();
        var portfolioSnapshots = await context.PortfolioSnapshots
            .Include(snapshot => snapshot.Items)
            .OrderBy(snapshot => snapshot.ProductId)
            .ThenBy(snapshot => snapshot.TimestampUtc)
            .ThenBy(snapshot => snapshot.SnapshotId)
            .ToListAsync();
        var settings = await context.Settings.OrderByDescending(item => item.Id).FirstOrDefaultAsync();
        var tfsConfig = await context.TfsConfigs.OrderByDescending(item => item.UpdatedAtUtc).FirstOrDefaultAsync();

        Assert.HasCount(3, profiles, "Expected multiple mock product owners to be seeded.");
        Assert.HasCount(6, products, "Expected multiple products per owner to be seeded.");
        Assert.HasCount(6, repositories, "Expected one realistic repository per seeded product.");
        Assert.HasCount(16, portfolioSnapshots, "Expected battleship CDC history for the active profile products.");
        Assert.IsGreaterThanOrEqualTo(8, teams.Count, "Expected realistic team coverage to be seeded.");
        Assert.IsNotNull(settings, "Expected mock settings to be created.");
        Assert.IsNotNull(tfsConfig, "Expected mock TFS configuration to be created.");
        Assert.IsNotNull(settings.ActiveProfileId, "Expected an active profile to be selected.");
        Assert.IsTrue(profiles.All(profile => profile.Products.Count >= 2), "Each mock profile should own multiple products.");
        Assert.IsTrue(products.All(product => product.BacklogRoots.Count > 0), "Each seeded product should have backlog roots.");
        Assert.IsTrue(products.All(product => product.ProductTeamLinks.Count > 0), "Each seeded product should be linked to at least one team.");
        Assert.IsTrue(products.All(product => repositories.Count(repository => repository.ProductId == product.Id) == 1),
            "Each seeded product should receive exactly one repository for PR and pipeline sync.");
        Assert.AreEqual(
            2,
            portfolioSnapshots.Select(snapshot => snapshot.ProductId).Distinct().Count(),
            "Only the active battleship portfolio should receive the seeded CDC timeline.");
        Assert.IsTrue(
            portfolioSnapshots.All(snapshot => snapshot.Source.Length > 0),
            "Seeded CDC snapshots should expose deterministic source labels.");
        Assert.AreEqual(
            52,
            portfolioSnapshots.Sum(snapshot => snapshot.Items.Count),
            "The seeded CDC history should contain deterministic snapshot rows derived from the battleship work packages.");
        Assert.AreEqual("Empty portfolio", portfolioSnapshots.First().Source);
        Assert.AreEqual(DateTime.UnixEpoch, DateTime.SpecifyKind(portfolioSnapshots.First().TimestampUtc, DateTimeKind.Utc));
        Assert.AreEqual("https://dev.azure.com/mock", tfsConfig.Url);
        Assert.AreEqual("Battleship Systems", tfsConfig.Project);
        Assert.AreEqual("Battleship Systems", tfsConfig.DefaultAreaPath);
    }

    [TestMethod]
    public async Task StartAsync_WhenCalledTwice_DoesNotDuplicateMockConfiguration()
    {
        await using var provider = CreateServiceProvider();
        var service = provider.GetRequiredService<MockConfigurationSeedHostedService>();

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var profiles = await context.Profiles.ToListAsync();
        var products = await context.Products.ToListAsync();
        var repositories = await context.Repositories.ToListAsync();
        var teams = await context.Teams.ToListAsync();
        var portfolioSnapshots = await context.PortfolioSnapshots.ToListAsync();
        var tfsConfigs = await context.TfsConfigs.ToListAsync();

        Assert.HasCount(3, profiles, "Seeding should remain idempotent for profiles.");
        Assert.HasCount(6, products, "Seeding should remain idempotent for products.");
        Assert.HasCount(6, repositories, "Seeding should remain idempotent for repositories.");
        Assert.IsGreaterThanOrEqualTo(8, teams.Count, "Seeding should remain idempotent for teams.");
        Assert.HasCount(16, portfolioSnapshots, "Seeding should remain idempotent for the battleship CDC history.");
        Assert.HasCount(1, tfsConfigs, "Seeding should remain idempotent for mock TFS configuration.");
    }

    [TestMethod]
    public async Task StartAsync_SeedsOrderedPortfolioHistoryForActiveProfile()
    {
        await using var provider = CreateServiceProvider();
        var service = provider.GetRequiredService<MockConfigurationSeedHostedService>();

        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var activeProfileId = await context.Settings
            .OrderByDescending(item => item.Id)
            .Select(item => item.ActiveProfileId)
            .FirstAsync();

        Assert.IsNotNull(activeProfileId, "Expected the mock seed to set an active profile.");

        var stateService = CreateStateService(context);

        var latestOnly = await stateService.GetHistoryStateAsync(
            activeProfileId.Value,
            new PortfolioReadQueryOptions(SnapshotCount: 1),
            CancellationToken.None);
        var latestTwo = await stateService.GetHistoryStateAsync(
            activeProfileId.Value,
            new PortfolioReadQueryOptions(SnapshotCount: 2),
            CancellationToken.None);
        var fullHistory = await stateService.GetHistoryStateAsync(
            activeProfileId.Value,
            new PortfolioReadQueryOptions(SnapshotCount: 20),
            CancellationToken.None);

        Assert.IsNotNull(latestOnly);
        Assert.IsNotNull(latestTwo);
        Assert.IsNotNull(fullHistory);

        Assert.HasCount(1, latestOnly.Snapshots);
        Assert.AreEqual("Sprint 6 - Near completion", latestOnly.Snapshots[0].Source);

        CollectionAssert.AreEqual(
            new[] { "Sprint 6 - Near completion", "Sprint 5 - Majority completed" },
            latestTwo.Snapshots.Select(snapshot => snapshot.Source).ToArray());

        CollectionAssert.AreEqual(
            new[]
            {
                "Sprint 6 - Near completion",
                "Sprint 5 - Majority completed",
                "Sprint 4 - Reprioritized backlog",
                "Sprint 3 - Completion checkpoint",
                "Sprint 3 - New work added",
                "Sprint 2 - Work starts",
                "Sprint 1 - Initial backlog",
                "Empty portfolio"
            },
            fullHistory.Snapshots.Select(snapshot => snapshot.Source).ToArray(),
            "History should order equal timestamps by descending SnapshotId after timestamp ordering.");

        Assert.AreEqual(
            DateTime.UnixEpoch,
            DateTime.SpecifyKind(fullHistory.Snapshots[^1].Snapshot.Timestamp.UtcDateTime, DateTimeKind.Utc),
            "The oldest snapshot should reuse the UnixEpoch fallback timestamp.");
        Assert.IsEmpty(fullHistory.Snapshots[^1].Snapshot.Items, "The fallback snapshot should represent the empty portfolio state.");
        Assert.IsTrue(
            fullHistory.Snapshots[0].Snapshot.Items.Any(item => item.LifecycleState == WorkPackageLifecycleState.Retired),
            "The seeded timeline should preserve a reprioritized work package in later snapshots.");
        Assert.IsTrue(
            fullHistory.Snapshots[0].Snapshot.Items
                .Where(item => item.LifecycleState == WorkPackageLifecycleState.Active)
                .All(item => Math.Abs(item.Progress - 1d) < 0.0001d),
            "The latest snapshot should show active work packages at full completion.");
    }

    private static PortfolioReadModelStateService CreateStateService(PoToolDbContext context)
        => new(
            context,
            new PortfolioSnapshotSelectionService(
                context,
                new PortfolioSnapshotPersistenceMapper(),
                NullLogger<PortfolioSnapshotSelectionService>.Instance),
            new ProductAggregationService());

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        var databaseName = $"mock-seed-{Guid.NewGuid()}";
        services.AddLogging();
        services.AddDbContext<PoToolDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<BattleshipWorkItemGenerator>();
        services.AddSingleton<BattleshipDependencyGenerator>();
        services.AddSingleton<BattleshipPullRequestGenerator>();
        services.AddSingleton<BattleshipPipelineGenerator>();
        services.AddSingleton<MockDataValidator>();
        services.AddSingleton<BattleshipMockDataFacade>();
        services.AddSingleton<MockConfigurationSeedHostedService>();

        return services.BuildServiceProvider();
    }
}
