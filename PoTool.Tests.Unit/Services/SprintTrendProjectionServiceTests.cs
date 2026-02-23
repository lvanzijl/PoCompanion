using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PoTool.Api.Services;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class SprintTrendProjectionServiceTests
{
    [TestMethod]
    public async Task ComputeProjectionsAsync_ReturnsEmpty_WhenActivitySourceIsNotConfigured()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var service = new SprintTrendProjectionService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SprintTrendProjectionService>.Instance);

        var projections = await service.ComputeProjectionsAsync(1, [1, 2, 3]);

        Assert.HasCount(0, projections);
    }
}
