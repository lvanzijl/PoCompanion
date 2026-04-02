using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.DataState;
using PoTool.Shared.Settings;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public class CacheReadinessStateServiceTests
{
    [TestMethod]
    public async Task GetCurrentStateAsync_NoSuccessfulSyncInProgress_ReturnsNotReady()
    {
        var currentProfileProvider = new Mock<ICurrentProfileProvider>();
        currentProfileProvider.Setup(provider => provider.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var cacheStateRepository = new Mock<ICacheStateRepository>();
        cacheStateRepository.Setup(repository => repository.GetCacheStateAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheStateDto
            {
                ProductOwnerId = 7,
                SyncStatus = CacheSyncStatusDto.InProgress,
                CurrentSyncStage = "Warm cache"
            });

        var service = new CacheReadinessStateService(currentProfileProvider.Object, cacheStateRepository.Object);

        var result = await service.GetCurrentStateAsync();

        Assert.AreEqual(DataStateDto.NotReady, result.State);
        StringAssert.Contains(result.Reason!, "Warm cache");
        Assert.AreEqual(2, result.RetryAfterSeconds);
    }

    [TestMethod]
    public async Task GetCurrentStateAsync_FailedWithoutSuccessfulSync_ReturnsFailed()
    {
        var currentProfileProvider = new Mock<ICurrentProfileProvider>();
        currentProfileProvider.Setup(provider => provider.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var cacheStateRepository = new Mock<ICacheStateRepository>();
        cacheStateRepository.Setup(repository => repository.GetCacheStateAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheStateDto
            {
                ProductOwnerId = 3,
                SyncStatus = CacheSyncStatusDto.Failed,
                LastErrorMessage = "Sync failed hard"
            });

        var service = new CacheReadinessStateService(currentProfileProvider.Object, cacheStateRepository.Object);

        var result = await service.GetCurrentStateAsync();

        Assert.AreEqual(DataStateDto.Failed, result.State);
        Assert.AreEqual("Sync failed hard", result.Reason);
    }

    [TestMethod]
    public async Task GetCurrentStateAsync_LastSuccessfulSyncExists_ReturnsAvailable()
    {
        var currentProfileProvider = new Mock<ICurrentProfileProvider>();
        currentProfileProvider.Setup(provider => provider.GetCurrentProductOwnerIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var cacheStateRepository = new Mock<ICacheStateRepository>();
        cacheStateRepository.Setup(repository => repository.GetCacheStateAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheStateDto
            {
                ProductOwnerId = 5,
                SyncStatus = CacheSyncStatusDto.Success,
                LastSuccessfulSync = DateTimeOffset.UtcNow
            });

        var service = new CacheReadinessStateService(currentProfileProvider.Object, cacheStateRepository.Object);

        var result = await service.GetCurrentStateAsync();

        Assert.AreEqual(DataStateDto.Available, result.State);
        Assert.AreEqual(5, result.ProductOwnerId);
    }
}
