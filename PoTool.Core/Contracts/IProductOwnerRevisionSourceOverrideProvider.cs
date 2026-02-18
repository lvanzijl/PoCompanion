using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Provides optional per-product-owner revision source override.
/// </summary>
public interface IProductOwnerRevisionSourceOverrideProvider
{
    /// <summary>
    /// Returns a per-product-owner override when configured; otherwise null.
    /// </summary>
    Task<RevisionSource?> GetOverrideAsync(int productOwnerId, CancellationToken cancellationToken = default);
}
