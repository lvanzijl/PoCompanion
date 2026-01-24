namespace PoTool.Core.Contracts;

/// <summary>
/// Provides the current ProductOwner (Profile) ID from request context.
/// This is used by middleware to determine which ProductOwner's cache state to check.
/// </summary>
public interface ICurrentProfileProvider
{
    /// <summary>
    /// Gets the current ProductOwner (Profile) ID from the request context.
    /// Returns null if no profile is active or available.
    /// </summary>
    Task<int?> GetCurrentProductOwnerIdAsync(CancellationToken cancellationToken = default);
}
