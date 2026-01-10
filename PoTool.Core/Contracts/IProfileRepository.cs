using PoTool.Shared.Settings;

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
    /// Creates a new profile (Product Owner).
    /// </summary>
    Task<ProfileDto> CreateProfileAsync(
        string name,
        List<int> goalIds,
        ProfilePictureType pictureType = ProfilePictureType.Default,
        int defaultPictureId = 0,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing profile (Product Owner).
    /// </summary>
    Task<ProfileDto> UpdateProfileAsync(
        int id,
        string name,
        List<int> goalIds,
        ProfilePictureType? pictureType = null,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a profile by ID.
    /// </summary>
    Task<bool> DeleteProfileAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether any profiles exist.
    /// </summary>
    Task<bool> HasAnyProfileAsync(CancellationToken cancellationToken = default);
}
