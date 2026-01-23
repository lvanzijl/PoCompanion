using PoTool.Client.ApiClient;
using ProfilePictureType = PoTool.Shared.Settings.ProfilePictureType;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing user profiles via the API.
/// </summary>
public class ProfileService : IProfileService
{
    private readonly IProfilesClient _profilesClient;
    private int? _cachedActiveProfileId;

    public ProfileService(IProfilesClient profilesClient)
    {
        _profilesClient = profilesClient;
    }

    /// <summary>
    /// Gets all profiles.
    /// </summary>
    public async Task<IEnumerable<ProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        return await _profilesClient.GetAllProfilesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a profile by ID.
    /// </summary>
    public async Task<ProfileDto?> GetProfileByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _profilesClient.GetProfileByIdAsync(id, cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the currently active profile.
    /// </summary>
    public async Task<ProfileDto?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _profilesClient.GetActiveProfileAsync(cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a new profile (Product Owner).
    /// </summary>
    public async Task<ProfileDto> CreateProfileAsync(
        string name,
        List<int> goalIds,
        ProfilePictureType pictureType = ProfilePictureType.Default,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default)
    {
        // If no picture ID is specified and using default type, randomize it
        var pictureId = defaultPictureId ?? Random.Shared.Next(0, 64);

        var request = new CreateProfileRequest
        {
            Name = name,
            GoalIds = goalIds,
            PictureType = (ApiClient.ProfilePictureType)pictureType,
            DefaultPictureId = pictureId,
            CustomPicturePath = customPicturePath
        };

        return await _profilesClient.CreateProfileAsync(request, cancellationToken);
    }

    /// <summary>
    /// Updates an existing profile (Product Owner).
    /// </summary>
    public async Task<ProfileDto> UpdateProfileAsync(
        int id,
        string name,
        List<int> goalIds,
        ProfilePictureType? pictureType = null,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateProfileRequest
        {
            Name = name,
            GoalIds = goalIds,
            PictureType = pictureType.HasValue ? (ApiClient.ProfilePictureType?)pictureType.Value : null,
            DefaultPictureId = defaultPictureId,
            CustomPicturePath = customPicturePath
        };

        return await _profilesClient.UpdateProfileAsync(id, request, cancellationToken);
    }

    /// <summary>
    /// Deletes a profile.
    /// </summary>
    public async Task<bool> DeleteProfileAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _profilesClient.DeleteProfileAsync(id, cancellationToken);
            return true;
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return false;
        }
    }

    /// <summary>
    /// Sets the active profile.
    /// </summary>
    public async Task<SettingsDto> SetActiveProfileAsync(int? profileId, CancellationToken cancellationToken = default)
    {
        var request = new SetActiveProfileRequest
        {
            ProfileId = profileId
        };

        return await _profilesClient.SetActiveProfileAsync(request, cancellationToken);
    }

    /// <summary>
    /// Creates a profile (Product Owner) and sets it as active.
    /// </summary>
    public async Task<ProfileDto> CreateAndActivateProfileAsync(
        string name,
        List<int> goalIds,
        ProfilePictureType pictureType = ProfilePictureType.Default,
        int? defaultPictureId = null,
        string? customPicturePath = null,
        CancellationToken cancellationToken = default)
    {
        // Create the profile
        var profile = await CreateProfileAsync(name, goalIds,
            pictureType, defaultPictureId, customPicturePath, cancellationToken);

        // Set it as active
        await SetActiveProfileAsync(profile.Id, cancellationToken);

        // Update cached value
        _cachedActiveProfileId = profile.Id;

        return profile;
    }

    /// <inheritdoc />
    public int? GetActiveProfileId()
    {
        return _cachedActiveProfileId;
    }

    /// <inheritdoc />
    public bool IsActiveProfileValid()
    {
        return _cachedActiveProfileId.HasValue;
    }

    /// <inheritdoc />
    public void SetCachedActiveProfileId(int? profileId)
    {
        _cachedActiveProfileId = profileId;
    }
}
