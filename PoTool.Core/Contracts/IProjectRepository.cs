using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Repository interface for project persistence.
/// </summary>
public interface IProjectRepository
{
    /// <summary>
    /// Gets all projects in the system.
    /// </summary>
    Task<IEnumerable<ProjectDto>> GetAllProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a project by alias or internal identifier.
    /// </summary>
    Task<ProjectDto?> GetProjectByAliasOrIdAsync(string aliasOrId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all products that belong to a project resolved by alias or internal identifier.
    /// </summary>
    Task<IEnumerable<ProductDto>> GetProjectProductsAsync(string aliasOrId, CancellationToken cancellationToken = default);
}
