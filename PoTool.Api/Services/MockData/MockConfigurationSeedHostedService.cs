using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.WorkItems;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Seeds a minimal but realistic mock configuration so mock mode is immediately usable.
/// </summary>
public sealed class MockConfigurationSeedHostedService : IHostedService
{
    private const string MockProjectId = "mock-project-battleship-systems";
    private const string MockProjectAlias = "battleship-systems";
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

    private static readonly MockPortfolioSnapshotSeed[] PortfolioSnapshotTimeline =
    [
        new("Empty portfolio", DateTime.UnixEpoch, MockPortfolioSnapshotStage.Empty),
        new(
            "Sprint 1 - Initial backlog",
            new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc),
            MockPortfolioSnapshotStage.InitialBacklog),
        new(
            "Sprint 2 - Work starts",
            new DateTime(2026, 1, 26, 0, 0, 0, DateTimeKind.Utc),
            MockPortfolioSnapshotStage.WorkStarts),
        new(
            "Sprint 3 - New work added",
            new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc),
            MockPortfolioSnapshotStage.NewWorkAdded),
        new(
            "Sprint 3 - Completion checkpoint",
            new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc),
            MockPortfolioSnapshotStage.CompletionCheckpoint),
        new(
            "Sprint 4 - Reprioritized backlog",
            new DateTime(2026, 2, 23, 0, 0, 0, DateTimeKind.Utc),
            MockPortfolioSnapshotStage.Reprioritized),
        new(
            "Sprint 5 - Majority completed",
            new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc),
            MockPortfolioSnapshotStage.MajorityCompleted),
        new(
            "Sprint 6 - Near completion",
            new DateTime(2026, 3, 23, 0, 0, 0, DateTimeKind.Utc),
            MockPortfolioSnapshotStage.NearCompletion)
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

            var project = await EnsureMockProjectAsync(context, cancellationToken);
            var teams = await SeedTeamsAsync(context, hierarchy, now, cancellationToken);
            await SeedProfilesAndProductsAsync(context, hierarchy, goals, teams, project, now, cancellationToken);
        }

        await EnsureMockRepositoriesAsync(context, now, cancellationToken);
        await EnsureMockTfsConfigurationAsync(context, cancellationToken);
        await EnsureActiveProfileAsync(context, cancellationToken);
        await EnsureMockPortfolioSnapshotsAsync(context, mockDataFacade.GetMockHierarchy(), cancellationToken);

        _logger.LogInformation(
            "Seeded mock configuration with {ProfileCount} profiles, {ProductCount} products, {TeamCount} teams, {RepositoryCount} repositories, and {SnapshotCount} portfolio snapshots.",
            await context.Profiles.CountAsync(cancellationToken),
            await context.Products.CountAsync(cancellationToken),
            await context.Teams.CountAsync(cancellationToken),
            await context.Repositories.CountAsync(cancellationToken),
            await context.PortfolioSnapshots.CountAsync(cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<ProjectEntity> EnsureMockProjectAsync(
        PoToolDbContext context,
        CancellationToken cancellationToken)
    {
        var existingProject = await context.Projects
            .FirstOrDefaultAsync(project =>
                project.Id == MockProjectId ||
                project.Alias == MockProjectAlias ||
                project.Name == MockProjectName,
                cancellationToken);

        if (existingProject != null)
        {
            return existingProject;
        }

        var project = new ProjectEntity
        {
            Id = MockProjectId,
            Alias = MockProjectAlias,
            Name = MockProjectName
        };

        context.Projects.Add(project);
        await SaveChangesWithDiagnosticsAsync(
            context,
            cancellationToken,
            $"creating mock project '{MockProjectName}'");
        return project;
    }

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

    private async Task<List<TeamEntity>> SeedTeamsAsync(
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
        await SaveChangesWithDiagnosticsAsync(
            context,
            cancellationToken,
            "creating mock teams");
        return teams;
    }

    private async Task SeedProfilesAndProductsAsync(
        PoToolDbContext context,
        IReadOnlyCollection<PoTool.Shared.WorkItems.WorkItemDto> hierarchy,
        IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto> goals,
        IReadOnlyCollection<TeamEntity> teams,
        ProjectEntity project,
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
            await SaveChangesWithDiagnosticsAsync(
                context,
                cancellationToken,
                $"creating mock profile '{profile.Name}'");

            for (var productOrder = 0; productOrder < profileSeed.Products.Length; productOrder++)
            {
                var productSeed = profileSeed.Products[productOrder];
                var rootIds = productSeed.GoalIndexes
                    .Select(index => goals[index].TfsId)
                    .ToList();

                var product = new ProductEntity
                {
                    ProductOwnerId = profile.Id,
                    ProjectId = project.Id,
                    Name = productSeed.Name,
                    Order = productOrder,
                    PictureType = (int)ProductPictureType.Default,
                    DefaultPictureId = productSeed.DefaultPictureId,
                    EstimationMode = (int)Shared.Settings.EstimationMode.StoryPoints,
                    CreatedAt = now,
                    LastModified = now
                };

                context.Products.Add(product);
                await SaveChangesWithDiagnosticsAsync(
                    context,
                    cancellationToken,
                    $"creating mock product '{product.Name}' for profile '{profile.Name}'");

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

                ValidateProductSeedDependencies(product, project, relevantAreaPaths);

                context.ProductTeamLinks.AddRange(relevantAreaPaths.Select(areaPath => new ProductTeamLinkEntity
                {
                    ProductId = product.Id,
                    TeamId = teamByAreaPath[areaPath].Id
                }));

                await SaveChangesWithDiagnosticsAsync(
                    context,
                    cancellationToken,
                    $"creating backlog roots and team links for product '{product.Name}'");
            }
        }
    }

    private async Task EnsureMockRepositoriesAsync(
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
        await SaveChangesWithDiagnosticsAsync(
            context,
            cancellationToken,
            "creating mock repositories");
    }

    private async Task EnsureActiveProfileAsync(
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

        await SaveChangesWithDiagnosticsAsync(
            context,
            cancellationToken,
            "setting active mock profile");
    }

    private static async Task EnsureMockPortfolioSnapshotsAsync(
        PoToolDbContext context,
        IReadOnlyCollection<PoTool.Shared.WorkItems.WorkItemDto> hierarchy,
        CancellationToken cancellationToken)
    {
        var targetProfileId = await context.Profiles
            .OrderBy(profile => profile.Id)
            .Select(profile => (int?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!targetProfileId.HasValue)
        {
            return;
        }

        var targetProducts = await context.Products
            .Include(product => product.BacklogRoots)
            .Where(product => product.ProductOwnerId == targetProfileId.Value)
            .OrderBy(product => product.Id)
            .ToListAsync(cancellationToken);

        if (targetProducts.Count == 0)
        {
            return;
        }

        var plans = targetProducts
            .Select(product => CreateMockPortfolioPlan(product, hierarchy))
            .Where(plan => plan is not null)
            .Cast<MockPortfolioPlan>()
            .ToList();

        if (plans.Count == 0)
        {
            return;
        }

        var targetProductIds = plans.Select(plan => plan.ProductId).ToArray();
        var existingKeys = await context.PortfolioSnapshots
            .AsNoTracking()
            .Where(snapshot => targetProductIds.Contains(snapshot.ProductId))
            .Select(snapshot => new MockPortfolioSnapshotKey(snapshot.ProductId, snapshot.TimestampUtc, snapshot.Source))
            .ToListAsync(cancellationToken);
        var existingKeySet = existingKeys.ToHashSet();
        var mapper = new PortfolioSnapshotPersistenceMapper();

        foreach (var timelineEntry in PortfolioSnapshotTimeline)
        {
            var addedGroupSnapshot = false;

            foreach (var plan in plans)
            {
                var key = new MockPortfolioSnapshotKey(plan.ProductId, timelineEntry.TimestampUtc, timelineEntry.Source);
                if (existingKeySet.Contains(key))
                {
                    continue;
                }

                var snapshot = new PortfolioSnapshot(
                    new DateTimeOffset(timelineEntry.TimestampUtc, TimeSpan.Zero),
                    BuildPortfolioSnapshotItems(plan, timelineEntry.Stage));
                context.PortfolioSnapshots.Add(
                    mapper.ToEntity(
                        plan.ProductId,
                        timelineEntry.Source,
                        "Mock configuration seed",
                        isArchived: false,
                        snapshot));
                existingKeySet.Add(key);
                addedGroupSnapshot = true;
            }

            if (addedGroupSnapshot)
            {
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SaveChangesWithDiagnosticsAsync(
        PoToolDbContext context,
        CancellationToken cancellationToken,
        string operation)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var trackedEntries = context.ChangeTracker.Entries()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                .Select(entry =>
                {
                    var properties = entry.Properties
                        .Select(property => $"{property.Metadata.Name}={FormatPropertyValue(property.CurrentValue)}")
                        .ToArray();

                    return $"{entry.Entity.GetType().Name} [{string.Join(", ", properties)}]";
                })
                .ToArray();

            _logger.LogError(
                ex,
                "Mock configuration seeding failed while {Operation}. Pending entities: {PendingEntities}",
                operation,
                trackedEntries.Length == 0 ? "none" : string.Join(" | ", trackedEntries));

            throw;
        }
    }

    private static string FormatPropertyValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text when string.IsNullOrWhiteSpace(text) => "\"\"",
            DateTime dateTime => dateTime.ToString("O"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static void ValidateProductSeedDependencies(
        ProductEntity product,
        ProjectEntity project,
        IReadOnlyCollection<string> relevantAreaPaths)
    {
        if (string.IsNullOrWhiteSpace(project.Id))
        {
            throw new InvalidOperationException(
                $"Mock seeding cannot create product '{product.Name}' because the mock project ID is missing.");
        }

        if (!string.Equals(product.ProjectId, project.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Mock seeding cannot create product '{product.Name}' because Product.ProjectId '{product.ProjectId}' does not match project '{project.Id}'.");
        }

        if (relevantAreaPaths.Count == 0)
        {
            throw new InvalidOperationException(
                $"Mock seeding cannot create product-team links for product '{product.Name}' because no seeded teams matched its backlog roots.");
        }
    }
        }
    }

    private static MockPortfolioPlan? CreateMockPortfolioPlan(
        ProductEntity product,
        IReadOnlyCollection<PoTool.Shared.WorkItems.WorkItemDto> hierarchy)
    {
        var rootIds = product.BacklogRoots
            .Select(root => root.WorkItemTfsId)
            .Distinct()
            .ToList();

        if (rootIds.Count == 0)
        {
            return null;
        }

        var productItems = WorkItemHierarchyHelper.FilterDescendants(rootIds, hierarchy)
            .OrderBy(item => item.TfsId)
            .ToList();
        if (productItems.Count == 0)
        {
            return null;
        }

        var objectives = productItems
            .Where(item => item.Type.Equals(WorkItemType.Objective, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.TfsId)
            .ToList();
        var objectiveById = objectives.ToDictionary(item => item.TfsId);
        var templates = new List<MockPortfolioWorkPackageTemplate>();
        var selectedEpicIds = new HashSet<int>();

        foreach (var objective in objectives)
        {
            var epics = productItems
                .Where(item =>
                    item.Type.Equals(WorkItemType.Epic, StringComparison.OrdinalIgnoreCase)
                    && item.ParentTfsId == objective.TfsId)
                .OrderBy(item => item.TfsId)
                .Take(2)
                .ToList();

            foreach (var epic in epics)
            {
                if (!selectedEpicIds.Add(epic.TfsId))
                {
                    continue;
                }

                templates.Add(new MockPortfolioWorkPackageTemplate(
                    CreateBusinessKey("OBJ", objective),
                    CreateBusinessKey("EPIC", epic),
                    Math.Max(1d, epic.Effort ?? 8d)));
            }

            if (templates.Count >= 4)
            {
                break;
            }
        }

        if (templates.Count < 4)
        {
            foreach (var epic in productItems
                         .Where(item => item.Type.Equals(WorkItemType.Epic, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(item => item.TfsId))
            {
                if (!epic.ParentTfsId.HasValue
                    || !selectedEpicIds.Add(epic.TfsId)
                    || !objectiveById.TryGetValue(epic.ParentTfsId.Value, out var objective))
                {
                    continue;
                }

                templates.Add(new MockPortfolioWorkPackageTemplate(
                    CreateBusinessKey("OBJ", objective),
                    CreateBusinessKey("EPIC", epic),
                    Math.Max(1d, epic.Effort ?? 8d)));

                if (templates.Count >= 4)
                {
                    break;
                }
            }
        }

        return templates.Count == 0
            ? null
            : new MockPortfolioPlan(product.Id, product.Name, templates);
    }

    private static IReadOnlyList<PortfolioSnapshotItem> BuildPortfolioSnapshotItems(
        MockPortfolioPlan plan,
        MockPortfolioSnapshotStage stage)
    {
        var items = new List<PortfolioSnapshotItem>();

        switch (stage)
        {
            case MockPortfolioSnapshotStage.Empty:
                return items;

            case MockPortfolioSnapshotStage.InitialBacklog:
                AddSnapshotItem(items, plan, 0, 0d);
                AddSnapshotItem(items, plan, 1, 0d);
                AddSnapshotItem(items, plan, 2, 0d);
                break;

            case MockPortfolioSnapshotStage.WorkStarts:
                AddSnapshotItem(items, plan, 0, 0.28d);
                AddSnapshotItem(items, plan, 1, 0.18d);
                AddSnapshotItem(items, plan, 2, 0.08d);
                break;

            case MockPortfolioSnapshotStage.NewWorkAdded:
                AddSnapshotItem(items, plan, 0, 0.48d);
                AddSnapshotItem(items, plan, 1, 0.34d);
                AddSnapshotItem(items, plan, 2, 0.18d);
                AddSnapshotItem(items, plan, 3, 0d);
                break;

            case MockPortfolioSnapshotStage.CompletionCheckpoint:
                AddSnapshotItem(items, plan, 0, 0.78d);
                AddSnapshotItem(items, plan, 1, 0.56d);
                AddSnapshotItem(items, plan, 2, 0.36d);
                AddSnapshotItem(items, plan, 3, 0.16d);
                break;

            case MockPortfolioSnapshotStage.Reprioritized:
                AddSnapshotItem(items, plan, 0, 1d);
                AddSnapshotItem(items, plan, 1, 0.74d);
                AddSnapshotItem(items, plan, 2, 0.36d, WorkPackageLifecycleState.Retired);
                AddSnapshotItem(items, plan, 3, 0.28d);
                break;

            case MockPortfolioSnapshotStage.MajorityCompleted:
                AddSnapshotItem(items, plan, 0, 1d);
                AddSnapshotItem(items, plan, 1, 1d);
                AddSnapshotItem(items, plan, 2, 0.36d, WorkPackageLifecycleState.Retired);
                AddSnapshotItem(items, plan, 3, 0.78d);
                break;

            case MockPortfolioSnapshotStage.NearCompletion:
                AddSnapshotItem(items, plan, 0, 1d);
                AddSnapshotItem(items, plan, 1, 1d);
                AddSnapshotItem(items, plan, 2, 0.36d, WorkPackageLifecycleState.Retired);
                AddSnapshotItem(items, plan, 3, 1d);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown mock portfolio snapshot stage.");
        }

        return items;
    }

    private static void AddSnapshotItem(
        List<PortfolioSnapshotItem> items,
        MockPortfolioPlan plan,
        int index,
        double progress,
        WorkPackageLifecycleState lifecycleState = WorkPackageLifecycleState.Active)
    {
        if (index >= plan.WorkPackages.Count)
        {
            return;
        }

        var template = plan.WorkPackages[index];
        items.Add(new PortfolioSnapshotItem(
            plan.ProductId,
            template.ProjectNumber,
            template.WorkPackage,
            progress,
            template.TotalWeight,
            lifecycleState));
    }

    private static string CreateBusinessKey(string prefix, PoTool.Shared.WorkItems.WorkItemDto workItem)
    {
        var value = $"{prefix}-{workItem.TfsId}: {workItem.Title}";
        return value.Length <= 180 ? value : value[..180];
    }

    private sealed record MockProfileSeed(
        string Name,
        int DefaultPictureId,
        MockProductSeed[] Products);

    private sealed record MockProductSeed(
        string Name,
        int[] GoalIndexes,
        int DefaultPictureId);

    private sealed record MockPortfolioPlan(
        int ProductId,
        string ProductName,
        IReadOnlyList<MockPortfolioWorkPackageTemplate> WorkPackages);

    private sealed record MockPortfolioWorkPackageTemplate(
        string ProjectNumber,
        string WorkPackage,
        double TotalWeight);

    private sealed record MockPortfolioSnapshotSeed(
        string Source,
        DateTime TimestampUtc,
        MockPortfolioSnapshotStage Stage);

    private sealed record MockPortfolioSnapshotKey(
        int ProductId,
        DateTime TimestampUtc,
        string Source);

    private enum MockPortfolioSnapshotStage
    {
        Empty,
        InitialBacklog,
        WorkStarts,
        NewWorkAdded,
        CompletionCheckpoint,
        Reprioritized,
        MajorityCompleted,
        NearCompletion
    }
}
