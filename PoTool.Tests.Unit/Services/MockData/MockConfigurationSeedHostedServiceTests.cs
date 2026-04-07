using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PoTool.Api.Configuration;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Services;
using PoTool.Api.Services.Onboarding;
using PoTool.Core.Filters;
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
        await using var provider = await CreateSqliteServiceProviderAsync();
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
        var triageTags = await context.TriageTags
            .OrderBy(tag => tag.DisplayOrder)
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
        Assert.IsTrue(tfsConfig.HasTestedConnectionSuccessfully, "Mock mode should seed a ready-to-use tested connection.");
        Assert.IsTrue(tfsConfig.HasVerifiedTfsApiSuccessfully, "Mock mode should seed a ready-to-use verified TFS API state.");
        Assert.HasCount(6, triageTags, "Mock mode should seed the triage tag catalog for bug triage.");
        Assert.IsTrue(triageTags.All(tag => tag.IsEnabled), "Seeded mock triage tags should be enabled.");
    }

    [TestMethod]
    public async Task StartAsync_WhenCalledTwice_DoesNotDuplicateMockConfiguration()
    {
        await using var provider = await CreateSqliteServiceProviderAsync();
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
        var triageTags = await context.TriageTags.ToListAsync();

        Assert.HasCount(3, profiles, "Seeding should remain idempotent for profiles.");
        Assert.HasCount(6, products, "Seeding should remain idempotent for products.");
        Assert.HasCount(6, repositories, "Seeding should remain idempotent for repositories.");
        Assert.IsGreaterThanOrEqualTo(8, teams.Count, "Seeding should remain idempotent for teams.");
        Assert.HasCount(16, portfolioSnapshots, "Seeding should remain idempotent for the battleship CDC history.");
        Assert.HasCount(1, tfsConfigs, "Seeding should remain idempotent for mock TFS configuration.");
        Assert.HasCount(6, triageTags, "Seeding should remain idempotent for the mock triage tag catalog.");
    }

    [TestMethod]
    public async Task StartAsync_WhenHappyBindingChainScenarioSelected_SeedsConnectionOnlyOnboardingGraph()
    {
        await using var provider = await CreateSqliteServiceProviderAsync(OnboardingVerificationScenarioNames.HappyBindingChain);
        var service = provider.GetRequiredService<MockConfigurationSeedHostedService>();

        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        Assert.AreEqual(1, await context.OnboardingTfsConnections.CountAsync());
        Assert.AreEqual(0, await context.OnboardingProjectSources.CountAsync());
        Assert.AreEqual(0, await context.OnboardingProductRoots.CountAsync());
        Assert.AreEqual(0, await context.OnboardingProductSourceBindings.CountAsync());
    }

    [TestMethod]
    public async Task StartAsync_WhenTeamAssignmentScenarioSelected_SeedsReachableAssignmentBlocker()
    {
        await using var provider = await CreateSqliteServiceProviderAsync(OnboardingVerificationScenarioNames.TeamAssignment);
        var service = provider.GetRequiredService<MockConfigurationSeedHostedService>();

        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        Assert.AreEqual(1, await context.OnboardingTfsConnections.CountAsync());
        Assert.AreEqual(1, await context.OnboardingProjectSources.CountAsync());
        Assert.AreEqual(1, await context.OnboardingProductRoots.CountAsync());
        Assert.AreEqual(2, await context.OnboardingProductSourceBindings.CountAsync());
        Assert.AreEqual(2, await context.OnboardingTeamSources.CountAsync(), "Team-assignment verification should seed the invalid source plus one reachable replacement candidate.");

        var statusService = new OnboardingStatusService(context, Mock.Of<IOnboardingObservability>());
        var status = await statusService.GetStatusAsync(CancellationToken.None);

        Assert.IsTrue(status.Succeeded);
        CollectionAssert.Contains(status.Data!.BlockingReasons.Select(issue => issue.Code).ToList(), "TEAM_BINDING_SOURCE_INVALID");
    }

    [TestMethod]
    public async Task StartAsync_SeedsOrderedPortfolioHistoryForActiveProfile()
    {
        await using var provider = await CreateSqliteServiceProviderAsync();
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
            FilterContext.Empty(),
            new PortfolioReadQueryOptions(SnapshotCount: 1),
            CancellationToken.None);
        var latestTwo = await stateService.GetHistoryStateAsync(
            activeProfileId.Value,
            FilterContext.Empty(),
            new PortfolioReadQueryOptions(SnapshotCount: 2),
            CancellationToken.None);
        var fullHistory = await stateService.GetHistoryStateAsync(
            activeProfileId.Value,
            FilterContext.Empty(),
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

    [TestMethod]
    public async Task StartAsync_WhenUsingSqlite_SeedsMockConfigurationWithoutForeignKeyViolations()
    {
        await using var provider = await CreateSqliteServiceProviderAsync();

        var service = provider.GetRequiredService<MockConfigurationSeedHostedService>();
        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var project = await context.Projects.SingleAsync();
        var products = await context.Products
            .Include(product => product.Project)
            .Include(product => product.ProductOwner)
            .Include(product => product.ProductTeamLinks)
            .Include(product => product.BacklogRoots)
            .Include(product => product.Repositories)
            .OrderBy(product => product.Id)
            .ToListAsync();
        var teams = await context.Teams.ToListAsync();
        var productTeamLinks = await context.ProductTeamLinks.ToListAsync();
        var settings = await context.Settings.SingleAsync();
        var tfsConfig = await context.TfsConfigs.SingleAsync();
        var snapshots = await context.PortfolioSnapshots
            .Include(snapshot => snapshot.Items)
            .OrderBy(snapshot => snapshot.ProductId)
            .ThenBy(snapshot => snapshot.TimestampUtc)
            .ToListAsync();

        Assert.AreEqual("mock-project-battleship-systems", project.Id);
        Assert.AreEqual("battleship-systems", project.Alias);
        Assert.AreEqual("Battleship Systems", project.Name);
        Assert.HasCount(6, products, "Expected SQLite seeding to create the mock products.");
        Assert.IsTrue(products.All(product => product.ProjectId == project.Id), "Each seeded product should reference the seeded mock project.");
        Assert.IsTrue(products.All(product => product.Project?.Id == project.Id), "Each seeded product should materialize the seeded mock project.");
        Assert.IsTrue(products.All(product => product.ProductOwner is not null), "Each seeded product should materialize its profile owner.");
        Assert.IsTrue(products.All(product => product.BacklogRoots.Count > 0), "Each seeded product should retain backlog roots under SQLite.");
        Assert.IsTrue(products.All(product => product.Repositories.Count > 0), "Each seeded product should retain repositories under SQLite.");
        Assert.IsNotEmpty(productTeamLinks, "Expected seeded product-team links to exist under SQLite.");
        Assert.IsTrue(productTeamLinks.All(link => teams.Any(team => team.Id == link.TeamId)), "Each seeded product-team link should reference an existing team.");
        Assert.AreEqual(settings.ActiveProfileId, products.First().ProductOwnerId, "The deterministic active profile should own the first seeded product.");
        Assert.AreEqual("https://dev.azure.com/mock", tfsConfig.Url);
        Assert.HasCount(16, snapshots, "Expected the deterministic snapshot timeline to persist under SQLite.");
        Assert.IsTrue(snapshots.All(snapshot => snapshot.Items.Count > 0 || snapshot.Source == "Empty portfolio"), "Each snapshot should include rows except the empty baseline.");
    }

    [TestMethod]
    public async Task StartAsync_WhenSqliteHasPartialMockCoreState_CompletesDeterministically()
    {
        await using var provider = await CreateSqliteServiceProviderAsync();

        await using (var setupScope = provider.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<PoToolDbContext>();
            setupContext.Profiles.Add(new ProfileEntity
            {
                Name = "Commander Elena Marquez",
                GoalIds = string.Empty,
                PictureType = 0,
                DefaultPictureId = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                LastModified = DateTimeOffset.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        var service = provider.GetRequiredService<MockConfigurationSeedHostedService>();
        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var profiles = await context.Profiles.OrderBy(profile => profile.Name).ToListAsync();
        var products = await context.Products.OrderBy(product => product.Name).ToListAsync();
        var teams = await context.Teams.OrderBy(team => team.TeamAreaPath).ToListAsync();
        var repositories = await context.Repositories.ToListAsync();
        var snapshots = await context.PortfolioSnapshots.ToListAsync();

        Assert.HasCount(3, profiles, "Partial mock state should be repaired to the full seeded profile set.");
        Assert.HasCount(6, products, "Partial mock state should still produce the full seeded product set.");
        Assert.IsGreaterThanOrEqualTo(8, teams.Count, "Partial mock state should still produce the full seeded team set.");
        Assert.HasCount(6, repositories, "Partial mock state should still produce the deterministic repository set.");
        Assert.HasCount(16, snapshots, "Partial mock state should still produce the deterministic portfolio snapshot set.");
        Assert.AreEqual(
            profiles.Count,
            profiles.Select(profile => profile.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            "Partial mock state repair should not duplicate profile roots.");
    }

    [TestMethod]
    public async Task StartAsync_WhenUsingSqlite_ResolvesAllRequiredSeedForeignKeys()
    {
        await using var provider = await CreateSqliteServiceProviderAsync();

        var service = provider.GetRequiredService<MockConfigurationSeedHostedService>();
        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        await AssertRequiredForeignKeysResolvedAsync<ProductEntity>(context);
        await AssertRequiredForeignKeysResolvedAsync<ProductBacklogRootEntity>(context);
        await AssertRequiredForeignKeysResolvedAsync<ProductTeamLinkEntity>(context);
        await AssertRequiredForeignKeysResolvedAsync<RepositoryEntity>(context);
        await AssertRequiredForeignKeysResolvedAsync<PortfolioSnapshotEntity>(context);
    }

    private static PortfolioReadModelStateService CreateStateService(PoToolDbContext context)
        => new(
            context,
            new PortfolioSnapshotSelectionService(
                context,
                new PortfolioSnapshotPersistenceMapper(),
                NullLogger<PortfolioSnapshotSelectionService>.Instance),
            new ProductAggregationService());

    private static ServiceProvider CreateSqliteServiceProvider(SqliteConnection connection, string scenario = OnboardingVerificationScenarioNames.HappyBindingChain)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PoToolDbContext>(options => options.UseSqlite(connection));
        ConfigureCommonServices(services, scenario);

        return services.BuildServiceProvider();
    }

    private static async Task<ServiceProvider> CreateSqliteServiceProviderAsync(string scenario = OnboardingVerificationScenarioNames.HappyBindingChain)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var provider = CreateSqliteServiceProvider(connection, scenario);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        await context.Database.EnsureCreatedAsync();

        return provider;
    }

    private static async Task AssertRequiredForeignKeysResolvedAsync<TEntity>(PoToolDbContext context)
        where TEntity : class
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        Assert.IsNotNull(entityType, $"Expected EF model metadata for {typeof(TEntity).Name}.");

        var requiredForeignKeys = entityType.GetForeignKeys()
            .Where(IsRequiredForeignKey)
            .ToList();
        var entities = await context.Set<TEntity>()
            .AsNoTracking()
            .ToListAsync();

        foreach (var entity in entities)
        {
            foreach (var foreignKey in requiredForeignKeys)
            {
                var values = foreignKey.Properties
                    .Select(property => typeof(TEntity).GetProperty(property.Name)?.GetValue(entity))
                    .ToArray();

                Assert.IsFalse(
                    values.Any(value => IsMissingForeignKeyValue(value)),
                    $"{typeof(TEntity).Name} should assign required FK '{string.Join(", ", foreignKey.Properties.Select(property => property.Name))}'.");

                var principal = await context.FindAsync(foreignKey.PrincipalEntityType.ClrType, values);
                Assert.IsNotNull(
                    principal,
                    $"{typeof(TEntity).Name} should resolve parent '{foreignKey.PrincipalEntityType.ClrType.Name}' for FK '{string.Join(", ", foreignKey.Properties.Select(property => property.Name))}'.");
            }
        }
    }

    private static bool IsRequiredForeignKey(IForeignKey foreignKey)
        => foreignKey.Properties.All(property => !property.IsNullable);

    private static bool IsMissingForeignKeyValue(object? value)
        => value switch
        {
            null => true,
            string text => string.IsNullOrWhiteSpace(text),
            int number => number == 0,
            long number => number == 0,
            _ => false
        };

    private static void ConfigureCommonServices(IServiceCollection services, string scenario)
    {
        services.AddSingleton(new TfsRuntimeMode(useMockClient: true));
        services.AddSingleton<IOptions<OnboardingVerificationOptions>>(Options.Create(new OnboardingVerificationOptions
        {
            SelectedScenario = scenario
        }));
        services.AddSingleton<IOnboardingVerificationScenarioService, OnboardingVerificationScenarioService>();
        services.AddSingleton<BattleshipWorkItemGenerator>();
        services.AddSingleton<BattleshipDependencyGenerator>();
        services.AddSingleton<BattleshipPullRequestGenerator>();
        services.AddSingleton<BattleshipPipelineGenerator>();
        services.AddSingleton<MockDataValidator>();
        services.AddSingleton<BattleshipMockDataFacade>();
        services.AddSingleton<MockConfigurationSeedHostedService>();
    }
}
