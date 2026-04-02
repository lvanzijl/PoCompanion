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
/// Startup seeding must remain dependency-ordered, assign required foreign keys explicitly,
/// and pass SQLite-backed relational validation before commit.
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
        var now = DateTimeOffset.UtcNow;
        var hierarchy = mockDataFacade.GetMockHierarchy();
        var seedPlan = BuildSeedPlan(hierarchy);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var project = await EnsureMockProjectAsync(context, cancellationToken);
        var teamsByAreaPath = await EnsureMockTeamsAsync(context, seedPlan.Teams, now, cancellationToken);
        await EnsureMockProfilesAndProductsAsync(context, seedPlan, project, teamsByAreaPath, now, cancellationToken);
        await SaveChangesWithDiagnosticsAsync(
            context,
            cancellationToken,
            "persisting mock core configuration");

        await EnsureMockTfsConfigurationAsync(context, now, cancellationToken);
        await EnsureActiveProfileAsync(context, seedPlan.ActiveProfileName, cancellationToken);
        await EnsureMockPortfolioSnapshotsAsync(context, seedPlan.ActiveProfileName, hierarchy, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded mock configuration with {ProfileCount} profiles, {ProductCount} products, {TeamCount} teams, {RepositoryCount} repositories, and {SnapshotCount} portfolio snapshots.",
            await context.Profiles.CountAsync(cancellationToken),
            await context.Products.CountAsync(cancellationToken),
            await context.Teams.CountAsync(cancellationToken),
            await context.Repositories.CountAsync(cancellationToken),
            await context.PortfolioSnapshots.CountAsync(cancellationToken));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static MockSeedPlan BuildSeedPlan(
        IReadOnlyCollection<PoTool.Shared.WorkItems.WorkItemDto> hierarchy)
    {
        var goals = hierarchy
            .Where(item => item.Type.Equals(WorkItemType.Goal, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.TfsId)
            .ToList();

        if (goals.Count == 0)
        {
            throw new InvalidOperationException(
                "Mock configuration seeding cannot start because the generated hierarchy does not contain any goal roots.");
        }

        var teams = hierarchy
            .Where(item => item.Type.Equals(WorkItemType.Epic, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.AreaPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select((areaPath, index) => new MockTeamPlan(
                areaPath.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last(),
                areaPath,
                (index * 3) % 24,
                $"mock-team-{index + 1:D2}"))
            .ToList();

        if (teams.Count == 0)
        {
            throw new InvalidOperationException(
                "Mock configuration seeding cannot start because the generated hierarchy does not expose any epic team area paths.");
        }

        var teamAreaPaths = teams
            .Select(team => team.AreaPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var profilePlans = ProfileSeeds
            .Select(profileSeed => BuildProfilePlan(profileSeed, goals, hierarchy, teamAreaPaths))
            .ToList();

        return new MockSeedPlan(
            teams,
            profilePlans,
            profilePlans[0].Name);
    }

    private static MockProfilePlan BuildProfilePlan(
        MockProfileSeed profileSeed,
        IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto> goals,
        IReadOnlyCollection<PoTool.Shared.WorkItems.WorkItemDto> hierarchy,
        IReadOnlySet<string> teamAreaPaths)
    {
        var ownedGoalIds = profileSeed.Products
            .SelectMany(product => product.GoalIndexes)
            .Distinct()
            .Select(index => ResolveGoalId(goals, profileSeed.Name, index))
            .ToList();

        var products = profileSeed.Products
            .Select((productSeed, productOrder) => BuildProductPlan(
                profileSeed,
                productSeed,
                productOrder,
                goals,
                hierarchy,
                teamAreaPaths))
            .ToList();

        return new MockProfilePlan(
            profileSeed.Name,
            profileSeed.DefaultPictureId,
            ownedGoalIds,
            products);
    }

    private static MockProductPlan BuildProductPlan(
        MockProfileSeed profileSeed,
        MockProductSeed productSeed,
        int productOrder,
        IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto> goals,
        IReadOnlyCollection<PoTool.Shared.WorkItems.WorkItemDto> hierarchy,
        IReadOnlySet<string> teamAreaPaths)
    {
        var rootIds = productSeed.GoalIndexes
            .Select(index => ResolveGoalId(goals, profileSeed.Name, index))
            .ToList();

        var descendants = WorkItemHierarchyHelper.FilterDescendants(rootIds, hierarchy)
            .ToList();

        if (descendants.Count == 0)
        {
            throw new InvalidOperationException(
                $"Mock configuration seeding cannot create product '{productSeed.Name}' because its backlog roots do not resolve to any hierarchy descendants.");
        }

        var relevantAreaPaths = descendants
            .Select(item => item.AreaPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(teamAreaPaths.Contains)
            .OrderBy(areaPath => areaPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (relevantAreaPaths.Count == 0)
        {
            throw new InvalidOperationException(
                $"Mock configuration seeding cannot create product '{productSeed.Name}' because no seeded teams match its hierarchy descendants.");
        }

        var repositoryNames = MockDevOpsSeedCatalog.GetRepositoryNamesForProduct(productSeed.Name);
        if (repositoryNames.Count == 0)
        {
            throw new InvalidOperationException(
                $"Mock configuration seeding cannot create product '{productSeed.Name}' because no deterministic repositories are registered for it.");
        }

        return new MockProductPlan(
            productSeed.Name,
            productSeed.DefaultPictureId,
            productOrder,
            rootIds,
            relevantAreaPaths,
            repositoryNames);
    }

    private static int ResolveGoalId(
        IReadOnlyList<PoTool.Shared.WorkItems.WorkItemDto> goals,
        string profileName,
        int goalIndex)
    {
        if (goalIndex < 0 || goalIndex >= goals.Count)
        {
            throw new InvalidOperationException(
                $"Mock configuration seeding cannot resolve goal index '{goalIndex}' for profile '{profileName}' because the generated hierarchy only contains {goals.Count} goals.");
        }

        return goals[goalIndex].TfsId;
    }

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

        if (existingProject == null)
        {
            existingProject = new ProjectEntity
            {
                Id = MockProjectId
            };
            context.Projects.Add(existingProject);
        }

        existingProject.Alias = MockProjectAlias;
        existingProject.Name = MockProjectName;
        return existingProject;
    }

    private async Task EnsureMockTfsConfigurationAsync(
        PoToolDbContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingConfig = await context.TfsConfigs
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingConfig == null)
        {
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
        }
        else
        {
            existingConfig.Url = MockOrganizationUrl;
            existingConfig.Project = MockProjectName;
            existingConfig.DefaultAreaPath = MockProjectName;
            existingConfig.UseDefaultCredentials = true;
            existingConfig.TimeoutSeconds = 30;
            existingConfig.ApiVersion = "7.0";
            existingConfig.LastValidated = now;
            existingConfig.UpdatedAt = now;
            existingConfig.UpdatedAtUtc = now.UtcDateTime;
        }

        await SaveChangesWithDiagnosticsAsync(
            context,
            cancellationToken,
            "persisting mock TFS configuration");
    }

    private async Task<Dictionary<string, TeamEntity>> EnsureMockTeamsAsync(
        PoToolDbContext context,
        IReadOnlyList<MockTeamPlan> teamPlans,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var expectedAreaPaths = teamPlans
            .Select(team => team.AreaPath)
            .ToList();

        var existingTeams = await context.Teams
            .Where(team => expectedAreaPaths.Contains(team.TeamAreaPath))
            .ToListAsync(cancellationToken);

        EnsureNoDuplicateEntities(
            existingTeams,
            team => team.TeamAreaPath,
            "mock teams",
            "TeamAreaPath");

        var teamsByAreaPath = existingTeams.ToDictionary(team => team.TeamAreaPath, StringComparer.OrdinalIgnoreCase);
        foreach (var teamPlan in teamPlans)
        {
            if (!teamsByAreaPath.TryGetValue(teamPlan.AreaPath, out var team))
            {
                team = new TeamEntity
                {
                    TeamAreaPath = teamPlan.AreaPath,
                    CreatedAt = now
                };
                context.Teams.Add(team);
                teamsByAreaPath.Add(teamPlan.AreaPath, team);
            }

            team.Name = teamPlan.Name;
            team.PictureType = (int)TeamPictureType.Default;
            team.DefaultPictureId = teamPlan.DefaultPictureId;
            team.ProjectName = MockProjectName;
            team.TfsTeamId = teamPlan.TfsTeamId;
            team.TfsTeamName = teamPlan.Name;
            team.LastModified = now;
        }

        return teamsByAreaPath;
    }

    private async Task EnsureMockProfilesAndProductsAsync(
        PoToolDbContext context,
        MockSeedPlan seedPlan,
        ProjectEntity project,
        IReadOnlyDictionary<string, TeamEntity> teamsByAreaPath,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var profileNames = seedPlan.Profiles
            .Select(profile => profile.Name)
            .ToList();
        var productNames = seedPlan.Profiles
            .SelectMany(profile => profile.Products)
            .Select(product => product.Name)
            .ToList();

        var existingProfiles = await context.Profiles
            .Include(profile => profile.Products)
            .Where(profile => profileNames.Contains(profile.Name))
            .ToListAsync(cancellationToken);
        var existingProducts = await context.Products
            .Include(product => product.BacklogRoots)
            .Include(product => product.ProductTeamLinks)
            .Include(product => product.Repositories)
            .Where(product => product.ProjectId == MockProjectId && productNames.Contains(product.Name))
            .ToListAsync(cancellationToken);

        EnsureNoDuplicateEntities(
            existingProfiles,
            profile => profile.Name,
            "mock profiles",
            "Name");
        EnsureNoDuplicateEntities(
            existingProducts,
            product => product.Name,
            "mock products",
            "Name");

        var profilesByName = existingProfiles.ToDictionary(profile => profile.Name, StringComparer.OrdinalIgnoreCase);
        var productsByName = existingProducts.ToDictionary(product => product.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var profilePlan in seedPlan.Profiles)
        {
            if (!profilesByName.TryGetValue(profilePlan.Name, out var profile))
            {
                profile = new ProfileEntity
                {
                    Name = profilePlan.Name,
                    CreatedAt = now
                };
                context.Profiles.Add(profile);
                profilesByName.Add(profilePlan.Name, profile);
            }

            profile.GoalIds = string.Join(",", profilePlan.OwnedGoalIds);
            profile.PictureType = (int)ProfilePictureType.Default;
            profile.DefaultPictureId = profilePlan.DefaultPictureId;
            profile.LastModified = now;

            foreach (var productPlan in profilePlan.Products)
            {
                if (!productsByName.TryGetValue(productPlan.Name, out var product))
                {
                    product = new ProductEntity
                    {
                        Name = productPlan.Name,
                        CreatedAt = now
                    };
                    context.Products.Add(product);
                    productsByName.Add(productPlan.Name, product);
                }

                product.ProductOwner = profile;
                product.Project = project;
                product.Order = productPlan.Order;
                product.PictureType = (int)ProductPictureType.Default;
                product.DefaultPictureId = productPlan.DefaultPictureId;
                product.EstimationMode = (int)Shared.Settings.EstimationMode.StoryPoints;
                product.LastModified = now;

                ValidateProductSeedDependencies(product, project, productPlan.TeamAreaPaths);
                ReconcileBacklogRoots(product, productPlan.RootIds);
                ReconcileProductTeamLinks(product, productPlan.TeamAreaPaths, teamsByAreaPath);
                ReconcileRepositories(product, productPlan.RepositoryNames, now);
            }
        }
    }

    private async Task EnsureActiveProfileAsync(
        PoToolDbContext context,
        string activeProfileName,
        CancellationToken cancellationToken)
    {
        var activeProfileId = await context.Profiles
            .Where(profile => profile.Name == activeProfileName)
            .Select(profile => (int?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!activeProfileId.HasValue)
        {
            throw new InvalidOperationException(
                $"Mock configuration seeding cannot set the active profile because profile '{activeProfileName}' does not exist.");
        }

        var settings = await context.Settings
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings == null)
        {
            context.Settings.Add(new SettingsEntity
            {
                ActiveProfileId = activeProfileId,
                LastModified = DateTimeOffset.UtcNow
            });
        }
        else if (settings.ActiveProfileId != activeProfileId)
        {
            settings.ActiveProfileId = activeProfileId;
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

    private async Task EnsureMockPortfolioSnapshotsAsync(
        PoToolDbContext context,
        string activeProfileName,
        IReadOnlyCollection<PoTool.Shared.WorkItems.WorkItemDto> hierarchy,
        CancellationToken cancellationToken)
    {
        var targetProfileId = await context.Profiles
            .Where(profile => profile.Name == activeProfileName)
            .Select(profile => (int?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!targetProfileId.HasValue)
        {
            throw new InvalidOperationException(
                $"Mock configuration seeding cannot create portfolio snapshots because profile '{activeProfileName}' does not exist.");
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

        var snapshotsToAdd = new List<PortfolioSnapshotEntity>();
        foreach (var timelineEntry in PortfolioSnapshotTimeline)
        {
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
                snapshotsToAdd.Add(
                    mapper.ToEntity(
                        plan.ProductId,
                        timelineEntry.Source,
                        "Mock configuration seed",
                        isArchived: false,
                        snapshot));
                existingKeySet.Add(key);
            }
        }

        if (snapshotsToAdd.Count == 0)
        {
            return;
        }

        context.PortfolioSnapshots.AddRange(snapshotsToAdd);
        await SaveChangesWithDiagnosticsAsync(
            context,
            cancellationToken,
            "persisting mock portfolio snapshots");
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
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
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

        if (!string.Equals(product.ProjectId, project.Id, StringComparison.Ordinal)
            && !ReferenceEquals(product.Project, project))
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

    private static void ReconcileBacklogRoots(
        ProductEntity product,
        IReadOnlyCollection<int> rootIds)
    {
        var targetRootIds = rootIds.ToHashSet();
        var existingRootIds = product.BacklogRoots
            .Select(root => root.WorkItemTfsId)
            .ToHashSet();

        foreach (var root in product.BacklogRoots.Where(root => !targetRootIds.Contains(root.WorkItemTfsId)).ToList())
        {
            product.BacklogRoots.Remove(root);
        }

        foreach (var rootId in targetRootIds.Where(rootId => !existingRootIds.Contains(rootId)))
        {
            product.BacklogRoots.Add(new ProductBacklogRootEntity
            {
                Product = product,
                WorkItemTfsId = rootId
            });
        }
    }

    private static void ReconcileProductTeamLinks(
        ProductEntity product,
        IReadOnlyCollection<string> teamAreaPaths,
        IReadOnlyDictionary<string, TeamEntity> teamsByAreaPath)
    {
        var targetTrackedTeams = teamAreaPaths
            .Select(areaPath =>
            {
                if (!teamsByAreaPath.TryGetValue(areaPath, out var team))
                {
                    throw new InvalidOperationException(
                        $"Mock configuration seeding cannot create product-team links for product '{product.Name}' because team area path '{areaPath}' was not resolved.");
                }

                return team;
            })
            .ToList();

        var targetResolvedTeamIds = targetTrackedTeams
            .Where(team => team.Id != 0)
            .Select(team => team.Id)
            .ToHashSet();
        var targetPendingTeams = targetTrackedTeams
            .Where(team => team.Id == 0)
            .ToHashSet();

        foreach (var link in product.ProductTeamLinks
                     .Where(link =>
                         (link.TeamId != 0 && !targetResolvedTeamIds.Contains(link.TeamId))
                         || (link.TeamId == 0 && link.Team is not null && !targetPendingTeams.Contains(link.Team)))
                     .ToList())
        {
            product.ProductTeamLinks.Remove(link);
        }

        var existingResolvedTeamIds = product.ProductTeamLinks
            .Where(link => link.TeamId != 0)
            .Select(link => link.TeamId)
            .ToHashSet();
        var existingPendingTeams = product.ProductTeamLinks
            .Where(link => link.TeamId == 0 && link.Team is not null)
            .Select(link => link.Team)
            .ToHashSet();

        foreach (var team in targetTrackedTeams.Where(team => team.Id != 0 && !existingResolvedTeamIds.Contains(team.Id)))
        {
            product.ProductTeamLinks.Add(new ProductTeamLinkEntity
            {
                Product = product,
                Team = team
            });
        }

        foreach (var team in targetTrackedTeams.Where(team => team.Id == 0 && !existingPendingTeams.Contains(team)))
        {
            product.ProductTeamLinks.Add(new ProductTeamLinkEntity
            {
                Product = product,
                Team = team
            });
        }
    }

    private static void ReconcileRepositories(
        ProductEntity product,
        IReadOnlyCollection<string> repositoryNames,
        DateTimeOffset now)
    {
        var targetRepositoryNames = repositoryNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingRepositoryNames = product.Repositories
            .Select(repository => repository.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var repository in product.Repositories
                     .Where(repository => !targetRepositoryNames.Contains(repository.Name))
                     .ToList())
        {
            product.Repositories.Remove(repository);
        }

        foreach (var repositoryName in targetRepositoryNames.Where(repositoryName => !existingRepositoryNames.Contains(repositoryName)))
        {
            product.Repositories.Add(new RepositoryEntity
            {
                Product = product,
                Name = repositoryName,
                CreatedAt = now
            });
        }
    }

    private static void EnsureNoDuplicateEntities<TEntity>(
        IEnumerable<TEntity> entities,
        Func<TEntity, string> keySelector,
        string entityDescription,
        string propertyName)
    {
        var duplicateKey = entities
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(duplicateKey))
        {
            throw new InvalidOperationException(
                $"Mock configuration seeding cannot continue because duplicate {entityDescription} were found for {propertyName} '{duplicateKey}'.");
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

    private sealed record MockSeedPlan(
        IReadOnlyList<MockTeamPlan> Teams,
        IReadOnlyList<MockProfilePlan> Profiles,
        string ActiveProfileName);

    private sealed record MockTeamPlan(
        string Name,
        string AreaPath,
        int DefaultPictureId,
        string TfsTeamId);

    private sealed record MockProfilePlan(
        string Name,
        int DefaultPictureId,
        IReadOnlyList<int> OwnedGoalIds,
        IReadOnlyList<MockProductPlan> Products);

    private sealed record MockProductSeed(
        string Name,
        int[] GoalIndexes,
        int DefaultPictureId);

    private sealed record MockProductPlan(
        string Name,
        int DefaultPictureId,
        int Order,
        IReadOnlyList<int> RootIds,
        IReadOnlyList<string> TeamAreaPaths,
        IReadOnlyList<string> RepositoryNames);

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
