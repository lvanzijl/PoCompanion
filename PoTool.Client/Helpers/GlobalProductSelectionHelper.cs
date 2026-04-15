using PoTool.Client.Models;
using PoTool.Shared.Settings;

namespace PoTool.Client.Helpers;

public static class GlobalProductSelectionHelper
{
    public static ProductScopeResolution ResolveEffectiveScope(FilterState state, IReadOnlyCollection<ProductDto> availableProducts)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(availableProducts);

        if (state.ProductIds.Count == 0)
        {
            var allProducts = availableProducts.ToList();
            return new ProductScopeResolution(
                RequestedProductIds: Array.Empty<int>(),
                EffectiveProductIds: allProducts.Select(product => product.Id).ToArray(),
                Products: allProducts,
                InvalidRequestedProductIds: Array.Empty<int>(),
                Reason: null);
        }

        var invalidRequestedIds = ResolveUnavailableSelectedProductIds(state, availableProducts);
        if (invalidRequestedIds.Count > 0)
        {
            return new ProductScopeResolution(
                RequestedProductIds: state.ProductIds.Distinct().ToArray(),
                EffectiveProductIds: Array.Empty<int>(),
                Products: Array.Empty<ProductDto>(),
                InvalidRequestedProductIds: invalidRequestedIds,
                Reason: invalidRequestedIds.Count == 1
                    ? $"Selected product '{invalidRequestedIds[0]}' is not available in the current scope."
                    : $"Selected products '{string.Join(", ", invalidRequestedIds)}' are not available in the current scope.");
        }

        var scopedProducts = ResolveScopedProducts(state, availableProducts.ToList());
        return new ProductScopeResolution(
            RequestedProductIds: state.ProductIds.Distinct().ToArray(),
            EffectiveProductIds: scopedProducts.Select(product => product.Id).ToArray(),
            Products: scopedProducts,
            InvalidRequestedProductIds: Array.Empty<int>(),
            Reason: null);
    }

    public static IReadOnlyList<ProductDto> ResolveScopedProducts(FilterState state, IReadOnlyList<ProductDto> availableProducts)
    {
        if (state.ProductIds.Count == 0)
        {
            return availableProducts;
        }

        var selectedIds = state.ProductIds.ToHashSet();
        return availableProducts
            .Where(product => selectedIds.Contains(product.Id))
            .ToList();
    }

    public static int? ResolveSingleProductId(FilterState state, IReadOnlyCollection<ProductDto> availableProducts)
    {
        if (state.ProductIds.Count != 1)
        {
            return null;
        }

        var selectedProductId = state.ProductIds[0];
        return availableProducts.Any(product => product.Id == selectedProductId)
            ? selectedProductId
            : null;
    }

    public static IReadOnlyList<int> ResolveUnavailableSelectedProductIds(
        FilterState state,
        IReadOnlyCollection<ProductDto> availableProducts)
    {
        if (state.ProductIds.Count == 0)
        {
            return Array.Empty<int>();
        }

        var availableProductIds = availableProducts
            .Select(product => product.Id)
            .ToHashSet();

        return state.ProductIds
            .Where(productId => !availableProductIds.Contains(productId))
            .Distinct()
            .ToArray();
    }
}

public sealed record ProductScopeResolution(
    IReadOnlyList<int> RequestedProductIds,
    IReadOnlyList<int> EffectiveProductIds,
    IReadOnlyList<ProductDto> Products,
    IReadOnlyList<int> InvalidRequestedProductIds,
    string? Reason)
{
    public bool HasInvalidSelection => InvalidRequestedProductIds.Count > 0;

    public int? SingleEffectiveProductId => EffectiveProductIds.Count == 1
        ? EffectiveProductIds[0]
        : null;
}
