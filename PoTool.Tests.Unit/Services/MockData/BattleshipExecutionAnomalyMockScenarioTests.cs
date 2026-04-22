using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PoTool.Api.Configuration;
using PoTool.Api.Persistence;
using PoTool.Api.Repositories;
using PoTool.Api.Services;
using PoTool.Api.Services.MockData;
using PoTool.Api.Services.Onboarding;
using PoTool.Api.Services.Sync;
using PoTool.Client.Models;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.ExecutionRealityCheck;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.WorkItems;
using PoTool.Shared.Onboarding;
using PoTool.Shared.Planning;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services.MockData;

[TestClass]
public sealed class BattleshipExecutionAnomalyMockScenarioTests
{
    private const string TargetProductName = "Incident Response Control";
    private const string TargetTeamName = "Emergency Protocols";

    [TestMethod]
    public async Task BattleshipMockScenario_ProducesExecutionAnomaliesAndSinglePlanningBoardHint()
    {
        await using var provider = await CreateSqliteServiceProviderAsync();
        var seedService = provider.GetRequiredService<MockConfigurationSeedHostedService>();
        await seedService.StartAsync(CancellationToken.None);

        await RunSyncStagesAsync(provider);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productOwnerId = await context.Settings
            .OrderByDescending(setting => setting.Id)
            .Select(setting => setting.ActiveProfileId)
            .SingleAsync();
        Assert.IsNotNull(productOwnerId, "Expected mock seeding to select an active profile.");

        var product = await context.Products
            .Include(entity => entity.ProductTeamLinks)
                .ThenInclude(link => link.Team)
            .SingleAsync(entity => entity.Name == TargetProductName);
        var team = product.ProductTeamLinks
            .Select(link => link.Team)
            .Single(team => string.Equals(team.Name, TargetTeamName, StringComparison.OrdinalIgnoreCase));
        var sprint11 = await context.Sprints
            .SingleAsync(sprint => sprint.TeamId == team.Id && sprint.Name == "Sprint 11");

        Assert.AreEqual("current", sprint11.TimeFrame, "Sprint 11 should remain the current sprint for the selected team.");
        Assert.AreEqual(12, await context.Sprints.CountAsync(sprint => sprint.TeamId == team.Id), "The target team should receive the full historical-plus-current Battleship sprint set.");

        var cdcService = CreateCdcSliceService(context);
        var sliceResult = await cdcService.BuildAsync(productOwnerId.Value, sprint11.Id, [product.Id], CancellationToken.None);

        Assert.IsTrue(sliceResult.HasSufficientEvidence, "The extended Battleship mock data should now satisfy the 8-sprint CDC evidence window.");
        Assert.IsNotNull(sliceResult.Slice);

        var completionSeries = sliceResult.Slice.WindowRows.Select(row => row.CommitmentCompletion).ToArray();
        var completionMedian = sliceResult.Slice.Baselines
            .Single(baseline => baseline.MetricKey == ExecutionRealityCheckCdcKeys.CommitmentCompletionMetricKey)
            .Median;

        Assert.HasCount(ExecutionRealityCheckCdcSliceProjector.RequiredWindowSize, completionSeries);
        Assert.IsTrue(completionSeries.Take(5).Any(value => value > completionMedian),
            "The seeded window should still contain earlier stronger completion sprints for variability context.");
        Assert.IsTrue(completionSeries.Skip(5).All(value => value < completionMedian),
            "The last three sprints should sit below the typical commitment-completion median.");

        var interpretation = new ExecutionRealityCheckInterpretationService().Interpret(sliceResult);
        Assert.AreEqual(ExecutionRealityCheckOverallState.Investigate, interpretation.OverallState);
        Assert.IsGreaterThanOrEqualTo(2, interpretation.TotalSeverity,
            "The seeded Battleship anomaly window should escalate the execution interpretation above a single watch-level signal.");
        Assert.IsTrue(
            GetAnomaly(interpretation, ExecutionRealityCheckCdcKeys.CompletionVariabilityAnomalyKey).Status
                is ExecutionRealityCheckAnomalyStatus.Weak or ExecutionRealityCheckAnomalyStatus.Strong,
            "Completion variability should be active for the seeded Battleship anomaly window.");
        Assert.IsTrue(
            interpretation.Anomalies.Any(anomaly => anomaly.Status != ExecutionRealityCheckAnomalyStatus.Inactive),
            "At least one execution anomaly should remain active after the deterministic mock extension is ingested.");

        var interpretationLayer = new ExecutionRealityCheckInterpretationLayerService(
            cdcService,
            new ExecutionRealityCheckInterpretationService());
        var hintService = new ProductPlanningBoardExecutionHintService(
            new ProductRepository(context),
            new SprintRepository(context),
            interpretationLayer);

        var board = new ProductPlanningBoardDto(
            product.Id,
            product.Name,
            [],
            [],
            [],
            [],
            []);
        var hintedBoard = await hintService.ApplyExecutionHintAsync(board, team.Id, sprint11.Id, CancellationToken.None);

        Assert.IsNotNull(hintedBoard.ExecutionHint, "The planning board should surface a single execution hint once the seeded anomaly window is present.");
        Assert.IsTrue(
            hintedBoard.ExecutionHint.AnomalyKey is ExecutionRealityCheckCdcKeys.CompletionBelowTypicalAnomalyKey
                or ExecutionRealityCheckCdcKeys.CompletionVariabilityAnomalyKey
                or ExecutionRealityCheckCdcKeys.SpilloverIncreaseAnomalyKey,
            "The surfaced planning-board hint should carry one of the supported execution anomaly keys.");
        Assert.IsTrue(
            ProductPlanningExecutionHintNavigation.ResolveRoute(hintedBoard.ExecutionHint) is WorkspaceRoutes.SprintExecution or WorkspaceRoutes.DeliveryTrends,
            "The surfaced planning-board hint should resolve to one of the supported execution destinations.");
    }

