using PoTool.Client.Models;
using PoTool.Shared.Settings;

namespace PoTool.Client.Helpers;

public static class GlobalProductSelectionHelper
{
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
}
