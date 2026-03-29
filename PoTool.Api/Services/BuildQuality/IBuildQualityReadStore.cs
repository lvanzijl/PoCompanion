namespace PoTool.Api.Services.BuildQuality;

/// <summary>
/// Cache-backed analytical BuildQuality read store.
/// Owns BuildQuality scope composition and sprint metadata reads.
/// </summary>
public interface IBuildQualityReadStore
{
    Task<BuildQualityScopeSelection> GetScopeSelectionAsync(
        int productOwnerId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        int? repositoryId,
        int? pipelineDefinitionId,
        CancellationToken cancellationToken);

    Task<BuildQualityScopeSelection> GetScopeSelectionAsync(
        IReadOnlyList<int> productIds,
        DateTime? windowStartUtc,
        DateTime? windowEndUtc,
        int? repositoryId,
        int? pipelineDefinitionId,
        CancellationToken cancellationToken);

    Task<BuildQualitySprintWindow?> GetSprintWindowAsync(
        int sprintId,
        CancellationToken cancellationToken);
}

public sealed record BuildQualitySprintWindow(
    int Id,
    string Name,
    int TeamId,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc);
