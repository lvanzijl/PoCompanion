using System.Web;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Client.Pages.Beta;

/// <summary>
/// Base class for Beta workspace pages providing shared functionality
/// for context propagation and navigation.
/// </summary>
public abstract class BetaWorkspaceBase : ComponentBase
{
    [Inject]
    protected IProfileService ProfileService { get; set; } = default!;
    
    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;
    
    [Inject]
    protected ISnackbar Snackbar { get; set; } = default!;

    /// <summary>
    /// Product ID for context propagation (parsed from URL).
    /// </summary>
    protected int? ProductId { get; set; }
    
    /// <summary>
    /// Team ID for context propagation (parsed from URL).
    /// </summary>
    protected int? TeamId { get; set; }

    /// <summary>
    /// Parses query parameters from the current URL for context propagation.
    /// Extracts productId and teamId if present.
    /// </summary>
    protected virtual void ParseContextQueryParameters()
    {
        var uri = new Uri(NavigationManager.Uri);
        var queryParams = HttpUtility.ParseQueryString(uri.Query);
        
        if (int.TryParse(queryParams["productId"], out var productId))
        {
            ProductId = productId;
        }
        
        if (int.TryParse(queryParams["teamId"], out var teamId))
        {
            TeamId = teamId;
        }
    }

    /// <summary>
    /// Builds a query string that includes context parameters (productId, teamId)
    /// along with any additional parameters.
    /// </summary>
    /// <param name="additionalParams">Additional query parameters to include (e.g., "validationType=missing-effort")</param>
    /// <returns>Query string starting with "?" or empty string if no parameters</returns>
    protected string BuildContextQuery(string? additionalParams = null)
    {
        var parameters = new List<string>();
        
        if (ProductId.HasValue)
        {
            parameters.Add($"productId={ProductId.Value}");
        }
        
        if (TeamId.HasValue)
        {
            parameters.Add($"teamId={TeamId.Value}");
        }
        
        if (!string.IsNullOrEmpty(additionalParams))
        {
            parameters.Add(additionalParams);
        }
        
        return parameters.Count > 0 ? "?" + string.Join("&", parameters) : "";
    }
    
    /// <summary>
    /// Checks if the user has a valid profile selected and redirects to profile selection if not.
    /// </summary>
    /// <param name="returnUrl">The URL to return to after profile selection.</param>
    /// <returns>True if profile is valid, false if redirecting to profile selection.</returns>
    protected async Task<bool> EnsureProfileAsync(string returnUrl)
    {
        var activeProfile = await ProfileService.GetActiveProfileAsync();
        if (activeProfile == null)
        {
            NavigationManager.NavigateTo($"{WorkspaceRoutes.Profiles}?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return false;
        }
        
        ProfileService.SetCachedActiveProfileId(activeProfile.Id);
        return true;
    }
    
    /// <summary>
    /// Navigates to the Beta Home page.
    /// </summary>
    protected void NavigateToBetaHome()
    {
        NavigationManager.NavigateTo(WorkspaceRoutes.BetaHome);
    }
}
