using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class DataStateResultFactoryTests
{
    [TestMethod]
    public void ToDataStateResult_WhenCanonicalEnvelopeContainsInvalidFields_PreservesMetadata()
    {
        var response = new DataStateResponseDto<DeliveryQueryResponseDto<HomeProductBarMetricsDto>>
        {
            State = DataStateDto.Available,
            Data = new DeliveryQueryResponseDto<HomeProductBarMetricsDto>
            {
                Data = new HomeProductBarMetricsDto { BugCount = 2 },
                RequestedFilter = CreateFilter([999]),
                EffectiveFilter = CreateFilter([], isAll: true),
                InvalidFields = ["productIds"],
                ValidationMessages = [new FilterValidationIssueDto { Field = "productIds", Message = "Product scope was normalized." }],
                TeamLabels = new Dictionary<int, string>(),
                SprintLabels = new Dictionary<int, string>()
            }
        };

        var result = response.ToDataStateResult();

        Assert.AreEqual(DataStateResultStatus.Invalid, result.Status);
        Assert.IsTrue(result.CanUseData);
        CollectionAssert.AreEqual(new[] { "productIds" }, result.InvalidFields.ToArray());
        CollectionAssert.AreEqual(new[] { 999 }, ((DeliveryFilterContextDto)result.RequestedFilter!).ProductIds.Values.ToArray());
        Assert.IsTrue(((DeliveryFilterContextDto)result.EffectiveFilter!).ProductIds.IsAll);
    }

    [TestMethod]
    public void ToDataStateResult_WhenNotReady_PreservesReason()
    {
        var response = new DataStateResponseDto<int>
        {
            State = DataStateDto.NotReady,
            Reason = "Cache warming",
            RetryAfterSeconds = 5
        };

        var result = response.ToDataStateResult();

        Assert.AreEqual(DataStateResultStatus.NotReady, result.Status);
        Assert.AreEqual("Cache warming", result.Reason);
        Assert.AreEqual(5, result.RetryAfterSeconds);
    }

    private static DeliveryFilterContextDto CreateFilter(IReadOnlyList<int> productIds, bool isAll = false)
        => new()
        {
            ProductIds = new FilterSelectionDto<int>
            {
                IsAll = isAll,
                Values = productIds.ToArray()
            },
            Time = new FilterTimeSelectionDto
            {
                Mode = FilterTimeSelectionModeDto.None,
                SprintIds = []
            }
        };
}
