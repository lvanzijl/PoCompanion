using Mediator;
using Microsoft.Extensions.Configuration;
using PoTool.Api.Services;
using PoTool.Core.Contracts;
using PoTool.Shared.Settings;
using PoTool.Core.Settings.Queries;

namespace PoTool.Api.Handlers.Settings;

/// <summary>
/// Handler for retrieving startup readiness state.
/// Used by the Startup Orchestrator to determine where to route the user.
/// </summary>
public class GetStartupReadinessQueryHandler : IQueryHandler<GetStartupReadinessQuery, StartupReadinessDto>
{
    private readonly TfsConfigurationService _tfsConfigService;
    private readonly IProfileRepository _profileRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly bool _isMockDataEnabled;

    public GetStartupReadinessQueryHandler(
        TfsConfigurationService tfsConfigService,
        IProfileRepository profileRepository,
        ISettingsRepository settingsRepository,
        IConfiguration configuration)
    {
        _tfsConfigService = tfsConfigService;
        _profileRepository = profileRepository;
        _settingsRepository = settingsRepository;
        _isMockDataEnabled = configuration.GetValue<bool>("TfsIntegration:UseMockClient", false);
    }

    public async ValueTask<StartupReadinessDto> Handle(GetStartupReadinessQuery query, CancellationToken cancellationToken)
    {
        // Get TFS configuration state
        var tfsConfig = await _tfsConfigService.GetConfigEntityAsync(cancellationToken);
        var hasSavedTfsConfig = tfsConfig != null && !string.IsNullOrWhiteSpace(tfsConfig.Url);
        var hasTestedConnection = tfsConfig?.HasTestedConnectionSuccessfully ?? false;
        var hasVerifiedTfsApi = tfsConfig?.HasVerifiedTfsApiSuccessfully ?? false;

        // Get profile state
        var hasAnyProfile = await _profileRepository.HasAnyProfileAsync(cancellationToken);
        var settings = await _settingsRepository.GetSettingsAsync(cancellationToken);
        var activeProfileId = settings?.ActiveProfileId;

        // Determine the missing requirement message based on the startup routing logic
        string? missingMessage = null;

        if (!_isMockDataEnabled)
        {
            if (!hasSavedTfsConfig)
            {
                missingMessage = "Configuration required: Please configure your TFS/Azure DevOps connection.";
            }
            else if (!hasTestedConnection)
            {
                missingMessage = "Test Connection required: Please test your TFS connection.";
            }
            else if (!hasVerifiedTfsApi)
            {
                missingMessage = "Verify TFS API required: Please verify your TFS API connection.";
            }
            else if (!hasAnyProfile)
            {
                missingMessage = "Profile required: Please create your first profile.";
            }
            else if (activeProfileId == null)
            {
                missingMessage = "Profile selection required: Please select an active profile.";
            }
        }

        return new StartupReadinessDto(
            IsMockDataEnabled: _isMockDataEnabled,
            HasSavedTfsConfig: hasSavedTfsConfig,
            HasTestedConnectionSuccessfully: hasTestedConnection,
            HasVerifiedTfsApiSuccessfully: hasVerifiedTfsApi,
            HasAnyProfile: hasAnyProfile,
            ActiveProfileId: activeProfileId,
            MissingRequirementMessage: missingMessage
        );
    }
}
