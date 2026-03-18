using MudBlazor;
using PoTool.Client.Pages.Home;

namespace PoTool.Tests.Unit.Pages.Home;

[TestClass]
public sealed class SprintDeliveryDrillDownBreadcrumbBuilderTests
{
    [TestMethod]
    public void Build_ProductLevel_UsesProductsOverviewAsRoot()
    {
        var result = SprintDeliveryDrillDownBreadcrumbBuilder.Build(
            SprintDeliveryDrillDownLevel.Product,
            "Incident Response Control",
            null,
            null);

        Assert.HasCount(2, result);
        AssertBreadcrumb(result[0], "Products overview", Icons.Material.Filled.Dashboard, SprintDeliveryDrillDownLevel.ProductsOverview, isCurrent: false);
        AssertBreadcrumb(result[1], "Incident Response Control", Icons.Material.Filled.Inventory2, SprintDeliveryDrillDownLevel.Product, isCurrent: true);
    }

    [TestMethod]
    public void Build_EpicLevel_KeepsInPageHierarchyTargets()
    {
        var result = SprintDeliveryDrillDownBreadcrumbBuilder.Build(
            SprintDeliveryDrillDownLevel.Epic,
            "Incident Response Control",
            "Hardening epic",
            null);

        Assert.HasCount(3, result);
        AssertBreadcrumb(result[0], "Products overview", Icons.Material.Filled.Dashboard, SprintDeliveryDrillDownLevel.ProductsOverview, isCurrent: false);
        AssertBreadcrumb(result[1], "Incident Response Control", Icons.Material.Filled.Inventory2, SprintDeliveryDrillDownLevel.Product, isCurrent: false);
        AssertBreadcrumb(result[2], "Hardening epic", Icons.Material.Filled.AccountTree, SprintDeliveryDrillDownLevel.Epic, isCurrent: true);
    }

    [TestMethod]
    public void Build_FeatureLevel_AddsFeatureAsCurrentBreadcrumb()
    {
        var result = SprintDeliveryDrillDownBreadcrumbBuilder.Build(
            SprintDeliveryDrillDownLevel.Feature,
            "Incident Response Control",
            "Hardening epic",
            "Feature rollout");

        Assert.HasCount(4, result);
        AssertBreadcrumb(result[3], "Feature rollout", Icons.Material.Filled.FeaturedPlayList, SprintDeliveryDrillDownLevel.Feature, isCurrent: true);
    }

    private static void AssertBreadcrumb(
        SprintDeliveryDrillDownBreadcrumbSegment actual,
        string expectedText,
        string expectedIcon,
        SprintDeliveryDrillDownLevel expectedTargetLevel,
        bool isCurrent)
    {
        Assert.AreEqual(expectedText, actual.Text);
        Assert.AreEqual(expectedIcon, actual.Icon);
        Assert.AreEqual(expectedTargetLevel, actual.TargetLevel);
        Assert.AreEqual(isCurrent, actual.IsCurrent);
    }
}
