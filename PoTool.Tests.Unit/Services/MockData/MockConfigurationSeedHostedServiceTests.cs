using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PoTool.Api.Persistence;
using PoTool.Api.Services.MockData;

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
        var settings = await context.Settings.OrderByDescending(item => item.Id).FirstOrDefaultAsync();
        var tfsConfig = await context.TfsConfigs.OrderByDescending(item => item.UpdatedAtUtc).FirstOrDefaultAsync();

        Assert.HasCount(3, profiles, "Expected multiple mock product owners to be seeded.");
        Assert.HasCount(6, products, "Expected multiple products per owner to be seeded.");
        Assert.HasCount(6, repositories, "Expected one realistic repository per seeded product.");
        Assert.IsGreaterThanOrEqualTo(8, teams.Count, "Expected realistic team coverage to be seeded.");
        Assert.IsNotNull(settings, "Expected mock settings to be created.");
        Assert.IsNotNull(tfsConfig, "Expected mock TFS configuration to be created.");
        Assert.IsNotNull(settings.ActiveProfileId, "Expected an active profile to be selected.");
        Assert.IsTrue(profiles.All(profile => profile.Products.Count >= 2), "Each mock profile should own multiple products.");
        Assert.IsTrue(products.All(product => product.BacklogRoots.Count > 0), "Each seeded product should have backlog roots.");
        Assert.IsTrue(products.All(product => product.ProductTeamLinks.Count > 0), "Each seeded product should be linked to at least one team.");
        Assert.IsTrue(products.All(product => repositories.Count(repository => repository.ProductId == product.Id) == 1),
            "Each seeded product should receive exactly one repository for PR and pipeline sync.");
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
        var tfsConfigs = await context.TfsConfigs.ToListAsync();

        Assert.HasCount(3, profiles, "Seeding should remain idempotent for profiles.");
        Assert.HasCount(6, products, "Seeding should remain idempotent for products.");
        Assert.HasCount(6, repositories, "Seeding should remain idempotent for repositories.");
        Assert.IsGreaterThanOrEqualTo(8, teams.Count, "Seeding should remain idempotent for teams.");
        Assert.HasCount(1, tfsConfigs, "Seeding should remain idempotent for mock TFS configuration.");
    }

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
