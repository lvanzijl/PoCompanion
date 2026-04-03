using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Repository interface for sprint persistence.
/// </summary>
public interface ISprintRepository
{
    /// <summary>
    /// Gets all sprints across all teams, ordered by start date (nulls last).
    /// Used for iteration path matching when team context is not available.
    /// </summary>
    Task<IEnumerable<SprintDto>> GetAllSprintsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sprints for a specific team, ordered by start date (nulls last).
    /// </summary>
    Task<IEnumerable<SprintDto>> GetSprintsForTeamAsync(int teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current sprint for a team using only strict team-scoped current-sprint semantics.
    /// Returns null when the team has no sprint marked current and no sprint whose active date window overlaps now.
    /// </summary>
    Task<SprintDto?> GetCurrentSprintForTeamAsync(int teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts sprints for a team from TFS sync data.
    /// Performs insert for new sprints, updates existing ones, and optionally removes stale ones.
    /// </summary>
    Task UpsertSprintsForTeamAsync(
        int teamId,
        IEnumerable<TeamIterationDto> iterations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes sprints that haven't been synced recently (stale data cleanup).
    /// </summary>
    Task RemoveStaleSprintsAsync(
        int teamId,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken = default);
}
