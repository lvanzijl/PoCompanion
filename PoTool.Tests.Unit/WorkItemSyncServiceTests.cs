using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Core.WorkItems;
using PoTool.Api.Hubs;
using PoTool.Api.Persistence;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class WorkItemSyncServiceTests
{
    [TestMethod]
    public async Task TriggerSyncAsync_UsesTfsClientAndReplacesRepository()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // In-memory DB
        services.AddDbContext<PoToolDbContext>(options =>
            options.UseInMemoryDatabase("testdb"));

        // Mock ITfsClient
        var tfsMock = new Mock<ITfsClient>();
        tfsMock.Setup(t => t.GetWorkItemsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new[]
               {
                   new WorkItemDto(1, "Epic", "Test", "Area", "Iter", "Active", "{}", DateTimeOffset.UtcNow)
               } as IEnumerable<WorkItemDto>);

        services.AddSingleton(tfsMock.Object);

        // Use real repository
        services.AddScoped<PoTool.Api.Repositories.WorkItemRepository>();

        // HubContext mock
        var hubMock = new Mock<IHubContext<WorkItemHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.All).Returns(clientProxy.Object);
        hubMock.SetupGet(h => h.Clients).Returns(clientsMock.Object);
        services.AddSingleton(hubMock.Object);

        services.AddSingleton<WorkItemSyncService>();
        services.AddSingleton<NullLoggerFactory>();

        var provider = services.BuildServiceProvider();

        var syncService = provider.GetRequiredService<WorkItemSyncService>();

        // Execute the trigger
        await syncService.TriggerSyncAsync("Area");

        // Verify repository now has items
        var repo = provider.GetRequiredService<PoTool.Api.Repositories.WorkItemRepository>();
        var items = await repo.GetAllAsync();
        Assert.AreEqual(1, items.Count());
    }
}
