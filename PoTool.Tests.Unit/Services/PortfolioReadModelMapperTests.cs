using PoTool.Api.Services;
using PoTool.Core.Domain.DeliveryTrends.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Tests.Unit.Services;

[TestClass]
public sealed class PortfolioReadModelMapperTests
{
    private static readonly IPortfolioReadModelMapper Mapper = new PortfolioReadModelMapper();

    [TestMethod]
    public void ToSnapshotItemDto_MapsCanonicalSnapshotFieldsExplicitly()
    {
        var dto = Mapper.ToSnapshotItemDto(
            new PortfolioSnapshotItem(7, "PRJ-100", "WP-3", 0.65d, 12d, WorkPackageLifecycleState.Retired),
            new Dictionary<int, string>
            {
                [7] = "Product Seven"
            });

        Assert.AreEqual(7, dto.ProductId);
        Assert.AreEqual("Product Seven", dto.ProductName);
        Assert.AreEqual("PRJ-100", dto.ProjectNumber);
        Assert.AreEqual("WP-3", dto.WorkPackage);
        Assert.AreEqual(PortfolioLifecycleState.Retired, dto.LifecycleState);
        Assert.AreEqual(0.65d, dto.Progress, 0.001d);
        Assert.AreEqual(12d, dto.Weight, 0.001d);
    }

    [TestMethod]
    public void ToComparisonItemDto_PreservesNullableDeltasAndLifecycleStates()
    {
        var dto = Mapper.ToComparisonItemDto(
            new PortfolioSnapshotComparisonItem(
                9,
                "PRJ-200",
                null,
                WorkPackageLifecycleState.Active,
                null,
                0.4d,
                null,
                null,
                8d,
                null,
                null),
            new Dictionary<int, string>
            {
                [9] = "Product Nine"
            });

        Assert.AreEqual("Product Nine", dto.ProductName);
        Assert.AreEqual(PortfolioLifecycleState.Active, dto.PreviousLifecycleState);
        Assert.IsNull(dto.CurrentLifecycleState);
        Assert.IsTrue(dto.PreviousProgress.HasValue);
        Assert.AreEqual(0.4d, dto.PreviousProgress.Value, 0.001d);
        Assert.IsNull(dto.CurrentProgress);
        Assert.IsNull(dto.ProgressDelta);
        Assert.IsTrue(dto.PreviousWeight.HasValue);
        Assert.AreEqual(8d, dto.PreviousWeight.Value, 0.001d);
        Assert.IsNull(dto.CurrentWeight);
        Assert.IsNull(dto.WeightDelta);
    }
}
