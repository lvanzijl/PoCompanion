using PoTool.Core.Contracts;
using PoTool.Shared.Settings;

namespace PoTool.Api.Services;

/// <summary>
/// Service for applying profile-based filtering to queries.
/// </summary>
public class ProfileFilterService
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IProfileRepository _profileRepository;
    private readonly ILogger<ProfileFilterService> _logger;

    public ProfileFilterService(
        ISettingsRepository settingsRepository,
        IProfileRepository profileRepository,
        ILogger<ProfileFilterService> logger)
    {
        _settingsRepository = settingsRepository;
        _profileRepository = profileRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets the area paths from the active profile, or null if no profile is active.
    /// NOTE: Profiles no longer have direct area paths. Area paths are now defined through Products and Teams.
    /// This method returns null to disable profile-based area path filtering.
    /// </summary>
    public async Task<List<string>?> GetActiveProfileAreaPathsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);

            if (settings?.ActiveProfileId == null)
            {
                _logger.LogDebug("No active profile set, skipping area path filtering");
                return null;
            }

            var profile = await _profileRepository.GetProfileByIdAsync(settings.ActiveProfileId.Value, cancellationToken);

            if (profile == null)
            {
                _logger.LogWarning("Active profile ID {ProfileId} not found", settings.ActiveProfileId.Value);
                return null;
            }

            // Profiles no longer have area paths - they are now defined through Products and Teams
            _logger.LogDebug("Profile-based area path filtering is disabled. Use Product/Team filtering instead.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active profile area paths");
            // Return null to allow queries to proceed without filtering
            return null;
        }
    }

    /// <summary>
    /// Checks if a work item matches any of the profile's area paths.
    /// </summary>
    public bool MatchesAreaPathFilter(string workItemAreaPath, List<string>? profileAreaPaths)
    {
        if (profileAreaPaths == null || profileAreaPaths.Count == 0)
        {
            // No filter means include everything
            return true;
        }

        // Check if work item area path starts with any of the profile's area paths
        // This allows hierarchical matching (e.g., "Project\Product" matches "Project\Product\Feature")
        return profileAreaPaths.Any(profilePath =>
            workItemAreaPath.Equals(profilePath, StringComparison.OrdinalIgnoreCase) ||
            workItemAreaPath.StartsWith(profilePath + "\\", StringComparison.OrdinalIgnoreCase));
    }
}
