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
        var response = new DataStateResponseDto<DeliveryQueryResponseDto<CapacityCalibrationDto>>
        {
            State = DataStateDto.Available,
            Data = new DeliveryQueryResponseDto<CapacityCalibrationDto>
            {
                Data = new CapacityCalibrationDto(),
                RequestedFilter = new DeliveryFilterContextDto(),
                EffectiveFilter = new DeliveryFilterContextDto(),
                InvalidFields = ["productIds"],
                ValidationMessages = [new FilterValidationIssueDto { Field = "productIds", Message = "Scope corrected." }],
                SprintLabels = new Dictionary<int, string>(),
                TeamLabels = new Dictionary<int, string>()
            }
        };

        var viewModel = DataStateViewModel<DeliveryQueryResponseDto<CapacityCalibrationDto>>.FromResponse(
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
}
