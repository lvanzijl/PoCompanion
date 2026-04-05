using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Api.Persistence.Entities;
using PoTool.Api.Persistence.Entities.UiRoadmap;
using PoTool.Core.Domain.DeliveryTrends.Models;

namespace PoTool.Tests.Unit.Persistence;

[TestClass]
public sealed class PersistenceRelationshipContractTests
{
    private static readonly IReadOnlyDictionary<string, string[]> RequiredForeignKeyCoverageManifest =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["CachedMetricsEntity:ProductOwnerId->ProfileEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["CachedPipelineRunEntity:PipelineDefinitionId->PipelineDefinitionEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["CachedPipelineRunEntity:ProductOwnerId->ProfileEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["CoverageEntity:BuildId->CachedPipelineRunEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["EpicPlacementEntity:LaneId->LaneEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["OnboardingSnapshotMetadata:PipelineSnapshotPipelineSourceId->PipelineSnapshot"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["OnboardingSnapshotMetadata:ProductRootSnapshotProductRootId->ProductRootSnapshot"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["OnboardingSnapshotMetadata:ProjectSnapshotProjectSourceId->ProjectSnapshot"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["OnboardingSnapshotMetadata:TeamSnapshotTeamSourceId->TeamSnapshot"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["OnboardingValidationState:PipelineSourceId->PipelineSource"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["OnboardingValidationState:ProductRootId->ProductRoot"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["OnboardingValidationState:ProductSourceBindingId->ProductSourceBinding"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["OnboardingValidationState:ProjectSourceId->ProjectSource"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["OnboardingValidationState:TeamSourceId->TeamSource"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["OnboardingValidationState:TfsConnectionId->TfsConnection"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["PipelineDefinitionEntity:ProductId->ProductEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["PipelineDefinitionEntity:RepositoryId->RepositoryEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["PipelineSnapshot:PipelineSourceId->PipelineSource"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["PipelineSource:ProjectSourceId->ProjectSource"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["PlanningBoardSettingsEntity:ProductOwnerId->ProfileEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["PlanningEpicPlacementEntity:ProductId->ProductEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["PlanningEpicPlacementEntity:RowId->BoardRowEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["PortfolioFlowProjectionEntity:ProductId->ProductEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["PortfolioFlowProjectionEntity:SprintId->SprintEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["PortfolioSnapshotEntity:ProductId->ProductEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["PortfolioSnapshotItemEntity:SnapshotId->PortfolioSnapshotEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["ProductBacklogRootEntity:ProductId->ProductEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["ProductEntity:ProjectId->ProjectEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["ProductOwnerCacheStateEntity:ProductOwnerId->ProfileEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["ProductRoot:ProjectSourceId->ProjectSource"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["ProductRootSnapshot:ProductRootId->ProductRoot"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["ProductSourceBinding:ProductRootId->ProductRoot"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["ProductSourceBinding:ProjectSourceId->ProjectSource"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["ProductTeamLinkEntity:ProductId->ProductEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["ProductTeamLinkEntity:TeamId->TeamEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["ProjectSnapshot:ProjectSourceId->ProjectSource"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["ProjectSource:TfsConnectionId->TfsConnection"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["RepositoryEntity:ProductId->ProductEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["RoadmapSnapshotItemEntity:SnapshotId->RoadmapSnapshotEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["SprintEntity:TeamId->TeamEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["SprintMetricsProjectionEntity:ProductId->ProductEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["SprintMetricsProjectionEntity:SprintId->SprintEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)],
            ["TeamSnapshot:TeamSourceId->TeamSource"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds)],
            ["TeamSource:ProjectSourceId->ProjectSource"] = [nameof(OnboardingPersistenceFoundationTests.InsertValidOnboardingGraph_Succeeds), nameof(OnboardingPersistenceFoundationTests.MissingRequiredParentReference_IsRejected)],
            ["TestRunEntity:BuildId->CachedPipelineRunEntity"] = [nameof(SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships)]
        };

    [TestMethod]
    public void SaveChanges_WhenRequiredForeignKeyScalarIsMissing_ThrowsBeforeDatabaseCommit()
    {
        using var context = CreateSqliteContext();
        context.Repositories.Add(new RepositoryEntity
        {
            Name = "orphan-repository",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => context.SaveChanges());

        StringAssert.Contains(ex.Message, "RepositoryEntity");
        StringAssert.Contains(ex.Message, "ProductId");
        StringAssert.Contains(ex.Message, "ProductEntity");
    }

    [TestMethod]
    public async Task SaveChangesAsync_WhenRequiredForeignKeyReferencesMissingPrincipal_ThrowsBeforeDatabaseCommit()
    {
        await using var context = CreateSqliteContext();
        context.Repositories.Add(new RepositoryEntity
        {
            ProductId = 999,
            Name = "dangling-repository",
            CreatedAt = DateTimeOffset.UtcNow
        });

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => context.SaveChangesAsync());

        StringAssert.Contains(ex.Message, "RepositoryEntity");
        StringAssert.Contains(ex.Message, "missing parent 'ProductEntity'");
        StringAssert.Contains(ex.Message, "ProductId");
    }

    [TestMethod]
    public async Task SaveChangesAsync_WhenTrackedParentGraphIsValid_PersistsCoverageGraphForRequiredRelationships()
    {
        await using var context = CreateSqliteContext();
        var now = DateTimeOffset.UtcNow;

        var profile = new ProfileEntity
        {
            Name = "Persistence Owner",
            GoalIds = string.Empty,
            CreatedAt = now,
            LastModified = now
        };

        var project = new ProjectEntity
        {
            Id = "persistence-project",
            Alias = "persistence-project",
            Name = "Persistence Project"
        };

        var team = new TeamEntity
        {
            Name = "Persistence Team",
            TeamAreaPath = "Persistence\\Area",
            CreatedAt = now,
            LastModified = now
        };

        var sprint = new SprintEntity
        {
            Team = team,
            Path = "\\Persistence\\Sprint 1",
            Name = "Sprint 1",
            LastSyncedUtc = now,
            LastSyncedDateUtc = now.UtcDateTime
        };

        var product = new ProductEntity
        {
            Name = "Persistence Product",
            ProductOwner = profile,
            Project = project,
            CreatedAt = now,
            LastModified = now
        };

        var backlogRoot = new ProductBacklogRootEntity
        {
            Product = product,
            WorkItemTfsId = 42
        };

        var productTeamLink = new ProductTeamLinkEntity
        {
            Product = product,
            Team = team
        };

        var repository = new RepositoryEntity
        {
            Product = product,
            Name = "persistence-repository",
            CreatedAt = now
        };

        var pipelineDefinition = new PipelineDefinitionEntity
        {
            Product = product,
            Repository = repository,
            PipelineDefinitionId = 123,
            RepoId = "11111111-1111-1111-1111-111111111111",
            RepoName = "persistence-repository",
            Name = "Persistence Pipeline",
            LastSyncedUtc = now
        };

        var productOwnerCacheState = new ProductOwnerCacheStateEntity
        {
            ProductOwner = profile,
            SyncStatus = CacheSyncStatus.Idle
        };

        var cachedMetric = new CachedMetricsEntity
        {
            ProductOwner = profile,
            MetricName = "Velocity",
            MetricValue = 5m,
            ComputedAt = now
        };

        var planningBoardSettings = new PlanningBoardSettingsEntity
        {
            ProductOwner = profile,
            LastModified = now
        };

        var boardRow = new BoardRowEntity
        {
            DisplayOrder = 0,
            CreatedAt = now,
            LastModified = now
        };

        var planningEpicPlacement = new PlanningEpicPlacementEntity
        {
            Product = product,
            Row = boardRow,
            EpicId = 1001,
            OrderInCell = 0,
            CreatedAt = now,
            LastModified = now
        };

        var lane = new LaneEntity
        {
            ObjectiveId = 2001,
            DisplayOrder = 0
        };

        var epicPlacement = new EpicPlacementEntity
        {
            Lane = lane,
            EpicId = 2002,
            RowIndex = 0,
            OrderInRow = 0
        };

        var cachedPipelineRun = new CachedPipelineRunEntity
        {
            ProductOwner = profile,
            PipelineDefinition = pipelineDefinition,
            TfsRunId = 3001,
            CachedAt = now
        };

        var testRun = new TestRunEntity
        {
            Build = cachedPipelineRun,
            TotalTests = 10,
            PassedTests = 9,
            NotApplicableTests = 0,
            CachedAt = now
        };

        var coverage = new CoverageEntity
        {
            Build = cachedPipelineRun,
            CoveredLines = 80,
            TotalLines = 100,
            CachedAt = now
        };

        var portfolioFlowProjection = new PortfolioFlowProjectionEntity
        {
            Product = product,
            Sprint = sprint,
            StockStoryPoints = 10,
            RemainingScopeStoryPoints = 8,
            InflowStoryPoints = 2,
            ThroughputStoryPoints = 1,
            ProjectionTimestamp = now
        };

        var portfolioSnapshot = new PortfolioSnapshotEntity
        {
            Product = product,
            TimestampUtc = now.UtcDateTime,
            Source = "Persistence contract"
        };
        portfolioSnapshot.Items.Add(new PortfolioSnapshotItemEntity
        {
            Snapshot = portfolioSnapshot,
            ProjectNumber = "PN-1",
            Progress = 0.5d,
            TotalWeight = 12d,
            LifecycleState = WorkPackageLifecycleState.Active
        });

        var roadmapSnapshot = new RoadmapSnapshotEntity
        {
            CreatedAtUtc = now.UtcDateTime
        };
        roadmapSnapshot.Items.Add(new RoadmapSnapshotItemEntity
        {
            Snapshot = roadmapSnapshot,
            ProductName = "Persistence Product",
            EpicTfsId = 4001,
            EpicTitle = "Epic",
            EpicOrder = 1
        });

        var sprintMetricsProjection = new SprintMetricsProjectionEntity
        {
            Product = product,
            Sprint = sprint,
            LastComputedAt = now,
            IncludedUpToRevisionId = 1
        };

        context.AddRange(
            profile,
            project,
            team,
            sprint,
            product,
            backlogRoot,
            productTeamLink,
            repository,
            pipelineDefinition,
            productOwnerCacheState,
            cachedMetric,
            planningBoardSettings,
            boardRow,
            planningEpicPlacement,
            lane,
            epicPlacement,
            cachedPipelineRun,
            testRun,
            coverage,
            portfolioFlowProjection,
            portfolioSnapshot,
            roadmapSnapshot,
            sprintMetricsProjection);

        await context.SaveChangesAsync();

        Assert.AreEqual(project.Id, product.ProjectId);
        Assert.AreEqual(product.Id, repository.ProductId);
        Assert.AreEqual(product.Id, backlogRoot.ProductId);
        Assert.AreEqual(team.Id, productTeamLink.TeamId);
        Assert.AreEqual(product.Id, productTeamLink.ProductId);
        Assert.AreEqual(repository.Id, pipelineDefinition.RepositoryId);
        Assert.AreEqual(product.Id, pipelineDefinition.ProductId);
        Assert.AreEqual(profile.Id, cachedMetric.ProductOwnerId);
        Assert.AreEqual(profile.Id, productOwnerCacheState.ProductOwnerId);
        Assert.AreEqual(profile.Id, planningBoardSettings.ProductOwnerId);
        Assert.AreEqual(profile.Id, cachedPipelineRun.ProductOwnerId);
        Assert.AreEqual(pipelineDefinition.Id, cachedPipelineRun.PipelineDefinitionId);
        Assert.AreEqual(cachedPipelineRun.Id, testRun.BuildId);
        Assert.AreEqual(cachedPipelineRun.Id, coverage.BuildId);
        Assert.AreEqual(sprint.Id, portfolioFlowProjection.SprintId);
        Assert.AreEqual(product.Id, portfolioFlowProjection.ProductId);
        Assert.AreEqual(product.Id, portfolioSnapshot.ProductId);
        Assert.AreEqual(portfolioSnapshot.SnapshotId, portfolioSnapshot.Items.Single().SnapshotId);
        Assert.AreEqual(roadmapSnapshot.Id, roadmapSnapshot.Items.Single().SnapshotId);
        Assert.AreEqual(sprint.Id, sprintMetricsProjection.SprintId);
        Assert.AreEqual(product.Id, sprintMetricsProjection.ProductId);
    }

    [TestMethod]
    public async Task RequiredForeignKeyCoverageManifest_MatchesAllRequiredModelRelationships()
    {
        await using var context = CreateSqliteContext();

        var requiredRelationships = context.Model.GetEntityTypes()
            .OrderBy(entityType => entityType.ClrType.Name, StringComparer.Ordinal)
            .SelectMany(entityType => entityType.GetForeignKeys()
                .Where(foreignKey => foreignKey.Properties.All(property => !property.IsNullable))
                .Select(foreignKey => BuildRelationshipSignature(entityType, foreignKey)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEquivalent(
            requiredRelationships,
            RequiredForeignKeyCoverageManifest.Keys.ToArray(),
            "Every required FK in the EF model must be mapped to at least one persistence/seeding coverage test.");

        var testMethods = typeof(PersistenceRelationshipContractTests).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(PersistenceRelationshipContractTests).Namespace)
            .SelectMany(type => type.GetMethods())
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (relationship, coveredBy) in RequiredForeignKeyCoverageManifest)
        {
            Assert.IsNotEmpty(coveredBy, $"Coverage manifest entry '{relationship}' must reference at least one test.");

            foreach (var testName in coveredBy)
            {
                if (!testMethods.Contains(testName))
                {
                    Assert.Fail($"Coverage manifest entry '{relationship}' references unknown test '{testName}'.");
                }
            }
        }
    }

    private static PoToolDbContext CreateSqliteContext()
    {
        var options = new DbContextOptionsBuilder<PoToolDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var context = new PoToolDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static string BuildRelationshipSignature(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType,
        Microsoft.EntityFrameworkCore.Metadata.IForeignKey foreignKey)
        => $"{entityType.ClrType.Name}:{string.Join(",", foreignKey.Properties.Select(property => property.Name))}->{foreignKey.PrincipalEntityType.ClrType.Name}";
}
