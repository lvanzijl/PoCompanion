using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Services;
using PoTool.Core.Configuration;
using PoTool.Core.Contracts;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class DataSourceAwareReadProviderFactoryTests
{
    [TestMethod]
    public void GetWorkItemReadProvider_ReturnsCachedProvider_WhenModeIsCache()
    {
        var services = new ServiceCollection();

        var mockLiveProvider = new Mock<IWorkItemReadProvider>();
        var mockCachedProvider = new Mock<IWorkItemReadProvider>();

        services.AddKeyedScoped<IWorkItemReadProvider>("Live", (sp, key) => mockLiveProvider.Object);
        services.AddKeyedScoped<IWorkItemReadProvider>("Cached", (sp, key) => mockCachedProvider.Object);

        var mockModeProvider = new Mock<IDataSourceModeProvider>();
        mockModeProvider.SetupGet(m => m.Mode).Returns(DataSourceMode.Cache);
        services.AddScoped<IDataSourceModeProvider>(_ => mockModeProvider.Object);

        services.AddLogging();
        services.AddScoped<DataSourceAwareReadProviderFactory>();

        using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<DataSourceAwareReadProviderFactory>();

        var provider = factory.GetWorkItemReadProvider();

        Assert.AreSame(mockCachedProvider.Object, provider);
    }
}
