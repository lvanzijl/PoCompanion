using Mediator;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for getting the active profile.
/// </summary>
public class GetActiveProfileQueryHandler : IQueryHandler<GetActiveProfileQuery, ProfileDto?>
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IProfileRepository _profileRepository;

    public GetActiveProfileQueryHandler(
        ISettingsRepository settingsRepository,
        IProfileRepository profileRepository)
    {
        _settingsRepository = settingsRepository;
        _profileRepository = profileRepository;
    }

    public async ValueTask<ProfileDto?> Handle(GetActiveProfileQuery query, CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);

        if (settings?.ActiveProfileId == null)
        {
            return null;
        }

        return await _profileRepository.GetProfileByIdAsync(settings.ActiveProfileId.Value, cancellationToken);
    }
}
