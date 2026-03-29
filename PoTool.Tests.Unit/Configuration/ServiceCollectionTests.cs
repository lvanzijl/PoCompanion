using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Configuration;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.BacklogQuality;
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.EffortPlanning;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Metrics;
using PoTool.Core.Domain.Portfolio;
using PoTool.Integrations.Tfs.Clients;

namespace PoTool.Tests.Unit.Configuration;

/// <summary>
/// Tests to verify dependency injection registration is correct.
/// </summary>
[TestClass]
public class ServiceCollectionTests
{
    [TestMethod]
    public void AddPoToolApiServices_RegistersTfsConfigurationService_AsConcreteType()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        
        // Add required dependencies
        services.AddLogging();
        RegisterHostEnvironment(services);
        services.AddDbContext<PoToolDbContext>(options => 
            options.UseInMemoryDatabase("TestDb"));

        // Act - Register all services
        services.AddPoToolApiServices(configuration, isDevelopment: true);

        // Assert - Verify both concrete and interface registrations work
        using var serviceProvider = services.BuildServiceProvider();
        
        // Should be able to resolve concrete type
        var concreteService = serviceProvider.GetService<TfsConfigurationService>();
        Assert.IsNotNull(concreteService, "TfsConfigurationService concrete type should be resolvable");
        
        // Should be able to resolve interface type
        var interfaceService = serviceProvider.GetService<ITfsConfigurationService>();
        Assert.IsNotNull(interfaceService, "ITfsConfigurationService interface should be resolvable");
        
