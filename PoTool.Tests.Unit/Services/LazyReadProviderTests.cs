using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Services;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Services;

/// <summary>
/// Tests for lazy read provider wrappers that ensure provider resolution
/// is delayed until method calls (after middleware has set the mode).
/// </summary>
[TestClass]
public class LazyReadProviderTests
{
    [TestMethod]
    public async Task LazyWorkItemReadProvider_DelegatesCallsToFactory()
    {
        // Arrange - Setup service provider with keyed services
        var services = new ServiceCollection();
        
        var mockLiveProvider = new Mock<IWorkItemReadProvider>();
        mockLiveProvider.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> 
            { 
                new WorkItemDto(TfsId: 1, Type: "Task", Title: "Test", ParentTfsId: null, AreaPath: "Area", IterationPath: "Iteration", State: "New", RetrievedAt: DateTimeOffset.UtcNow, Effort: null, Description: null) 
            });
        
        var mockCachedProvider = new Mock<IWorkItemReadProvider>();
        mockCachedProvider.Setup(p => p.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> 
            { 
                new WorkItemDto(TfsId: 2, Type: "Task", Title: "Test", ParentTfsId: null, AreaPath: "Area", IterationPath: "Iteration", State: "New", RetrievedAt: DateTimeOffset.UtcNow, Effort: null, Description: null) 
            });

        services.AddKeyedScoped<IWorkItemReadProvider>("Live", (sp, key) => mockLiveProvider.Object);
        services.AddKeyedScoped<IWorkItemReadProvider>("Cached", (sp, key) => mockCachedProvider.Object);
        
        var mockModeProvider = new Mock<IDataSourceModeProvider>();
        mockModeProvider.SetupGet(m => m.Mode).Returns(DataSourceMode.Cache);
        services.AddScoped<IDataSourceModeProvider>(sp => mockModeProvider.Object);
        
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddScoped<DataSourceAwareReadProviderFactory>();
        
        var serviceProvider = services.BuildServiceProvider();

        // Create the lazy provider
        var factory = serviceProvider.GetRequiredService<DataSourceAwareReadProviderFactory>();
        var lazyProvider = new LazyWorkItemReadProvider(factory);

        // Act
        var result = await lazyProvider.GetAllAsync();

        // Assert - Should have called the cached provider (mode is Cache)
        Assert.IsNotNull(result);
        var items = result.ToList();
        Assert.HasCount(1, items);
        Assert.AreEqual(2, items[0].TfsId); // Cached provider returns TfsId=2
    }

    [TestMethod]
    public async Task LazyWorkItemReadProvider_RespectsLiveMode()
    {
        // Arrange
        var services = new ServiceCollection();
        
        var mockLiveProvider = new Mock<IWorkItemReadProvider>();
        mockLiveProvider.Setup(p => p.GetFilteredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> 
            { 
                new WorkItemDto(TfsId: 100, Type: "Task", Title: "Test", ParentTfsId: null, AreaPath: "Area", IterationPath: "Iteration", State: "New", RetrievedAt: DateTimeOffset.UtcNow, Effort: null, Description: null) 
            });
        
        var mockCachedProvider = new Mock<IWorkItemReadProvider>();
        mockCachedProvider.Setup(p => p.GetFilteredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto> 
            { 
                new WorkItemDto(TfsId: 200, Type: "Task", Title: "Test", ParentTfsId: null, AreaPath: "Area", IterationPath: "Iteration", State: "New", RetrievedAt: DateTimeOffset.UtcNow, Effort: null, Description: null) 
            });

        services.AddKeyedScoped<IWorkItemReadProvider>("Live", (sp, key) => mockLiveProvider.Object);
        services.AddKeyedScoped<IWorkItemReadProvider>("Cached", (sp, key) => mockCachedProvider.Object);
        
        var mockModeProvider = new Mock<IDataSourceModeProvider>();
        mockModeProvider.SetupGet(m => m.Mode).Returns(DataSourceMode.Live);
        services.AddScoped<IDataSourceModeProvider>(sp => mockModeProvider.Object);
        
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddScoped<DataSourceAwareReadProviderFactory>();
        
        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataSourceAwareReadProviderFactory>();
        var lazyProvider = new LazyWorkItemReadProvider(factory);

        // Act
        var result = await lazyProvider.GetFilteredAsync("test");

        // Assert - Should have called the live provider (mode is Live)
        Assert.IsNotNull(result);
        var items = result.ToList();
        Assert.HasCount(1, items);
        Assert.AreEqual(100, items[0].TfsId); // Live provider returns TfsId=100
    }
}
