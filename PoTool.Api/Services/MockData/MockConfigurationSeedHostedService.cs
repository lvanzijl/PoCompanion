using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Seeds a minimal but realistic mock configuration so mock mode is immediately usable.
/// </summary>
public sealed class MockConfigurationSeedHostedService : IHostedService
{
    private const string MockOrganizationUrl = "https://dev.azure.com/mock";
    private const string MockProjectName = "Battleship Systems";

    private static readonly MockProfileSeed[] ProfileSeeds =
    [
        new(
            "Commander Elena Marquez",
            3,
            [
                new MockProductSeed("Incident Response Control", [0, 5], 9),
                new MockProductSeed("Crew Safety Operations", [2], 11)
            ]),
        new(
            "Commander Noah Patel",
            7,
            [
                new MockProductSeed("Damage Control Platform", [1, 3], 15),
                new MockProductSeed("Predictive Maintenance Insights", [6], 17)
            ]),
        new(
            "Commander Sophie van Dijk",
            12,
            [
                new MockProductSeed("Communication & Coordination", [4, 8], 21),
                new MockProductSeed("Portfolio Reporting", [7, 9], 23)
            ])
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MockConfigurationSeedHostedService> _logger;

    public MockConfigurationSeedHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<MockConfigurationSeedHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        var mockDataFacade = scope.ServiceProvider.GetRequiredService<BattleshipMockDataFacade>();

        var hasProfiles = await context.Profiles.AnyAsync(cancellationToken);
        var hasProducts = await context.Products.AnyAsync(cancellationToken);
        var hasTeams = await context.Teams.AnyAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (!hasProfiles && !hasProducts && !hasTeams)
        {
            var hierarchy = mockDataFacade.GetMockHierarchy();
            var goals = hierarchy
                .Where(item => item.Type.Equals(WorkItemType.Goal, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.TfsId)
                .ToList();

            if (goals.Count == 0)
            {
                _logger.LogWarning("Mock mode configuration seeding skipped because no goal roots were generated.");
                return;
            }

            var teams = await SeedTeamsAsync(context, hierarchy, now, cancellationToken);
            await SeedProfilesAndProductsAsync(context, hierarchy, goals, teams, now, cancellationToken);
        }

        await EnsureMockRepositoriesAsync(context, now, cancellationToken);
        await EnsureMockTfsConfigurationAsync(context, cancellationToken);
        await EnsureActiveProfileAsync(context, cancellationToken);

        _logger.LogInformation(
            "Seeded mock configuration with {ProfileCount} profiles, {ProductCount} products, {TeamCount} teams, and {RepositoryCount} repositories.",
            await context.Profiles.CountAsync(cancellationToken),
            await context.Products.CountAsync(cancellationToken),
            await context.Teams.CountAsync(cancellationToken),
            await context.Repositories.CountAsync(cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureMockTfsConfigurationAsync(
        PoToolDbContext context,
        CancellationToken cancellationToken)
    {
        var existingConfig = await context.TfsConfigs
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingConfig != null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        context.TfsConfigs.Add(new TfsConfigEntity
        {
            Url = MockOrganizationUrl,
            Project = MockProjectName,
            DefaultAreaPath = MockProjectName,
            UseDefaultCredentials = true,
            TimeoutSeconds = 30,
            ApiVersion = "7.0",
            LastValidated = now,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedAtUtc = now.UtcDateTime
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<List<TeamEntity>> SeedTeamsAsync(
        PoToolDbContext context,
        IReadOnlyCollection<PoTool.Shared.WorkItems.WorkItemDto> hierarchy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var teamAreaPaths = hierarchy
            .Where(item => item.Type.Equals(WorkItemType.Epic, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.AreaPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var teams = teamAreaPaths
            .Select((areaPath, index) => new TeamEntity
            {
                Name = areaPath.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last(),
                TeamAreaPath = areaPath,
                PictureType = (int)TeamPictureType.Default,
                DefaultPictureId = (index * 3) % 24,
                ProjectName = MockProjectName,
                TfsTeamId = $"mock-team-{index + 1:D2}",
                TfsTeamName = areaPath.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last(),
                CreatedAt = now,
                LastModified = now
            })
            .ToList();

        context.Teams.AddRange(teams);
        await context.SaveChangesAsync(cancellationToken);
        return teams;
    }

    private static async Task SeedProfilesAndProductsAsync(
        PoToolDbContext context,
        IReadOnlyCollection<PoTool.Shared.WorkItems.WorkItemDto> hierarchy,
        IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto> goals,
        IReadOnlyCollection<TeamEntity> teams,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var teamByAreaPath = teams.ToDictionary(team => team.TeamAreaPath, StringComparer.OrdinalIgnoreCase);

        foreach (var profileSeed in ProfileSeeds)
        {
            var ownedGoalIds = profileSeed.Products
                .SelectMany(product => product.GoalIndexes)
                .Distinct()
                .Select(index => goals[index].TfsId)
                .ToList();

            var profile = new ProfileEntity
            {
                Name = profileSeed.Name,
                GoalIds = string.Join(",", ownedGoalIds),
                PictureType = (int)ProfilePictureType.Default,
                DefaultPictureId = profileSeed.DefaultPictureId,
                CreatedAt = now,
                LastModified = now
            };

            context.Profiles.Add(profile);
            await context.SaveChangesAsync(cancellationToken);

            for (var productOrder = 0; productOrder < profileSeed.Products.Length; productOrder++)
            {
                var productSeed = profileSeed.Products[productOrder];
                var rootIds = productSeed.GoalIndexes
                    .Select(index => goals[index].TfsId)
                    .ToList();

                var product = new ProductEntity
                {
                    ProductOwnerId = profile.Id,
                    Name = productSeed.Name,
                    Order = productOrder,
                    PictureType = (int)ProductPictureType.Default,
                    DefaultPictureId = productSeed.DefaultPictureId,
                    EstimationMode = (int)Shared.Settings.EstimationMode.StoryPoints,
                    CreatedAt = now,
                    LastModified = now
                };

                context.Products.Add(product);
                await context.SaveChangesAsync(cancellationToken);

                context.ProductBacklogRoots.AddRange(rootIds.Select(rootId => new ProductBacklogRootEntity
                {
                    ProductId = product.Id,
                    WorkItemTfsId = rootId
                }));

                var relevantAreaPaths = WorkItemHierarchyHelper.FilterDescendants(rootIds, hierarchy)
                    .Select(item => item.AreaPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(teamByAreaPath.ContainsKey)
                    .ToList();

                context.ProductTeamLinks.AddRange(relevantAreaPaths.Select(areaPath => new ProductTeamLinkEntity
                {
                    ProductId = product.Id,
                    TeamId = teamByAreaPath[areaPath].Id
                }));

                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static async Task EnsureMockRepositoriesAsync(
        PoToolDbContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var products = await context.Products
            .OrderBy(product => product.Id)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
        {
            return;
        }

        var existingRepositories = await context.Repositories
            .ToListAsync(cancellationToken);

        var existingByProduct = existingRepositories
            .GroupBy(repository => repository.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(repository => repository.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var repositoriesToAdd = new List<RepositoryEntity>();

        foreach (var product in products)
        {
            var targetRepositories = MockDevOpsSeedCatalog.GetRepositoryNamesForProduct(product.Name);
            if (targetRepositories.Count == 0)
            {
                continue;
            }

            existingByProduct.TryGetValue(product.Id, out var existingNames);

            repositoriesToAdd.AddRange(targetRepositories
                .Where(repositoryName => existingNames == null || !existingNames.Contains(repositoryName))
                .Select(repositoryName => new RepositoryEntity
                {
                    ProductId = product.Id,
                    Name = repositoryName,
                    CreatedAt = now
                }));
        }

        if (repositoriesToAdd.Count == 0)
        {
            return;
        }

        context.Repositories.AddRange(repositoriesToAdd);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureActiveProfileAsync(
        PoToolDbContext context,
        CancellationToken cancellationToken)
    {
        var firstProfileId = await context.Profiles
            .OrderBy(profile => profile.Id)
            .Select(profile => (int?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!firstProfileId.HasValue)
        {
            return;
        }

        var settings = await context.Settings
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings == null)
        {
            context.Settings.Add(new SettingsEntity
            {
                ActiveProfileId = firstProfileId,
                LastModified = DateTimeOffset.UtcNow
            });
        }
        else if (settings.ActiveProfileId == null)
        {
            settings.ActiveProfileId = firstProfileId;
            settings.LastModified = DateTimeOffset.UtcNow;
        }
        else
        {
            return;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record MockProfileSeed(
        string Name,
        int DefaultPictureId,
        MockProductSeed[] Products);

    private sealed record MockProductSeed(
        string Name,
        int[] GoalIndexes,
        int DefaultPictureId);
}
