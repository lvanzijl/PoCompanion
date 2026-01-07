using System.Net.Http.Json;
using System.Text.Json;

namespace PoTool.Client.Services;

/// <summary>
/// Service that orchestrates startup routing based on TFS configuration and profile state.
/// Implements the decision tree from User_landing_v2.md.
/// </summary>
public class StartupOrchestratorService : IStartupOrchestratorService
{
    private readonly HttpClient _httpClient;
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StartupOrchestratorService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<StartupReadinessDto?> GetStartupReadinessAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/startup/readiness", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
            return await response.Content.ReadFromJsonAsync<StartupReadinessDto>(_jsonOptions, cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public StartupRoutingResult DetermineRoute(StartupReadinessDto readiness)
    {
        // Decision tree from User_landing_v2.md:
        //
        // IF IsMockDataEnabled:
        // - Route to Profiles Home or existing home; do not block navigation.
        //
        // ELSE (Real TFS mode):
        // - if !HasSavedTfsConfig → Configuration ("Configuration required")
        // - else if !HasTestedConnectionSuccessfully → Configuration ("Test Connection required")
        // - else if !HasVerifiedTfsApiSuccessfully → Configuration ("Verify TFS API required")
        // - else if !HasAnyProfile → Create first profile
        // - else if ActiveProfileId is null → Profiles Home (force selection)
        // - else → Profiles Home

        if (readiness.IsMockDataEnabled)
        {
            // Mock mode: app is usable, route to profiles home
            return new StartupRoutingResult(
                Route: StartupRoute.ProfilesHome,
                Message: null,
                IsAppUsable: true
            );
        }

        // Real TFS mode - check readiness flags in order
        if (!readiness.HasSavedTfsConfig)
        {
            return new StartupRoutingResult(
                Route: StartupRoute.Configuration,
                Message: "Configuration required: Please configure your TFS/Azure DevOps connection.",
                IsAppUsable: false
            );
        }

        if (!readiness.HasTestedConnectionSuccessfully)
        {
            return new StartupRoutingResult(
                Route: StartupRoute.Configuration,
                Message: "Test Connection required: Please test your TFS connection to verify it works.",
                IsAppUsable: false
            );
        }

        if (!readiness.HasVerifiedTfsApiSuccessfully)
        {
            return new StartupRoutingResult(
                Route: StartupRoute.Configuration,
                Message: "Verify TFS API required: Please verify your TFS API capabilities.",
                IsAppUsable: false
            );
        }

        if (!readiness.HasAnyProfile)
        {
            return new StartupRoutingResult(
                Route: StartupRoute.CreateFirstProfile,
                Message: "Profile required: Please create your first profile to get started.",
                IsAppUsable: false
            );
        }

        if (readiness.ActiveProfileId == null)
        {
            return new StartupRoutingResult(
                Route: StartupRoute.ProfilesHome,
                Message: "Please select a profile to continue.",
                IsAppUsable: false
            );
        }

        // All requirements met
        return new StartupRoutingResult(
            Route: StartupRoute.ProfilesHome,
            Message: null,
            IsAppUsable: true
        );
    }

    /// <inheritdoc />
    public bool IsFeaturePageAccessible(StartupReadinessDto readiness)
    {
        // In mock mode, feature pages are always accessible
        if (readiness.IsMockDataEnabled)
        {
            return true;
        }

        // In real mode, feature pages require:
        // - HasVerifiedTfsApiSuccessfully
        // - HasAnyProfile
        // - ActiveProfileId != null
        return readiness.HasVerifiedTfsApiSuccessfully 
               && readiness.HasAnyProfile 
               && readiness.ActiveProfileId != null;
    }
}
