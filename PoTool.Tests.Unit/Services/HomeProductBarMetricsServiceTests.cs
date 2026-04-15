using Moq;
using PoTool.Client.ApiClient;
using PoTool.Client.Services;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class HomeProductBarMetricsServiceTests
{
    [TestMethod]
    public async Task GetAsync_WhenDataIsAvailable_ReturnsMetricsAndFilterMetadata()
    {
        var metricsClient = new Mock<IMetricsClient>();
        metricsClient
            .Setup(client => client.GetHomeProductBarMetricsAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataStateResponseDtoOfDeliveryQueryResponseDtoOfHomeProductBarMetricsDto
            {
                State = DataStateDto.Available,
                Data = new DeliveryQueryResponseDtoOfHomeProductBarMetricsDto
                {
                    Data = new HomeProductBarMetricsDto
                    {
                        SprintProgressPercentage = 55,
                        BugCount = 3,
                        ChangesTodayCount = 2
                    },
                    RequestedFilter = CreateDeliveryFilter([10]),
                    EffectiveFilter = CreateDeliveryFilter([10]),
                    InvalidFields = [],
                    ValidationMessages = []
                }
            });

        var service = new HomeProductBarMetricsService(metricsClient.Object);

        var result = await service.GetAsync(7, 10, CancellationToken.None);

        Assert.AreEqual(DataStateDto.Available, result.State);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(55, result.Data.SprintProgressPercentage);
        Assert.IsNotNull(result.FilterMetadata);
        CollectionAssert.AreEqual(new[] { 10 }, ((DeliveryFilterContextDto)result.FilterMetadata.RequestedFilter).ProductIds.Values.ToArray());
    }

    [TestMethod]
    public async Task GetAsync_WhenCacheIsNotReady_PreservesReasonInsteadOfReturningGenericFailure()
    {
        var metricsClient = new Mock<IMetricsClient>();
        metricsClient
            .Setup(client => client.GetHomeProductBarMetricsAsync(7, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataStateResponseDtoOfDeliveryQueryResponseDtoOfHomeProductBarMetricsDto
            {
                State = DataStateDto.NotReady,
                Reason = "Cache has not been built for the active profile yet.",
                RetryAfterSeconds = 2
            });

        var service = new HomeProductBarMetricsService(metricsClient.Object);

        var result = await service.GetAsync(7, null, CancellationToken.None);

        Assert.AreEqual(DataStateDto.NotReady, result.State);
        Assert.IsNull(result.Data);
        Assert.AreEqual("Cache has not been built for the active profile yet.", result.Reason);
    }

    private static DeliveryFilterContextDto CreateDeliveryFilter(IReadOnlyList<int> productIds)
        => new()
        {
            ProductIds = new FilterSelectionDto<int>
            {
                IsAll = productIds.Count == 0,
                Values = productIds.ToArray()
            },
            Time = new FilterTimeSelectionDto
            {
                Mode = FilterTimeSelectionModeDto.None,
                SprintIds = []
            }
        };
}
