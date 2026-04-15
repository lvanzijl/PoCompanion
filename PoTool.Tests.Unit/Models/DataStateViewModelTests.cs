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
        var response = new DataStateResponseDto<FakeInvalidPayload>
        {
            State = DataStateDto.Available,
            Data = new FakeInvalidPayload
            {
                InvalidFields = ["productIds"]
            }
        };

        var viewModel = DataStateViewModel<FakeInvalidPayload>.FromResponse(
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

    private sealed class FakeInvalidPayload
    {
        public IReadOnlyList<string> InvalidFields { get; init; } = [];
    }
}
