using PoTool.Shared.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Repository interface for team persistence.
/// </summary>
public interface ITeamRepository
{
    /// <summary>
    /// Gets all teams, optionally including archived ones.
    /// </summary>
    Task<IEnumerable<TeamDto>> GetAllTeamsAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a team by ID.
    /// </summary>
    Task<TeamDto?> GetTeamByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new team.
    /// </summary>
    Task<TeamDto> CreateTeamAsync(
        string name,
        string teamAreaPath,
        TeamPictureType pictureType,
        int defaultPictureId,
        string? customPicturePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing team.
    /// </summary>
    Task<TeamDto> UpdateTeamAsync(
        int id,
        string name,
        string teamAreaPath,
        TeamPictureType? pictureType,
        int? defaultPictureId,
        string? customPicturePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives or unarchives a team.
    /// </summary>
    Task<TeamDto> ArchiveTeamAsync(int id, bool isArchived, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a team and all its product links.
    /// </summary>
    Task<bool> DeleteTeamAsync(int id, CancellationToken cancellationToken = default);
}
