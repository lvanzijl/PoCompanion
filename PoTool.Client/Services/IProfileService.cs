using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Interface for profile service operations.
/// </summary>
public interface IProfileService
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
    /// Gets the currently active profile.
    /// </summary>
    Task<ProfileDto?> GetActiveProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new profile (Product Owner).
    /// </summary>
    Task<ProfileDto> CreateProfileAsync(
        string name,
        List<int> goalIds,
        Shared.Settings.ProfilePictureType pictureType = Shared.Settings.ProfilePictureType.Default,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing profile (Product Owner).
    /// </summary>
    Task<ProfileDto> UpdateProfileAsync(
        int id,
        string name,
        List<int> goalIds,
        Shared.Settings.ProfilePictureType? pictureType = null,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a profile.
    /// </summary>
    Task<bool> DeleteProfileAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active profile.
    /// </summary>
    Task<SettingsDto> SetActiveProfileAsync(int? profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a profile (Product Owner) and sets it as active.
    /// </summary>
    Task<ProfileDto> CreateAndActivateProfileAsync(
        string name,
        List<int> goalIds,
        Shared.Settings.ProfilePictureType pictureType = Shared.Settings.ProfilePictureType.Default,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active profile ID from cached state (synchronous check).
    /// Returns null if no profile is cached/active.
    /// </summary>
    int? GetActiveProfileId();

    /// <summary>
    /// Checks if the active profile is valid (synchronous check based on cached state).
    /// </summary>
    bool IsActiveProfileValid();

    /// <summary>
    /// Sets the cached active profile ID (called after successful profile operations).
    /// </summary>
    void SetCachedActiveProfileId(int? profileId);
}