    private static async Task RunSyncStagesAsync(ServiceProvider provider)
    {
        await using var setupScope = provider.CreateAsyncScope();
        var context = setupScope.ServiceProvider.GetRequiredService<PoToolDbContext>();

        var productOwnerId = await context.Settings
            .OrderByDescending(setting => setting.Id)
            .Select(setting => setting.ActiveProfileId)
            .SingleAsync();
        Assert.IsNotNull(productOwnerId, "Expected the mock seed to persist an active profile before sync.");

        var productIds = await context.Products
            .Where(product => product.ProductOwnerId == productOwnerId)
            .Select(product => product.Id)
            .ToArrayAsync();
        var rootIds = await context.ProductBacklogRoots
            .Where(root => productIds.Contains(root.ProductId))
            .Select(root => root.WorkItemTfsId)
            .ToArrayAsync();
        var repositoryNames = await context.Repositories
            .Where(repository => productIds.Contains(repository.ProductId))
            .Select(repository => repository.Name)
            .ToArrayAsync();

        var syncContext = new SyncContext
        {
            ProductOwnerId = productOwnerId.Value,
            RootWorkItemIds = rootIds,
            RepositoryNames = repositoryNames,
            PipelineDefinitionIds = []
        };

        var stages = new ISyncStage[]
        {
            setupScope.ServiceProvider.GetRequiredService<TeamSprintSyncStage>(),
            setupScope.ServiceProvider.GetRequiredService<WorkItemSyncStage>(),
            setupScope.ServiceProvider.GetRequiredService<ActivityIngestionSyncStage>(),
            setupScope.ServiceProvider.GetRequiredService<WorkItemRelationshipSnapshotStage>(),
            setupScope.ServiceProvider.GetRequiredService<WorkItemResolutionSyncStage>()
        };

        foreach (var stage in stages)
        {
            var result = await stage.ExecuteAsync(syncContext, _ => { }, CancellationToken.None);
            Assert.IsTrue(result.Success, $"Expected stage '{stage.StageName}' to succeed for the Battleship mock anomaly scenario.");
        }
    }

