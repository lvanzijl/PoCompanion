using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for sprint-related operations.
/// Wraps the generated API client for sprint endpoints.
/// </summary>
public class SprintService
{
    private readonly ISprintsClient _client;

    public SprintService(ISprintsClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Gets all sprints for a specific team.
    /// </summary>
    /// <param name="teamId">The team ID.</param>
    /// <returns>List of sprints ordered by start date descending.</returns>
    public async Task<IEnumerable<SprintDto>> GetSprintsForTeamAsync(int teamId, CancellationToken cancellationToken = default)
    {
        return await _client.GetSprintsForTeamAsync(teamId, cancellationToken);
    }

    /// <summary>
    /// Gets the current sprint for a specific team.
    /// </summary>
    /// <param name="teamId">The team ID.</param>
    /// <returns>The current sprint, or null if none found.</returns>
    public async Task<SprintDto?> GetCurrentSprintForTeamAsync(int teamId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetCurrentSprintForTeamAsync(teamId, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            // No current sprint found
            return null;
        }
    }
}
