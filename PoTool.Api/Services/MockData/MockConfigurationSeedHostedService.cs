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

        if (hasProfiles || hasProducts || hasTeams)
        {
            await EnsureActiveProfileAsync(context, cancellationToken);
            return;
        }

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

        var now = DateTimeOffset.UtcNow;
        var teams = await SeedTeamsAsync(context, hierarchy, now, cancellationToken);
        await SeedProfilesAndProductsAsync(context, hierarchy, goals, teams, now, cancellationToken);
        await EnsureActiveProfileAsync(context, cancellationToken);

        _logger.LogInformation(
            "Seeded mock configuration with {ProfileCount} profiles, {ProductCount} products, and {TeamCount} teams.",
            await context.Profiles.CountAsync(cancellationToken),
            await context.Products.CountAsync(cancellationToken),
            await context.Teams.CountAsync(cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
                ProjectName = "Battleship Systems",
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
