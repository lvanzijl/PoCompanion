using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Persistence.Entities.Onboarding;
using PoTool.Api.Repositories;
using PoTool.Api.Services.Onboarding;
using PoTool.Core.Planning;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Core.WorkItems;
using PoTool.Shared.Onboarding;
using PoTool.Shared.Settings;
using PoTool.Shared.WorkItems;

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

    private static readonly (string Name, int DisplayOrder)[] MockTriageTags =
    [
        ("Needs Investigation", 0),
        ("Regression", 1),
        ("Customer Reported", 2),
        ("Operational Risk", 3),
        ("Hotfix Candidate", 4),
        ("Needs Repro", 5)
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MockConfigurationSeedHostedService> _logger;
    private readonly IOnboardingVerificationScenarioService _verificationScenarioService;

    public MockConfigurationSeedHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<MockConfigurationSeedHostedService> logger,
        IOnboardingVerificationScenarioService verificationScenarioService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _verificationScenarioService = verificationScenarioService;
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

        await EnsureMockSprintsAsync(context, teamsByAreaPath.Values, now, cancellationToken);
        await EnsureMockPlanningBoardDataAsync(context, hierarchy, mockDataFacade, now, cancellationToken);
        await EnsureMockTfsConfigurationAsync(context, now, cancellationToken);
        await EnsureMockTriageTagsAsync(context, now, cancellationToken);
        await EnsureActiveProfileAsync(context, seedPlan.ActiveProfileName, cancellationToken);
        await EnsureMockPortfolioSnapshotsAsync(context, seedPlan.ActiveProfileName, hierarchy, cancellationToken);
        await EnsureOnboardingVerificationScenarioAsync(context, cancellationToken);
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
                HasTestedConnectionSuccessfully = true,
                HasVerifiedTfsApiSuccessfully = true,
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
            existingConfig.HasTestedConnectionSuccessfully = true;
            existingConfig.HasVerifiedTfsApiSuccessfully = true;
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

    private async Task EnsureMockTriageTagsAsync(
        PoToolDbContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tagNames = MockTriageTags
            .Select(static tag => tag.Name)
            .ToArray();

        var existingTags = await context.TriageTags
            .Where(tag => tagNames.Contains(tag.Name))
            .ToListAsync(cancellationToken);

        foreach (var (name, displayOrder) in MockTriageTags)
        {
            var tag = existingTags.FirstOrDefault(
                existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase));

            if (tag is null)
            {
                context.TriageTags.Add(new TriageTagEntity
                {
                    Name = name,
                    IsEnabled = true,
                    DisplayOrder = displayOrder,
                    CreatedAt = now
                });
                continue;
            }

            tag.IsEnabled = true;
            tag.DisplayOrder = displayOrder;
        }

        await SaveChangesWithDiagnosticsAsync(
            context,
            cancellationToken,
            "persisting mock triage tags");
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

    private async Task EnsureMockSprintsAsync(
        PoToolDbContext context,
        IEnumerable<TeamEntity> teams,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sprintRepository = new SprintRepository(context);
        var iterations = BattleshipSprintSeedCatalog.CreateTeamIterations(MockProjectName, now);

        foreach (var team in teams.OrderBy(static team => team.Id))
        {
            await sprintRepository.UpsertSprintsForTeamAsync(team.Id, iterations, cancellationToken);
        }
    }

    private async Task EnsureMockPlanningBoardDataAsync(
        PoToolDbContext context,
        IReadOnlyCollection<WorkItemDto> hierarchy,
        BattleshipMockDataFacade mockDataFacade,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(hierarchy);
        ArgumentNullException.ThrowIfNull(mockDataFacade);

        var sprintByNumber = BattleshipSprintSeedCatalog.CreateTeamIterations(MockProjectName, now)
            .ToDictionary(ParseSprintNumber);
        var targetProductNames = new[]
        {
            BattleshipPlanningBoardSeedCatalog.PrimaryProductName,
            BattleshipPlanningBoardSeedCatalog.SecondaryProductName
        };

        var targetProducts = await context.Products
            .Include(product => product.BacklogRoots)
            .Where(product => targetProductNames.Contains(product.Name))
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);

        if (targetProducts.Count != targetProductNames.Length)
        {
            throw new InvalidOperationException(
                "Mock planning-board seeding requires both Battleship planning products to be present.");
        }

        var intentStore = new ProductPlanningIntentStore(context);
        foreach (var product in targetProducts)
        {
            var productRoots = product.BacklogRoots
                .Select(static root => root.WorkItemTfsId)
                .OrderBy(static id => id)
                .ToArray();
            var epicSeeds = BattleshipPlanningBoardSeedCatalog.CreateProductSeeds(product.Name, productRoots, hierarchy);
            var intents = epicSeeds
                .Select(seed => MapToPlanningIntent(product.Id, seed, sprintByNumber, now))
                .ToArray();

            await intentStore.UpsertForProductAsync(product.Id, intents, cancellationToken);
            await intentStore.DeleteMissingEpicsAsync(
                product.Id,
                epicSeeds.Select(static seed => seed.EpicId).ToArray(),
                cancellationToken);

            foreach (var seed in epicSeeds)
            {
                var targetDate = ResolveTargetDate(seed, sprintByNumber);
                var updated = await mockDataFacade.UpdateWorkItemPlanningDatesAsync(
                    seed.EpicId,
                    DateOnly.FromDateTime(sprintByNumber[seed.StartSprintNumber].StartDate!.Value.UtcDateTime),
                    targetDate,
                    cancellationToken);

                if (!updated)
                {
                    throw new InvalidOperationException(
                        $"Mock planning-board seeding could not align Battleship epic '{seed.EpicId}' with its deterministic planning dates.");
                }
            }
        }
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

    private async Task EnsureOnboardingVerificationScenarioAsync(
        PoToolDbContext context,
        CancellationToken cancellationToken)
    {
        if (!_verificationScenarioService.IsEnabled || _verificationScenarioService.CurrentScenario is null)
        {
            return;
        }

        var scenario = _verificationScenarioService.CurrentScenario;
        await ResetOnboardingVerificationGraphAsync(context, cancellationToken);

        if (!scenario.Seed.IncludeConnection)
        {
            return;
        }

        var connection = new TfsConnection
        {
            ConnectionKey = "connection",
            OrganizationUrl = MockOrganizationUrl,
            AuthenticationMode = "Ntlm",
            TimeoutSeconds = 30,
            ApiVersion = "7.0",
            AvailabilityValidationState = CreateValidValidationState(),
            PermissionValidationState = CreateValidValidationState(),
            CapabilityValidationState = CreateValidValidationState(),
            LastAttemptedValidationAtUtc = DateTime.UtcNow,
            LastSuccessfulValidationAtUtc = DateTime.UtcNow
        };
        context.OnboardingTfsConnections.Add(connection);
        await SaveChangesWithDiagnosticsAsync(context, cancellationToken, "persisting onboarding verification connection");

        var lookupProjects = scenario.Lookup.Projects.ToDictionary(project => project.ProjectExternalId, StringComparer.OrdinalIgnoreCase);
        var lookupTeams = scenario.Lookup.Teams.ToDictionary(team => team.TeamExternalId, StringComparer.OrdinalIgnoreCase);
        var lookupPipelines = scenario.Lookup.Pipelines.ToDictionary(pipeline => pipeline.PipelineExternalId, StringComparer.OrdinalIgnoreCase);
        var lookupRoots = scenario.Lookup.WorkItems.ToDictionary(workItem => workItem.WorkItemExternalId, StringComparer.OrdinalIgnoreCase);

        var projectsByExternalId = new Dictionary<string, ProjectSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var projectExternalId in scenario.Seed.ProjectExternalIds)
        {
            if (!lookupProjects.TryGetValue(projectExternalId, out var lookupProject))
            {
                throw new InvalidOperationException(
                    $"Onboarding verification scenario '{scenario.Name}' cannot seed project '{projectExternalId}' because no lookup fixture exists for it.");
            }

            var project = new ProjectSource
            {
                TfsConnectionId = connection.Id,
                ProjectExternalId = lookupProject.ProjectExternalId,
                Enabled = true,
                Snapshot = new ProjectSnapshot
                {
                    ProjectExternalId = lookupProject.ProjectExternalId,
                    Name = lookupProject.Name,
                    Description = lookupProject.Description,
                    Metadata = CreateSnapshotMetadata()
                },
                ValidationState = CreateValidValidationState()
            };

            context.OnboardingProjectSources.Add(project);
            projectsByExternalId.Add(projectExternalId, project);
        }

        await SaveChangesWithDiagnosticsAsync(context, cancellationToken, "persisting onboarding verification projects");

        var teamsByExternalId = new Dictionary<string, TeamSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var teamExternalId in scenario.Seed.TeamExternalIds.Concat(scenario.Seed.InvalidTeamExternalIds).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!lookupTeams.TryGetValue(teamExternalId, out var lookupTeam))
            {
                throw new InvalidOperationException(
                    $"Onboarding verification scenario '{scenario.Name}' cannot seed team '{teamExternalId}' because no lookup fixture exists for it.");
            }

            if (!projectsByExternalId.TryGetValue(lookupTeam.ProjectExternalId, out var project))
            {
                throw new InvalidOperationException(
                    $"Onboarding verification scenario '{scenario.Name}' cannot seed team '{teamExternalId}' because project '{lookupTeam.ProjectExternalId}' is not seeded.");
            }

            var team = new TeamSource
            {
                ProjectSourceId = project.Id,
                TeamExternalId = lookupTeam.TeamExternalId,
                Enabled = true,
                Snapshot = new TeamSnapshot
                {
                    TeamExternalId = lookupTeam.TeamExternalId,
                    ProjectExternalId = lookupTeam.ProjectExternalId,
                    Name = lookupTeam.Name,
                    Description = lookupTeam.Description,
                    DefaultAreaPath = lookupTeam.DefaultAreaPath,
                    Metadata = CreateSnapshotMetadata()
                },
                ValidationState = scenario.Seed.InvalidTeamExternalIds.Contains(teamExternalId, StringComparer.OrdinalIgnoreCase)
                    ? CreateInvalidValidationState()
                    : CreateValidValidationState()
            };

            context.OnboardingTeamSources.Add(team);
            teamsByExternalId.Add(teamExternalId, team);
        }

        var pipelinesByExternalId = new Dictionary<string, PipelineSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var pipelineExternalId in scenario.Seed.PipelineExternalIds.Concat(scenario.Seed.InvalidPipelineExternalIds).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!lookupPipelines.TryGetValue(pipelineExternalId, out var lookupPipeline))
            {
                throw new InvalidOperationException(
                    $"Onboarding verification scenario '{scenario.Name}' cannot seed pipeline '{pipelineExternalId}' because no lookup fixture exists for it.");
            }

            if (!projectsByExternalId.TryGetValue(lookupPipeline.ProjectExternalId, out var project))
            {
                throw new InvalidOperationException(
                    $"Onboarding verification scenario '{scenario.Name}' cannot seed pipeline '{pipelineExternalId}' because project '{lookupPipeline.ProjectExternalId}' is not seeded.");
            }

            var pipeline = new PipelineSource
            {
                ProjectSourceId = project.Id,
                PipelineExternalId = lookupPipeline.PipelineExternalId,
                Enabled = true,
                Snapshot = new PipelineSnapshot
                {
                    PipelineExternalId = lookupPipeline.PipelineExternalId,
                    ProjectExternalId = lookupPipeline.ProjectExternalId,
                    Name = lookupPipeline.Name,
                    Folder = lookupPipeline.Folder,
                    YamlPath = lookupPipeline.YamlPath,
                    RepositoryExternalId = lookupPipeline.RepositoryExternalId,
                    RepositoryName = lookupPipeline.RepositoryName,
                    Metadata = CreateSnapshotMetadata()
                },
                ValidationState = scenario.Seed.InvalidPipelineExternalIds.Contains(pipelineExternalId, StringComparer.OrdinalIgnoreCase)
                    ? CreateInvalidValidationState()
                    : CreateValidValidationState()
            };

            context.OnboardingPipelineSources.Add(pipeline);
            pipelinesByExternalId.Add(pipelineExternalId, pipeline);
        }

        await SaveChangesWithDiagnosticsAsync(context, cancellationToken, "persisting onboarding verification assignments");

        var rootsByExternalId = new Dictionary<string, ProductRoot>(StringComparer.OrdinalIgnoreCase);
        foreach (var workItemExternalId in scenario.Seed.ProductRootExternalIds)
        {
            if (!lookupRoots.TryGetValue(workItemExternalId, out var lookupRoot))
            {
                throw new InvalidOperationException(
                    $"Onboarding verification scenario '{scenario.Name}' cannot seed product root '{workItemExternalId}' because no lookup fixture exists for it.");
            }

            if (!projectsByExternalId.TryGetValue(lookupRoot.ProjectExternalId, out var project))
            {
                throw new InvalidOperationException(
                    $"Onboarding verification scenario '{scenario.Name}' cannot seed product root '{workItemExternalId}' because project '{lookupRoot.ProjectExternalId}' is not seeded.");
            }

            var root = new ProductRoot
            {
                ProjectSourceId = project.Id,
                WorkItemExternalId = lookupRoot.WorkItemExternalId,
                Enabled = true,
                Snapshot = new ProductRootSnapshot
                {
                    WorkItemExternalId = lookupRoot.WorkItemExternalId,
                    Title = lookupRoot.Title,
                    WorkItemType = lookupRoot.WorkItemType,
                    State = lookupRoot.State,
                    ProjectExternalId = lookupRoot.ProjectExternalId,
                    AreaPath = lookupRoot.AreaPath,
                    Metadata = CreateSnapshotMetadata()
                },
                ValidationState = CreateValidValidationState()
            };

            context.OnboardingProductRoots.Add(root);
            rootsByExternalId.Add(workItemExternalId, root);
        }

        await SaveChangesWithDiagnosticsAsync(context, cancellationToken, "persisting onboarding verification product roots");

        foreach (var bindingSeed in scenario.Seed.Bindings)
        {
            if (!rootsByExternalId.TryGetValue(bindingSeed.ProductRootExternalId, out var root))
            {
                throw new InvalidOperationException(
                    $"Onboarding verification scenario '{scenario.Name}' cannot seed binding for product root '{bindingSeed.ProductRootExternalId}' because that root is not seeded.");
            }

            if (!projectsByExternalId.TryGetValue(root.Snapshot.ProjectExternalId, out var project))
            {
                throw new InvalidOperationException(
                    $"Onboarding verification scenario '{scenario.Name}' cannot seed binding for product root '{bindingSeed.ProductRootExternalId}' because its project scope is not seeded.");
            }

            context.OnboardingProductSourceBindings.Add(new ProductSourceBinding
            {
                ProductRootId = root.Id,
                ProjectSourceId = project.Id,
                TeamSourceId = bindingSeed.SourceType == OnboardingProductSourceTypeDto.Team
                    ? ResolveSeededSourceId(teamsByExternalId, bindingSeed.SourceExternalId, scenario.Name, "team")
                    : null,
                PipelineSourceId = bindingSeed.SourceType == OnboardingProductSourceTypeDto.Pipeline
                    ? ResolveSeededSourceId(pipelinesByExternalId, bindingSeed.SourceExternalId, scenario.Name, "pipeline")
                    : null,
                SourceType = bindingSeed.SourceType switch
                {
                    OnboardingProductSourceTypeDto.Project => ProductSourceType.Project,
                    OnboardingProductSourceTypeDto.Team => ProductSourceType.Team,
                    OnboardingProductSourceTypeDto.Pipeline => ProductSourceType.Pipeline,
                    _ => throw new InvalidOperationException($"Unsupported onboarding source type '{bindingSeed.SourceType}'.")
                },
                SourceExternalId = bindingSeed.SourceExternalId,
                Enabled = bindingSeed.Enabled,
                ValidationState = CreateValidValidationState()
            });
        }

        await SaveChangesWithDiagnosticsAsync(context, cancellationToken, "persisting onboarding verification bindings");
    }

    private static async Task ResetOnboardingVerificationGraphAsync(
        PoToolDbContext context,
        CancellationToken cancellationToken)
    {
        context.OnboardingProductSourceBindings.RemoveRange(
            await context.OnboardingProductSourceBindings.IgnoreQueryFilters().ToListAsync(cancellationToken));
        context.OnboardingProductRoots.RemoveRange(
            await context.OnboardingProductRoots.IgnoreQueryFilters().ToListAsync(cancellationToken));
        context.OnboardingPipelineSources.RemoveRange(
            await context.OnboardingPipelineSources.IgnoreQueryFilters().ToListAsync(cancellationToken));
        context.OnboardingTeamSources.RemoveRange(
            await context.OnboardingTeamSources.IgnoreQueryFilters().ToListAsync(cancellationToken));
        context.OnboardingProjectSources.RemoveRange(
            await context.OnboardingProjectSources.IgnoreQueryFilters().ToListAsync(cancellationToken));
        context.OnboardingTfsConnections.RemoveRange(
            await context.OnboardingTfsConnections.IgnoreQueryFilters().ToListAsync(cancellationToken));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static int ResolveSeededSourceId<TSource>(
        IReadOnlyDictionary<string, TSource> sourcesByExternalId,
        string sourceExternalId,
        string scenarioName,
        string sourceType)
        where TSource : OnboardingGraphEntityBase
    {
        if (!sourcesByExternalId.TryGetValue(sourceExternalId, out var source))
        {
            throw new InvalidOperationException(
                $"Onboarding verification scenario '{scenarioName}' cannot seed a {sourceType} binding because source '{sourceExternalId}' was not resolved.");
        }

        if (source.Id <= 0)
        {
            throw new InvalidOperationException(
                $"Onboarding verification scenario '{scenarioName}' cannot seed a {sourceType} binding because source '{sourceExternalId}' does not have a persisted ID yet.");
        }

        return source.Id;
    }

    private static OnboardingValidationState CreateValidValidationState()
        => new()
        {
            Status = OnboardingValidationStatus.Valid.ToString(),
            ValidatedAtUtc = DateTime.UtcNow
        };

    private static OnboardingValidationState CreateInvalidValidationState()
        => new()
        {
            Status = OnboardingValidationStatus.Invalid.ToString(),
            ValidatedAtUtc = DateTime.UtcNow
        };

    private static OnboardingSnapshotMetadata CreateSnapshotMetadata()
        => new()
        {
            ConfirmedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow,
            IsCurrent = true,
            RenameDetected = false
        };

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

    private static ProductPlanningIntentRecord MapToPlanningIntent(
        int productId,
        BattleshipPlanningBoardSeedCatalog.BattleshipPlanningEpicSeed seed,
        IReadOnlyDictionary<int, TeamIterationDto> sprintByNumber,
        DateTimeOffset updatedAtUtc)
    {
        if (!sprintByNumber.TryGetValue(seed.StartSprintNumber, out var startSprint) || startSprint.StartDate is null)
        {
            throw new InvalidOperationException(
                $"Mock planning-board seeding is missing a dated Battleship sprint for Sprint {seed.StartSprintNumber}.");
        }

        var finalSprintNumber = seed.StartSprintNumber + seed.DurationInSprints - 1;
        if (!sprintByNumber.TryGetValue(finalSprintNumber, out var finalSprint) || finalSprint.FinishDate is null)
        {
            throw new InvalidOperationException(
                $"Mock planning-board seeding cannot resolve Sprint {finalSprintNumber} for epic '{seed.EpicId}'.");
        }

        return new ProductPlanningIntentRecord(
            productId,
            seed.EpicId,
            startSprint.StartDate.Value.UtcDateTime.Date,
            seed.DurationInSprints,
            null,
            updatedAtUtc.UtcDateTime);
    }

    private static DateOnly ResolveTargetDate(
        BattleshipPlanningBoardSeedCatalog.BattleshipPlanningEpicSeed seed,
        IReadOnlyDictionary<int, TeamIterationDto> sprintByNumber)
    {
        var finalSprintNumber = seed.StartSprintNumber + seed.DurationInSprints - 1;
        if (!sprintByNumber.TryGetValue(finalSprintNumber, out var finalSprint) || finalSprint.FinishDate is null)
        {
            throw new InvalidOperationException(
                $"Mock planning-board seeding cannot resolve the final dated sprint for epic '{seed.EpicId}'.");
        }

        return DateOnly.FromDateTime(finalSprint.FinishDate.Value.UtcDateTime.AddDays(-1));
    }

    private static int ParseSprintNumber(TeamIterationDto iteration)
    {
        ArgumentNullException.ThrowIfNull(iteration);

        return int.TryParse(iteration.Name.Replace("Sprint ", string.Empty, StringComparison.OrdinalIgnoreCase), out var sprintNumber)
            ? sprintNumber
            : throw new InvalidOperationException($"Unable to parse Battleship sprint number from iteration '{iteration.Name}'.");
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
