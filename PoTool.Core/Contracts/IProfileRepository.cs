using PoTool.Core.Settings;

namespace PoTool.Core.Contracts;

/// <summary>
/// Repository interface for profile persistence.
/// </summary>
public interface IProfileRepository
{
    /// <summary>
    /// Gets all profiles.
    /// </summary>
    Task<IEnumerable<ProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a profile by ID.
    /// </summary>
    Task<ProfileDto?> GetProfileByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new profile.
    /// </summary>
    Task<ProfileDto> CreateProfileAsync(
        string name,
        List<string> areaPaths,
        string teamName,
        List<int> goalIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing profile.
    /// </summary>
    Task<ProfileDto> UpdateProfileAsync(
        int id,
        string name,
        List<string> areaPaths,
        string teamName,
        List<int> goalIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a profile by ID.
    /// </summary>
    Task<bool> DeleteProfileAsync(int id, CancellationToken cancellationToken = default);
}