    private static ExecutionRealityCheckCdcSliceService CreateCdcSliceService(PoToolDbContext context)
    {
        var sprintSpilloverService = new SprintSpilloverService();
        return new ExecutionRealityCheckCdcSliceService(
            context,
            NullLogger<ExecutionRealityCheckCdcSliceService>.Instance,
            stateClassificationService: null,
            sprintSpilloverService,
            new SprintFactService(
                new SprintCommitmentService(),
                new SprintScopeChangeService(),
                new SprintCompletionService(),
                sprintSpilloverService,
                new CanonicalStoryPointResolutionService()),
            new PoTool.Core.Domain.Cdc.Sprints.SprintExecutionMetricsCalculator(),
            new ExecutionRealityCheckCdcSliceProjector(),
            new FixedTimeProvider(DateTimeOffset.UtcNow.AddHours(1)));
    }

    private static ExecutionRealityCheckAnomalyInterpretation GetAnomaly(
        ExecutionRealityCheckInterpretation interpretation,
        string anomalyKey)
    {
        return interpretation.Anomalies.Single(anomaly => anomaly.AnomalyKey == anomalyKey);
    }

    private static async Task<ServiceProvider> CreateSqliteServiceProviderAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var incrementalPlanner = new Mock<IIncrementalSyncPlanner>();
        incrementalPlanner
            .Setup(planner => planner.Plan(It.IsAny<IncrementalSyncPlannerRequest>()))
            .Returns<IncrementalSyncPlannerRequest>(request => new IncrementalSyncPlan
            {
                PlanningMode = IncrementalSyncPlanningMode.Full,
                AnalyticalScopeIds = request.CurrentAnalyticalScopeIds,
                ClosureScopeIds = request.CurrentClosureScopeIds,
                RequiresRelationshipSnapshotRebuild = true,
                RequiresResolutionRebuild = true,
                RequiresProjectionRefresh = true
            });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PoToolDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton(new TfsRuntimeMode(useMockClient: true));
        services.AddSingleton<IOptions<OnboardingVerificationOptions>>(Options.Create(new OnboardingVerificationOptions
        {
            SelectedScenario = OnboardingVerificationScenarioNames.HappyBindingChain
        }));
        services.AddSingleton<IOnboardingVerificationScenarioService, OnboardingVerificationScenarioService>();
        services.AddSingleton<BattleshipWorkItemGenerator>();
        services.AddSingleton<BattleshipDependencyGenerator>();
        services.AddSingleton<BattleshipPullRequestGenerator>();
        services.AddSingleton<BattleshipPipelineGenerator>();
        services.AddSingleton<MockDataValidator>();
        services.AddSingleton<BattleshipMockDataFacade>();
        services.AddSingleton<MockConfigurationSeedHostedService>();
        services.AddScoped<ITfsClient, MockTfsClient>();
        services.AddScoped(_ => incrementalPlanner.Object);
        services.Configure<ActivityIngestionOptions>(options => options.ActivityBackfillDays = 365);
        services.AddScoped<ActivityEventIngestionService>();
        services.AddScoped<ISprintRepository, SprintRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<WorkItemSyncStage>();
        services.AddScoped<ActivityIngestionSyncStage>();
        services.AddScoped<TeamSprintSyncStage>();
        services.AddSingleton<WorkItemRelationshipSnapshotService>();
        services.AddScoped<WorkItemRelationshipSnapshotStage>();
        services.AddSingleton<WorkItemResolutionService>();
        services.AddScoped<WorkItemResolutionSyncStage>();

        var provider = services.BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PoToolDbContext>();
        await context.Database.EnsureCreatedAsync();

        return provider;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
