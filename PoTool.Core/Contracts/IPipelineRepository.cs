using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines;

namespace PoTool.Core.Contracts;

/// <summary>
/// Interface for pipeline repository operations.
/// Handles local caching of pipeline data.
/// Uses in-memory storage for V1 - pipeline data is exploratory and read-only.
/// </summary>
public interface IPipelineRepository
{
    /// <summary>
    /// Retrieves all cached pipelines.
    /// </summary>
    Task<IEnumerable<PipelineDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific pipeline by ID.
    /// </summary>
    Task<PipelineDto?> GetByIdAsync(int pipelineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pipeline runs for a specific pipeline.
    /// </summary>
    Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all cached pipeline runs.
    /// </summary>
    Task<IEnumerable<PipelineRunDto>> GetAllRunsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pipeline runs for multiple pipelines with optional filtering.
    /// More efficient than GetAllRunsAsync when working with specific pipelines.
    /// </summary>
    /// <param name="pipelineIds">Collection of pipeline IDs to get runs for.</param>
    /// <param name="branchName">Optional branch name to filter by.</param>
    /// <param name="minFinishTime">Optional minimum finish time to filter by.</param>
    /// <param name="top">Maximum number of runs to retrieve per pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IEnumerable<PipelineRunDto>> GetRunsForPipelinesAsync(
        IEnumerable<int> pipelineIds,
        string? branchName = null,
        DateTimeOffset? minFinishTime = null,
        DateTimeOffset? maxFinishTime = null,
        IReadOnlyList<PoTool.Core.Pipelines.Filters.PipelineBranchScope>? branchScope = null,
        int top = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all cached pipeline data.
    /// </summary>
    Task ClearAllAsync(CancellationToken cancellationToken = default);

    // ============================================
    // PIPELINE DEFINITION METHODS
    // ============================================

    /// <summary>
    /// Retrieves pipeline definitions for a specific product.
    /// </summary>
    Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByProductIdAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pipeline definitions for a specific repository.
    /// </summary>
    Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves pipeline definitions for products and repositories.
    /// Performs upsert operation - updates existing or inserts new definitions.
    /// Removes definitions that no longer exist in TFS.
    /// </summary>
    /// <param name="definitions">Pipeline definitions to save.</param>
    /// <param name="productIds">Product IDs that were synced (used to identify stale entries).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveDefinitionsAsync(
        IEnumerable<PipelineDefinitionDto> definitions,
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default);
}
