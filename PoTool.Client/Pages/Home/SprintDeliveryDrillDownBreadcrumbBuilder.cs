using MudBlazor;

namespace PoTool.Client.Pages.Home;

public enum SprintDeliveryDrillDownLevel
{
    ProductsOverview,
    Product,
    Epic,
    Feature
}

public sealed record SprintDeliveryDrillDownBreadcrumbSegment(
    string Text,
    string Icon,
    SprintDeliveryDrillDownLevel TargetLevel,
    bool IsCurrent);

public static class SprintDeliveryDrillDownBreadcrumbBuilder
{
    public static IReadOnlyList<SprintDeliveryDrillDownBreadcrumbSegment> Build(
        SprintDeliveryDrillDownLevel level,
        string? productName,
        string? epicName,
        string? featureName)
    {
        var breadcrumbs = new List<SprintDeliveryDrillDownBreadcrumbSegment>
        {
            new(
                "Products overview",
                Icons.Material.Filled.Dashboard,
                SprintDeliveryDrillDownLevel.ProductsOverview,
                level == SprintDeliveryDrillDownLevel.ProductsOverview)
        };

        if (level >= SprintDeliveryDrillDownLevel.Product && !string.IsNullOrWhiteSpace(productName))
        {
            breadcrumbs.Add(new SprintDeliveryDrillDownBreadcrumbSegment(
                productName,
                Icons.Material.Filled.Inventory2,
                SprintDeliveryDrillDownLevel.Product,
                level == SprintDeliveryDrillDownLevel.Product));
        }

        if (level >= SprintDeliveryDrillDownLevel.Epic && !string.IsNullOrWhiteSpace(epicName))
        {
            breadcrumbs.Add(new SprintDeliveryDrillDownBreadcrumbSegment(
                epicName,
                Icons.Material.Filled.AccountTree,
                SprintDeliveryDrillDownLevel.Epic,
                level == SprintDeliveryDrillDownLevel.Epic));
        }

        if (level >= SprintDeliveryDrillDownLevel.Feature && !string.IsNullOrWhiteSpace(featureName))
        {
            breadcrumbs.Add(new SprintDeliveryDrillDownBreadcrumbSegment(
                featureName,
                Icons.Material.Filled.FeaturedPlayList,
                SprintDeliveryDrillDownLevel.Feature,
                true));
        }

        return breadcrumbs;
    }
}
