using PoTool.Shared.Pipelines;
using PoTool.Core.Pipelines;

namespace PoTool.Core.Contracts;

/// <summary>
/// Provider for reading pipeline data from the configured data source.
/// Implementations select between Live (TFS direct) or Cached (repository) based on mode.
/// </summary>
public interface IPipelineReadProvider
{
    /// <summary>
    /// Retrieves all pipelines from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific pipeline by ID from the configured data source.
    /// </summary>
    Task<PipelineDto?> GetByIdAsync(int pipelineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves specific pipelines by ID from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineDto>> GetByIdsAsync(IEnumerable<int> pipelineIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pipeline runs for a specific pipeline from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all pipeline runs from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineRunDto>> GetAllRunsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pipeline runs for multiple pipelines with optional filtering.
    /// More efficient than GetAllRunsAsync when working with specific pipelines.
    /// </summary>
    /// <param name="pipelineIds">Collection of pipeline IDs to get runs for.</param>
    /// <param name="branchName">Optional branch name to filter by (e.g., "refs/heads/main").</param>
    /// <param name="minStartTime">Optional minimum start time to filter by.</param>
    /// <param name="top">Maximum number of runs to retrieve per pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IEnumerable<PipelineRunDto>> GetRunsForPipelinesAsync(
        IEnumerable<int> pipelineIds,
        string? branchName = null,
        DateTimeOffset? minStartTime = null,
        DateTimeOffset? maxStartTime = null,
        IReadOnlyList<PoTool.Core.Pipelines.Filters.PipelineBranchScope>? branchScope = null,
        int top = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pipeline definitions for a specific product from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByProductIdAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pipeline definitions for a specific repository from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default);
}
