using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for managing user profiles via the API.
/// </summary>
public class ProfileService
{
    private readonly IProfilesClient _profilesClient;

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
    /// Creates a new profile.
    /// </summary>
    public async Task<ProfileDto> CreateProfileAsync(
        string name,
        List<string> areaPaths,
        string teamName,
        List<int> goalIds,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateProfileRequest
        {
            Name = name,
            AreaPaths = areaPaths,
            TeamName = teamName,
            GoalIds = goalIds
        };

        return await _profilesClient.CreateProfileAsync(request, cancellationToken);
    }

    /// <summary>
    /// Updates an existing profile.
    /// </summary>
    public async Task<ProfileDto> UpdateProfileAsync(
        int id,
        string name,
        List<string> areaPaths,
        string teamName,
        List<int> goalIds,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateProfileRequest
        {
            Name = name,
            AreaPaths = areaPaths,
            TeamName = teamName,
            GoalIds = goalIds
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
    /// Creates a profile and sets it as active.
    /// </summary>
    public async Task<ProfileDto> CreateAndActivateProfileAsync(
        string name,
        List<string> areaPaths,
        string teamName,
        List<int> goalIds,
        CancellationToken cancellationToken = default)
    {
        // Create the profile
        var profile = await CreateProfileAsync(name, areaPaths, teamName, goalIds, cancellationToken);

        // Set it as active
        await SetActiveProfileAsync(profile.Id, cancellationToken);

        return profile;
    }
}
