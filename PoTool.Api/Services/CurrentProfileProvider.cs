using PoTool.Core.Contracts;

namespace PoTool.Api.Services;

/// <summary>
/// Provides the current ProductOwner (Profile) ID from the active settings.
/// Used by middleware to determine which ProductOwner's cache state to check.
/// </summary>
public sealed class CurrentProfileProvider : ICurrentProfileProvider
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<CurrentProfileProvider> _logger;

    public CurrentProfileProvider(
        ISettingsRepository settingsRepository,
        ILogger<CurrentProfileProvider> logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current ProductOwner (Profile) ID from the active settings.
    /// Returns null if no profile is active.
    /// </summary>
    public async Task<int?> GetCurrentProductOwnerIdAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
            var activeProfileId = settings?.ActiveProfileId;

            if (activeProfileId == null)
            {
                _logger.LogDebug("No active profile set in settings");
            }
            else
            {
                _logger.LogDebug("Current active ProductOwner ID: {ProductOwnerId}", activeProfileId.Value);
            }

            return activeProfileId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current ProductOwner ID from settings");
            return null;
        }
    }
}
