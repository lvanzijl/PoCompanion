using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.DataState;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Models;

[TestClass]
public sealed class DataStateViewModelTests
{
    [TestMethod]
    public void FromResponse_WhenCanonicalEnvelopeContainsInvalidFields_MapsToInvalidUiState()
    {
        var response = new DataStateResponseDto<DeliveryQueryResponseDto<HomeProductBarMetricsDto>>
        {
            State = DataStateDto.Available,
            Data = new DeliveryQueryResponseDto<HomeProductBarMetricsDto>
            {
                Data = new HomeProductBarMetricsDto { BugCount = 1 },
                RequestedFilter = new DeliveryFilterContextDto
                {
                    ProductIds = new FilterSelectionDto<int> { IsAll = false, Values = [5] },
                    Time = new FilterTimeSelectionDto { Mode = FilterTimeSelectionModeDto.None, SprintIds = [] }
                },
                EffectiveFilter = new DeliveryFilterContextDto
                {
                    ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                    Time = new FilterTimeSelectionDto { Mode = FilterTimeSelectionModeDto.None, SprintIds = [] }
                },
                InvalidFields = ["productIds"],
                ValidationMessages = [],
                TeamLabels = new Dictionary<int, string>(),
                SprintLabels = new Dictionary<int, string>()
            }
        };

        var viewModel = DataStateViewModel<DeliveryQueryResponseDto<HomeProductBarMetricsDto>>.FromResponse(
            response,
            "Capacity calibration unavailable.");

        Assert.AreEqual(UiDataState.Invalid, viewModel.UiState);
    }

    [TestMethod]
    public void FromResult_PreservesInvalidStatus()
    {
        var result = DataStateResult<string>.Invalid("Selected team does not belong to the current product.");

        var viewModel = DataStateViewModel<string>.FromResult(result);

        Assert.AreEqual(UiDataState.Invalid, viewModel.UiState);
        Assert.AreEqual("Selected team does not belong to the current product.", viewModel.Reason);
    }

    [TestMethod]
    public void InvalidFactory_CreatesInvalidUiStateWithoutResponseEnvelope()
    {
        var viewModel = DataStateViewModel<string>.Invalid("Selected sprint range cannot be resolved.");

        Assert.AreEqual(UiDataState.Invalid, viewModel.UiState);
        Assert.AreEqual("Selected sprint range cannot be resolved.", viewModel.Reason);
    }

    [TestMethod]
    public void FromResponse_WhenCanonicalEnvelopeContainsInvalidFields_PreservesMetadata()
    {
        var response = new DataStateResponseDto<DeliveryQueryResponseDto<HomeProductBarMetricsDto>>
        {
            State = DataStateDto.Available,
            Data = new DeliveryQueryResponseDto<HomeProductBarMetricsDto>
            {
                Data = new HomeProductBarMetricsDto { BugCount = 2 },
                RequestedFilter = new DeliveryFilterContextDto
                {
                    ProductIds = new FilterSelectionDto<int> { IsAll = false, Values = [99] },
                    Time = new FilterTimeSelectionDto { Mode = FilterTimeSelectionModeDto.None, SprintIds = [] }
                },
                EffectiveFilter = new DeliveryFilterContextDto
                {
                    ProductIds = new FilterSelectionDto<int> { IsAll = true, Values = [] },
                    Time = new FilterTimeSelectionDto { Mode = FilterTimeSelectionModeDto.None, SprintIds = [] }
                },
                InvalidFields = ["productIds"],
                ValidationMessages = [new FilterValidationIssueDto { Field = "productIds", Message = "Product scope was normalized." }],
                TeamLabels = new Dictionary<int, string>(),
                SprintLabels = new Dictionary<int, string>()
            }
        };

        var viewModel = DataStateViewModel<DeliveryQueryResponseDto<HomeProductBarMetricsDto>>.FromResponse(
            response,
            "Home product bar unavailable.");

        CollectionAssert.AreEqual(new[] { "productIds" }, viewModel.InvalidFields.ToArray());
        Assert.AreEqual("Product scope was normalized.", viewModel.ValidationMessages.Single().Message);
        Assert.IsNotNull(viewModel.FilterMetadata);
    }
}
