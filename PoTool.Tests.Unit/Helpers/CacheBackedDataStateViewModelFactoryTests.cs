using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Helpers;

[TestClass]
public sealed class CacheBackedDataStateViewModelFactoryTests
{
    [TestMethod]
    public void ToViewModel_MapsNotReadyCacheResponsesToLoadingUiState()
    {
        var result = CacheBackedClientResult<string>.NotReady("Cache warming", retryAfterSeconds: 30);

        var viewModel = result.ToViewModel("fallback");

        Assert.AreEqual(UiDataState.Loading, viewModel.UiState);
        Assert.AreEqual("Cache warming", viewModel.Reason);
        Assert.AreEqual(30, viewModel.RetryAfterSeconds);
        Assert.AreEqual(DataStateResultStatus.NotReady, viewModel.ResultStatus);
    }

    [TestMethod]
    public void ToViewModel_MapsEmptyCacheResponsesToEmptyUiState()
    {
        var result = CacheBackedClientResult<string>.Empty("No rows");

        var viewModel = result.ToViewModel("fallback");

        Assert.AreEqual(UiDataState.EmptyButValid, viewModel.UiState);
        Assert.AreEqual("No rows", viewModel.Reason);
    }

    [TestMethod]
    public void ToViewModel_MapsUnavailableCacheResponsesToErrorUiState()
    {
        var result = CacheBackedClientResult<string>.Unavailable("Service unavailable");

        var viewModel = result.ToViewModel("fallback");

        Assert.AreEqual(UiDataState.Failed, viewModel.UiState);
        Assert.AreEqual("Service unavailable", viewModel.Reason);
    }

    [TestMethod]
    public void ToViewModel_MapsCanonicalInvalidMetadataToInvalidUiState()
    {
        var metadata = new CanonicalFilterMetadata(
            CanonicalFilterKind.Delivery,
            RequestedFilter: new { ProductId = 1 },
            EffectiveFilter: new { ProductId = 2 },
            InvalidFields: ["productId"],
            ValidationMessages:
            [
                new FilterValidationIssueDto
                {
                    Field = "productId",
                    Message = "Product is not valid for the selected scope."
                }
            ],
            TeamLabels: new Dictionary<int, string>(),
            SprintLabels: new Dictionary<int, string>());

        var result = CacheBackedClientResult<CanonicalClientResponse<string>>.Success(
            new CanonicalClientResponse<string>("payload", metadata));

        var viewModel = result.ToViewModel("fallback");

        Assert.AreEqual(UiDataState.Invalid, viewModel.UiState);
        CollectionAssert.AreEqual(new[] { "productId" }, viewModel.InvalidFields.ToArray());
        Assert.AreEqual("Product is not valid for the selected scope.", viewModel.Reason);
        Assert.AreSame(metadata, viewModel.FilterMetadata);
    }
}