        // Both should resolve to the same instance (scoped lifetime)
        using var scope = serviceProvider.CreateScope();
        var concreteInScope = scope.ServiceProvider.GetRequiredService<TfsConfigurationService>();
        var interfaceInScope = scope.ServiceProvider.GetRequiredService<ITfsConfigurationService>();
        Assert.AreSame(concreteInScope, interfaceInScope, 
            "Concrete and interface should resolve to same instance within a scope");
    }

    [TestMethod]
    public void AddPoToolApiServices_RegistersWorkItemStateClassificationService_Successfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        
        // Add required dependencies
        services.AddLogging();
        RegisterHostEnvironment(services);
        services.AddDbContext<PoToolDbContext>(options => 
            options.UseInMemoryDatabase("TestDb2"));

        // Act - Register all services
        services.AddPoToolApiServices(configuration, isDevelopment: true);

        // Assert - Should be able to build service provider without DI exceptions
        using var serviceProvider = services.BuildServiceProvider();
        
        // Should be able to resolve WorkItemStateClassificationService which depends on TfsConfigurationService
        var stateClassificationService = serviceProvider.GetService<IWorkItemStateClassificationService>();
        Assert.IsNotNull(stateClassificationService, 
            "IWorkItemStateClassificationService should be resolvable (validates TfsConfigurationService dependency)");
    }

    [TestMethod]
    public void AddPoToolApiServices_RegistersCanonicalMetricsServices_ForDiConsumers()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddLogging();
        RegisterHostEnvironment(services);
        services.AddDbContext<PoToolDbContext>(options =>
            options.UseInMemoryDatabase("TestDb3"));

        services.AddPoToolApiServices(configuration, isDevelopment: true);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var storyPointResolutionService = scope.ServiceProvider.GetService<ICanonicalStoryPointResolutionService>();
        var hierarchyRollupService = scope.ServiceProvider.GetService<IHierarchyRollupService>();
        var epicProgressService = scope.ServiceProvider.GetService<IEpicProgressService>();
        var epicAggregationService = scope.ServiceProvider.GetService<IEpicAggregationService>();
        var productAggregationService = scope.ServiceProvider.GetService<IProductAggregationService>();
        var planningQualityService = scope.ServiceProvider.GetService<IPlanningQualityService>();
        var snapshotComparisonService = scope.ServiceProvider.GetService<ISnapshotComparisonService>();
        var portfolioSnapshotValidationService = scope.ServiceProvider.GetService<IPortfolioSnapshotValidationService>();
        var portfolioSnapshotComparisonService = scope.ServiceProvider.GetService<IPortfolioSnapshotComparisonService>();
        var portfolioSnapshotFactory = scope.ServiceProvider.GetService<IPortfolioSnapshotFactory>();
        var portfolioSnapshotPersistenceMapper = scope.ServiceProvider.GetService<IPortfolioSnapshotPersistenceMapper>();
        var portfolioSnapshotCaptureDataService = scope.ServiceProvider.GetService<IPortfolioSnapshotCaptureDataService>();
        var portfolioSnapshotPersistenceService = scope.ServiceProvider.GetService<IPortfolioSnapshotPersistenceService>();
        var portfolioSnapshotSelectionService = scope.ServiceProvider.GetService<IPortfolioSnapshotSelectionService>();
        var portfolioSnapshotCaptureOrchestrator = scope.ServiceProvider.GetService<IPortfolioSnapshotCaptureOrchestrator>();
        var portfolioReadModelStateService = scope.ServiceProvider.GetService<IPortfolioReadModelStateService>();
        var portfolioReadModelMapper = scope.ServiceProvider.GetService<IPortfolioReadModelMapper>();
        var portfolioTrendAnalysisService = scope.ServiceProvider.GetService<IPortfolioTrendAnalysisService>();
        var portfolioDecisionSignalService = scope.ServiceProvider.GetService<IPortfolioDecisionSignalService>();
        var portfolioProgressQueryService = scope.ServiceProvider.GetService<PortfolioProgressQueryService>();
        var portfolioSnapshotQueryService = scope.ServiceProvider.GetService<PortfolioSnapshotQueryService>();
        var portfolioComparisonQueryService = scope.ServiceProvider.GetService<PortfolioComparisonQueryService>();
        var portfolioTrendQueryService = scope.ServiceProvider.GetService<PortfolioTrendQueryService>();
        var portfolioDecisionSignalQueryService = scope.ServiceProvider.GetService<PortfolioDecisionSignalQueryService>();
        var insightService = scope.ServiceProvider.GetService<IInsightService>();
        var deliveryProgressRollupService = scope.ServiceProvider.GetService<IDeliveryProgressRollupService>();
        var portfolioDeliverySummaryService = scope.ServiceProvider.GetService<IPortfolioDeliverySummaryService>();
        var sprintCommitmentService = scope.ServiceProvider.GetService<ISprintCommitmentService>();
        var sprintScopeChangeService = scope.ServiceProvider.GetService<ISprintScopeChangeService>();
        var sprintCompletionService = scope.ServiceProvider.GetService<ISprintCompletionService>();
        var sprintSpilloverService = scope.ServiceProvider.GetService<ISprintSpilloverService>();
        var sprintFactService = scope.ServiceProvider.GetService<ISprintFactService>();
        var sprintDeliveryProjectionService = scope.ServiceProvider.GetService<ISprintDeliveryProjectionService>();
        var portfolioFlowSummaryService = scope.ServiceProvider.GetService<IPortfolioFlowSummaryService>();
        var sprintExecutionMetricsCalculator = scope.ServiceProvider.GetService<PoTool.Core.Domain.Cdc.Sprints.ISprintExecutionMetricsCalculator>();
        var completionForecastService = scope.ServiceProvider.GetService<ICompletionForecastService>();
        var velocityCalibrationService = scope.ServiceProvider.GetService<IVelocityCalibrationService>();
        var effortTrendForecastService = scope.ServiceProvider.GetService<IEffortTrendForecastService>();
        var effortDistributionService = scope.ServiceProvider.GetService<IEffortDistributionService>();
        var effortEstimationQualityService = scope.ServiceProvider.GetService<IEffortEstimationQualityService>();
        var effortEstimationSuggestionService = scope.ServiceProvider.GetService<IEffortEstimationSuggestionService>();
        var backlogQualityAnalysisService = scope.ServiceProvider.GetService<IBacklogQualityAnalysisService>();
        var projectionService = scope.ServiceProvider.GetService<SprintTrendProjectionService>();

        Assert.IsNotNull(storyPointResolutionService, "Canonical story-point resolution service should be registered.");
        Assert.IsNotNull(hierarchyRollupService, "Hierarchy rollup service should be registered.");
        Assert.IsNotNull(epicProgressService, "Epic progress service should be registered.");
        Assert.IsNotNull(epicAggregationService, "Epic aggregation service should be registered.");
        Assert.IsNotNull(productAggregationService, "Product aggregation service should be registered.");
        Assert.IsNotNull(planningQualityService, "Planning Quality service should be registered.");
        Assert.IsNotNull(snapshotComparisonService, "Snapshot comparison service should be registered.");
        Assert.IsNotNull(portfolioSnapshotValidationService, "Portfolio snapshot validation service should be registered.");
        Assert.IsNotNull(portfolioSnapshotComparisonService, "Portfolio snapshot comparison service should be registered.");
        Assert.IsNotNull(portfolioSnapshotFactory, "Portfolio snapshot factory should be registered.");
        Assert.IsNotNull(portfolioSnapshotPersistenceMapper, "Portfolio snapshot persistence mapper should be registered.");
        Assert.IsNotNull(portfolioSnapshotCaptureDataService, "Portfolio snapshot capture-data service should be registered.");
        Assert.IsNotNull(portfolioSnapshotPersistenceService, "Portfolio snapshot persistence service should be registered.");
        Assert.IsNotNull(portfolioSnapshotSelectionService, "Portfolio snapshot selection service should be registered.");
        Assert.IsNotNull(portfolioSnapshotCaptureOrchestrator, "Portfolio snapshot capture orchestrator should be registered.");
        Assert.IsNotNull(portfolioReadModelStateService, "Portfolio read-model state service should be registered.");
        Assert.IsNotNull(portfolioReadModelMapper, "Portfolio read-model mapper should be registered.");
        Assert.IsNotNull(portfolioTrendAnalysisService, "Portfolio trend analysis service should be registered.");
        Assert.IsNotNull(portfolioDecisionSignalService, "Portfolio decision-signal service should be registered.");
        Assert.IsNotNull(portfolioProgressQueryService, "Portfolio progress query service should be registered.");
        Assert.IsNotNull(portfolioSnapshotQueryService, "Portfolio snapshot query service should be registered.");
        Assert.IsNotNull(portfolioComparisonQueryService, "Portfolio comparison query service should be registered.");
        Assert.IsNotNull(portfolioTrendQueryService, "Portfolio trend query service should be registered.");
        Assert.IsNotNull(portfolioDecisionSignalQueryService, "Portfolio decision-signal query service should be registered.");
        Assert.IsNotNull(insightService, "Insight service should be registered.");
        Assert.IsNotNull(deliveryProgressRollupService, "Delivery progress rollup service should be registered.");
        Assert.IsNotNull(portfolioDeliverySummaryService, "Portfolio delivery summary service should be registered.");
        Assert.IsNotNull(sprintCommitmentService, "Sprint commitment CDC service should be registered.");
        Assert.IsNotNull(sprintScopeChangeService, "Sprint scope-change CDC service should be registered.");
        Assert.IsNotNull(sprintCompletionService, "Sprint completion CDC service should be registered.");
        Assert.IsNotNull(sprintSpilloverService, "Sprint spillover CDC service should be registered.");
        Assert.IsNotNull(sprintFactService, "Sprint fact CDC service should be registered.");
        Assert.IsNotNull(sprintDeliveryProjectionService, "Sprint delivery projection service should be registered.");
        Assert.IsNotNull(portfolioFlowSummaryService, "Portfolio flow summary service should be registered.");
        Assert.IsNotNull(sprintExecutionMetricsCalculator, "Sprint execution metrics CDC service should be registered.");
        Assert.IsNotNull(completionForecastService, "Completion forecast service should be registered.");
        Assert.IsNotNull(velocityCalibrationService, "Velocity calibration service should be registered.");
        Assert.IsNotNull(effortTrendForecastService, "Effort trend forecast service should be registered.");
        Assert.IsNotNull(effortDistributionService, "Effort distribution CDC service should be registered.");
        Assert.IsNotNull(effortEstimationQualityService, "Effort estimation quality CDC service should be registered.");
        Assert.IsNotNull(effortEstimationSuggestionService, "Effort estimation suggestion CDC service should be registered.");
        Assert.IsNotNull(backlogQualityAnalysisService, "Backlog quality analysis service should be registered.");
        Assert.IsNotNull(projectionService, "SprintTrendProjectionService should be resolvable from DI.");
    }

    [TestMethod]
    public void AddPoToolApiServices_ResolvesMockTfsClient_WhenMockModeEnabled()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TfsIntegration:UseMockClient"] = bool.TrueString
            })
            .Build();

        services.AddLogging();
        RegisterHostEnvironment(services);
        services.AddDbContext<PoToolDbContext>(options =>
            options.UseInMemoryDatabase("TestDb4"));

        services.AddPoToolApiServices(configuration, isDevelopment: true);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var runtimeMode = scope.ServiceProvider.GetRequiredService<TfsRuntimeMode>();
        var tfsClient = scope.ServiceProvider.GetRequiredService<ITfsClient>();
        var gateway = scope.ServiceProvider.GetRequiredService<ITfsAccessGateway>();

        Assert.IsTrue(runtimeMode.UseMockClient, "Mock mode should be captured once during startup.");
        Assert.AreSame(gateway, tfsClient, "ITfsClient should resolve through the TFS access gateway.");
        Assert.IsInstanceOfType<TfsAccessGateway>(gateway, "Mock mode should resolve the guarded TFS gateway.");
        Assert.IsTrue(((TfsAccessGateway)gateway).UsesMockClient, "Gateway should wrap the mock TFS client in mock mode.");
    }

    [TestMethod]
    public void AddPoToolApiServices_ResolvesRealTfsClient_WhenMockModeDisabled()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TfsIntegration:UseMockClient"] = bool.FalseString
            })
            .Build();

        services.AddLogging();
        RegisterHostEnvironment(services);
        services.AddDbContext<PoToolDbContext>(options =>
            options.UseInMemoryDatabase("TestDb5"));

        services.AddPoToolApiServices(configuration, isDevelopment: true);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var runtimeMode = scope.ServiceProvider.GetRequiredService<TfsRuntimeMode>();
        var tfsClient = scope.ServiceProvider.GetRequiredService<ITfsClient>();
        var gateway = scope.ServiceProvider.GetRequiredService<ITfsAccessGateway>();

        Assert.IsFalse(runtimeMode.UseMockClient, "Real mode should be captured once during startup.");
        Assert.AreSame(gateway, tfsClient, "ITfsClient should resolve through the TFS access gateway.");
        Assert.IsInstanceOfType<TfsAccessGateway>(gateway, "Real mode should resolve the guarded TFS gateway.");
        Assert.IsFalse(((TfsAccessGateway)gateway).UsesMockClient, "Gateway should wrap the real TFS client in real mode.");
        Assert.AreEqual(typeof(RealTfsClient).FullName, ((TfsAccessGateway)gateway).InnerClientTypeName);
    }

    [TestMethod]
    public void AddPoToolApiServices_RegistersCachedPullRequestProviderAsDefault_AndPreservesExplicitLiveProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddLogging();
        RegisterHostEnvironment(services);
        services.AddDbContext<PoToolDbContext>(options =>
            options.UseInMemoryDatabase("TestDb6"));

        services.AddPoToolApiServices(configuration, isDevelopment: true);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var defaultProvider = scope.ServiceProvider.GetRequiredService<IPullRequestReadProvider>();
        var liveProvider = scope.ServiceProvider.GetRequiredKeyedService<IPullRequestReadProvider>("Live");
        var cachedProvider = scope.ServiceProvider.GetRequiredKeyedService<IPullRequestReadProvider>("Cached");

        Assert.IsInstanceOfType<CachedPullRequestReadProvider>(
            defaultProvider,
            "Analytical pull request reads should resolve the cached provider by default.");
        Assert.IsInstanceOfType<LivePullRequestReadProvider>(
            liveProvider,
            "Explicit live pull request flows must still be able to resolve the live provider.");
        Assert.IsInstanceOfType<CachedPullRequestReadProvider>(
            cachedProvider,
            "Explicit cached pull request resolution should remain available.");
    }

    [TestMethod]
    public void AddPoToolApiServices_RegistersCachedPipelineProviderAsDefault_AndPreservesExplicitLiveProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddLogging();
        RegisterHostEnvironment(services);
        services.AddDbContext<PoToolDbContext>(options =>
            options.UseInMemoryDatabase("TestDb7"));

        services.AddPoToolApiServices(configuration, isDevelopment: true);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var defaultProvider = scope.ServiceProvider.GetRequiredService<IPipelineReadProvider>();
        var liveProvider = scope.ServiceProvider.GetRequiredKeyedService<IPipelineReadProvider>("Live");
        var cachedProvider = scope.ServiceProvider.GetRequiredKeyedService<IPipelineReadProvider>("Cached");

        Assert.IsInstanceOfType<CachedPipelineReadProvider>(
            defaultProvider,
            "Analytical pipeline reads should resolve the cached provider by default.");
        Assert.IsInstanceOfType<LivePipelineReadProvider>(
            liveProvider,
            "Explicit live pipeline discovery flows must still be able to resolve the live provider.");
        Assert.IsInstanceOfType<CachedPipelineReadProvider>(
            cachedProvider,
            "Explicit cached pipeline resolution should remain available.");
    }

    private static void RegisterHostEnvironment(IServiceCollection services)
    {
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupProperty(environment => environment.EnvironmentName, Environments.Development);
        hostEnvironment.SetupProperty(environment => environment.ApplicationName, "PoTool.Tests.Unit");
        hostEnvironment.SetupProperty(environment => environment.ContentRootPath, AppContext.BaseDirectory);
        hostEnvironment.SetupProperty(environment => environment.ContentRootFileProvider, new NullFileProvider());

        services.AddSingleton(hostEnvironment.Object);
    }
}
