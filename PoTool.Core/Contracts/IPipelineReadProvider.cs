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
    /// Retrieves pipeline runs for a specific pipeline from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineRunDto>> GetRunsAsync(int pipelineId, int top = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all pipeline runs from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineRunDto>> GetAllRunsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all pipeline definitions from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineDefinitionDto>> GetAllDefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pipeline definitions for a specific product from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByProductIdAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves pipeline definitions for a specific repository from the configured data source.
    /// </summary>
    Task<IEnumerable<PipelineDefinitionDto>> GetDefinitionsByRepositoryIdAsync(int repositoryId, CancellationToken cancellationToken = default);
}
