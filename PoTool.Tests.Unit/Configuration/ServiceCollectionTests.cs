using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PoTool.Api.Configuration;
using PoTool.Api.Persistence;
using PoTool.Api.Services;
using PoTool.Core.Contracts;

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
        services.AddDbContext<PoToolDbContext>(options => 
            options.UseInMemoryDatabase("TestDb"));

        // Act - Register all services
        services.AddPoToolApiServices(configuration, isDevelopment: true);

        // Assert - Verify both concrete and interface registrations work
        var serviceProvider = services.BuildServiceProvider();
        
        // Should be able to resolve concrete type
        var concreteService = serviceProvider.GetService<TfsConfigurationService>();
        Assert.IsNotNull(concreteService, "TfsConfigurationService concrete type should be resolvable");
        
        // Should be able to resolve interface type
        var interfaceService = serviceProvider.GetService<ITfsConfigurationService>();
        Assert.IsNotNull(interfaceService, "ITfsConfigurationService interface should be resolvable");
        
        // Both should resolve to the same instance (scoped lifetime)
        var scope = serviceProvider.CreateScope();
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
        services.AddDbContext<PoToolDbContext>(options => 
            options.UseInMemoryDatabase("TestDb2"));

        // Act - Register all services
        services.AddPoToolApiServices(configuration, isDevelopment: true);

        // Assert - Should be able to build service provider without DI exceptions
        var serviceProvider = services.BuildServiceProvider();
        
        // Should be able to resolve WorkItemStateClassificationService which depends on TfsConfigurationService
        var stateClassificationService = serviceProvider.GetService<IWorkItemStateClassificationService>();
        Assert.IsNotNull(stateClassificationService, 
            "IWorkItemStateClassificationService should be resolvable (validates TfsConfigurationService dependency)");
    }
}
