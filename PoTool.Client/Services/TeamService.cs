using PoTool.Client.ApiClient;
using TeamPictureType = PoTool.Shared.Settings.TeamPictureType;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing teams via the API.
/// </summary>
public class TeamService
{
    private readonly ITeamsClient _teamsClient;

    public TeamService(ITeamsClient teamsClient)
    {
        _teamsClient = teamsClient;
    }

    /// <summary>
    /// Gets all teams.
    /// </summary>
    /// <param name="includeArchived">If true, includes archived teams. Default is false.</param>
    public async Task<IEnumerable<TeamDto>> GetAllTeamsAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        return await _teamsClient.GetAllTeamsAsync(includeArchived, cancellationToken);
    }

    /// <summary>
    /// Gets a team by ID.
    /// </summary>
    public async Task<TeamDto?> GetTeamByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _teamsClient.GetTeamByIdAsync(id, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a new team.
    /// </summary>
    public async Task<TeamDto> CreateTeamAsync(
        string name,
        string teamAreaPath,
        TeamPictureType pictureType = TeamPictureType.Default,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default)
    {
        // If no picture ID is specified and using default type, randomize it
        var pictureId = defaultPictureId ?? Random.Shared.Next(0, 64);

        var request = new CreateTeamRequest
        {
            Name = name,
            TeamAreaPath = teamAreaPath,
            PictureType = (ApiClient.TeamPictureType)pictureType,
            DefaultPictureId = pictureId,
            CustomPicturePath = customPicturePath
        };

        return await _teamsClient.CreateTeamAsync(request, cancellationToken);
    }

    /// <summary>
    /// Updates an existing team.
    /// </summary>
    public async Task<TeamDto> UpdateTeamAsync(
        int id,
        string name,
        string teamAreaPath,
        TeamPictureType? pictureType = null,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateTeamRequest
        {
            Name = name,
            TeamAreaPath = teamAreaPath,
            PictureType = pictureType.HasValue ? (ApiClient.TeamPictureType?)pictureType.Value : null,
            DefaultPictureId = defaultPictureId,
            CustomPicturePath = customPicturePath
        };

        return await _teamsClient.UpdateTeamAsync(id, request, cancellationToken);
    }

    /// <summary>
    /// Archives or unarchives a team.
    /// </summary>
    public async Task<TeamDto?> ArchiveTeamAsync(int id, bool isArchived, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ArchiveTeamRequest
            {
                IsArchived = isArchived
            };

            return await _teamsClient.ArchiveTeamAsync(id, request, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }
}
