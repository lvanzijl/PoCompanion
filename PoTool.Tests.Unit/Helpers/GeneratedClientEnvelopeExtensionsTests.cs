using PoTool.Client.ApiClient;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.BuildQuality;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;
using PoTool.Shared.WorkItems;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class GeneratedClientEnvelopeExtensionsTests
{
    [TestMethod]
    public void ToCacheBackedResult_UsesMappedEnvelopeExtension()
    {
        var envelope = new DataStateResponseDtoOfDeliveryQueryResponseDtoOfBuildQualityPageDto
        {
            State = DataStateDto.Available,
            Data = new DeliveryQueryResponseDtoOfBuildQualityPageDto
            {
                Data = new BuildQualityPageDto(),
                RequestedFilter = CreateDeliveryFilter(),
                EffectiveFilter = CreateDeliveryFilter(),
                InvalidFields = ["windowEndUtc"],
                ValidationMessages =
                [
                    new FilterValidationIssueDto
                    {
                        Field = "windowEndUtc",
                        Message = "Adjusted"
                    }
                ]
            }
        };

        var result = envelope.ToCacheBackedResult();

        Assert.AreEqual(CacheBackedClientState.Success, result.State);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(FilterTimeSelectionModeDto.MultiSprint, result.Data.RequestedFilter.Time.Mode);
        CollectionAssert.AreEqual(new[] { "windowEndUtc" }, result.Data.InvalidFields.ToArray());
    }

    [TestMethod]
    public void ToDataStateResponse_UsesMappedEnvelopeExtension()
    {
        var envelope = new DataStateResponseDtoOfSprintQueryResponseDtoOfGetSprintTrendMetricsResponse
        {
            State = DataStateDto.NotReady,
            Reason = "warming",
            RetryAfterSeconds = 30
        };

        var response = envelope.ToDataStateResponse();

        Assert.AreEqual(DataStateDto.NotReady, response.State);
        Assert.AreEqual("warming", response.Reason);
        Assert.AreEqual(30, response.RetryAfterSeconds);
        Assert.IsNull(response.Data);
    }

    [TestMethod]
    public void ReadOnlyListHelpers_MaterializeCollectionPayloads()
    {
        var envelope = new DataStateResponseDtoOfIEnumerableOfWorkItemDto
        {
            State = DataStateDto.Available,
            Data =
            [
                new WorkItemDto { TfsId = 11, Title = "One" },
                new WorkItemDto { TfsId = 12, Title = "Two" }
            ]
        };

        var dataResult = envelope.ToReadOnlyListDataStateResult();
        var stateResponse = envelope.ToReadOnlyListDataStateResponse();

        Assert.IsTrue(dataResult.CanUseData);
        Assert.HasCount(2, dataResult.Data!);
        Assert.HasCount(2, stateResponse.Data ?? Array.Empty<WorkItemDto>());
        Assert.AreEqual(11, dataResult.Data![0].TfsId);
    }

    private static DeliveryFilterContextDto CreateDeliveryFilter()
        => new()
        {
            ProductIds = new FilterSelectionDto<int> { IsAll = false, Values = [42] },
            Time = new FilterTimeSelectionDto
            {
                Mode = FilterTimeSelectionModeDto.MultiSprint,
                SprintIds = [3, 4]
            }
        };
}
