using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.DataState;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class CacheStateResponseServiceTests
{
    [TestMethod]
    public async Task ExecuteAsync_NotReady_DoesNotInvokeLoader()
    {
        var service = CreateService(new CacheStateDto
        {
            ProductOwnerId = 1,
            SyncStatus = CacheSyncStatusDto.InProgress
        });

        var wasCalled = false;
        var result = await service.ExecuteAsync<string>(
            _ =>
            {
                wasCalled = true;
                return Task.FromResult<string?>(null);
            },
            value => string.IsNullOrWhiteSpace(value),
            "No data",
            "Load failed");

        Assert.AreEqual(DataStateDto.NotReady, result.State);
        Assert.IsFalse(wasCalled);
    }

    [TestMethod]
    public async Task ExecuteAsync_AvailableNullPayload_ReturnsEmpty()
    {
        var service = CreateService(new CacheStateDto
        {
            ProductOwnerId = 1,
            SyncStatus = CacheSyncStatusDto.Success,
            LastSuccessfulSync = DateTimeOffset.UtcNow
        });

        var result = await service.ExecuteAsync<string>(
            _ => Task.FromResult<string?>(null),
            value => string.IsNullOrWhiteSpace(value),
            "No data",
            "Load failed");

        Assert.AreEqual(DataStateDto.Empty, result.State);
        Assert.AreEqual("No data", result.Reason);
    }

    [TestMethod]
    public async Task ExecuteAsync_AvailablePayload_ReturnsAvailable()
    {
        var service = CreateService(new CacheStateDto
        {
            ProductOwnerId = 1,
            SyncStatus = CacheSyncStatusDto.Success,
            LastSuccessfulSync = DateTimeOffset.UtcNow
        });

        var result = await service.ExecuteAsync(
            _ => Task.FromResult<string?>("ready"),
            value => string.IsNullOrWhiteSpace(value),
            "No data",
            "Load failed");

        Assert.AreEqual(DataStateDto.Available, result.State);
        Assert.AreEqual("ready", result.Data);
    }

    private static CacheStateResponseService CreateService(CacheStateDto cacheState)
    {
        var currentProfileProvider = new Mock<ICurrentProfileProvider>();
        currentProfileProvider.Setup(provider => provider.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cacheState.ProductOwnerId);

        var cacheStateRepository = new Mock<ICacheStateRepository>();
        cacheStateRepository.Setup(repository => repository.GetCacheStateAsync(cacheState.ProductOwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cacheState);

        var readinessService = new CacheReadinessStateService(currentProfileProvider.Object, cacheStateRepository.Object);
        return new CacheStateResponseService(readinessService, NullLogger<CacheStateResponseService>.Instance);
    }
}
