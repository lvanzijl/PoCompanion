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
using PoTool.Core.Contracts;
using PoTool.Core.Domain.Cdc.Sprints;
using PoTool.Core.Domain.DeliveryTrends.Services;
using PoTool.Core.Domain.Estimation;
using PoTool.Core.Domain.Forecasting.Services;
using PoTool.Core.Domain.Hierarchy;
using PoTool.Core.Domain.Metrics;
using PoTool.Core.Domain.Portfolio;

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
        var projectionService = scope.ServiceProvider.GetService<SprintTrendProjectionService>();

        Assert.IsNotNull(storyPointResolutionService, "Canonical story-point resolution service should be registered.");
        Assert.IsNotNull(hierarchyRollupService, "Hierarchy rollup service should be registered.");
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
        Assert.IsNotNull(projectionService, "SprintTrendProjectionService should be resolvable from DI.");
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
