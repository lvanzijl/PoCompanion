using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.WorkItems;
using PoTool.Api.Hubs;
using PoTool.Api.Persistence;

using PoTool.Core.WorkItems;

namespace PoTool.Tests.Unit;

[TestClass]
public sealed class WorkItemSyncServiceTests
{
    [TestMethod]
    public async Task TriggerSyncAsync_UsesTfsClientAndReplacesRepository()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // In-memory DB with unique name per test run - use single instance across scopes
        var dbName = "testdb_sync_" + Guid.NewGuid().ToString();
        services.AddDbContext<PoToolDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        // Mock ITfsClient
        var tfsMock = new Mock<ITfsClient>();
        tfsMock.Setup(t => t.GetWorkItemsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new[]
               {
                   new WorkItemDto(1, "Epic", "Test", null, "Area", "Iter", "Active", "{}", DateTimeOffset.UtcNow, null, null)
               } as IEnumerable<WorkItemDto>);

        services.AddSingleton(tfsMock.Object);

        // Register IWorkItemRepository as Scoped - WorkItemSyncService creates scopes internally
        services.AddScoped<IWorkItemRepository, PoTool.Api.Repositories.WorkItemRepository>();

        // HubContext mock - setup to allow SendAsync calls
        var hubMock = new Mock<IHubContext<WorkItemHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);
        clientsMock.Setup(c => c.All).Returns(clientProxy.Object);
        hubMock.SetupGet(h => h.Clients).Returns(clientsMock.Object);
        services.AddSingleton(hubMock.Object);

        services.AddSingleton<WorkItemSyncService>();
        services.AddSingleton<NullLoggerFactory>();

        var provider = services.BuildServiceProvider();

        var syncService = provider.GetRequiredService<WorkItemSyncService>();

        // Execute the trigger
        await syncService.TriggerSyncAsync("Area");

        // Verify TFS client was called
        tfsMock.Verify(t => t.GetWorkItemsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify repository now has items - use a new scope since sync service uses scopes
        using var scope = provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();
        var items = await repo.GetAllAsync();
        Assert.AreEqual(1, items.Count());
    }
}
