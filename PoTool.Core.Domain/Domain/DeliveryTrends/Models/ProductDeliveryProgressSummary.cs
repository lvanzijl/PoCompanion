namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Represents the product-level delivery progress summary derived from epic progress rollups.
/// </summary>
public sealed record ProductDeliveryProgressSummary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductDeliveryProgressSummary"/> class.
    /// </summary>
    public ProductDeliveryProgressSummary(
        int productId,
        int scopeChangeEffort,
        int completedFeatureCount)
    {
        DeliveryTrendModelValidation.ValidatePositiveId(productId, nameof(productId), "Product ID");
        DeliveryTrendModelValidation.ValidateCount(completedFeatureCount, nameof(completedFeatureCount), "Completed feature count");

        ProductId = productId;
        ScopeChangeEffort = scopeChangeEffort;
        CompletedFeatureCount = completedFeatureCount;
    }

    public int ProductId { get; }

    public int ScopeChangeEffort { get; }

    public int CompletedFeatureCount { get; }
}
