using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Repository contract for managing repository configurations.
/// </summary>
public interface IRepositoryConfigRepository
{
    /// <summary>
    /// Gets all repositories configured for a specific product.
    /// </summary>
    Task<IEnumerable<RepositoryDto>> GetRepositoriesByProductAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets repositories for multiple products in a single query.
    /// </summary>
    Task<Dictionary<int, List<RepositoryDto>>> GetRepositoriesByProductIdsAsync(List<int> productIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all repositories in the system.
    /// </summary>
    Task<IEnumerable<RepositoryDto>> GetAllRepositoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new repository configuration.
    /// </summary>
    Task<RepositoryDto> CreateRepositoryAsync(int productId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a repository configuration.
    /// </summary>
    Task DeleteRepositoryAsync(int repositoryId, CancellationToken cancellationToken = default);
}
