using System.Collections.Specialized;
using System.Web;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Client.Services;

namespace PoTool.Client.Pages.Home;

/// <summary>
/// Base class for Home workspace pages providing shared functionality
/// for context propagation and navigation.
/// </summary>
public abstract class WorkspaceBase : ComponentBase
{
    [Inject]
    protected IProfileService ProfileService { get; set; } = default!;
    
    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;
    
    [Inject]
    protected ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    protected ErrorMessageService ErrorMessageService { get; set; } = default!;

    /// <summary>
    /// Project alias for context propagation (parsed from URL).
    /// </summary>
    protected string? ProjectAlias { get; set; }

    /// <summary>
    /// Product ID for context propagation (parsed from URL).
    /// </summary>
    protected int? ProductId { get; set; }
    
    /// <summary>
    /// Team ID for context propagation (parsed from URL).
    /// </summary>
    protected int? TeamId { get; set; }

    /// <summary>
    /// Sprint ID for context propagation (parsed from URL).
    /// </summary>
    protected int? SprintId { get; set; }

    /// <summary>
    /// Optional start sprint for trend-range context.
    /// </summary>
    protected int? FromSprintId { get; set; }

    /// <summary>
    /// Optional end sprint for trend-range context.
    /// </summary>
    protected int? ToSprintId { get; set; }

    /// <summary>
    /// Parses query parameters from the current URL for context propagation.
    /// Extracts productId and teamId if present.
    /// </summary>
    protected virtual void ParseContextQueryParameters()
    {
        var context = WorkspaceQueryContextHelper.Parse(NavigationManager.Uri);
        ProjectAlias = context.ProjectAlias;
        ProductId = context.ProductId;
        TeamId = context.TeamId;
        SprintId = context.SprintId;
        FromSprintId = context.FromSprintId;
        ToSprintId = context.ToSprintId;
    }

    protected NameValueCollection GetQueryParameters()
    {
        var uri = new Uri(NavigationManager.Uri);
        return HttpUtility.ParseQueryString(uri.Query);
    }

    /// <summary>
    /// Builds a query string that includes context parameters (productId, teamId)
    /// along with any additional parameters.
    /// </summary>
    /// <param name="additionalParams">Additional query parameters to include (e.g., "validationType=missing-effort")</param>
    /// <returns>Query string starting with "?" or empty string if no parameters</returns>
    protected string BuildContextQuery(string? additionalParams = null)
        => WorkspaceQueryContextHelper.BuildQueryString(
            new WorkspaceQueryContext(ProjectAlias, ProductId, TeamId, SprintId, FromSprintId, ToSprintId),
            additionalParams);

    protected string BuildContextRoute(string route, string? additionalParams = null)
        => WorkspaceQueryContextHelper.BuildRoute(
            route,
            new WorkspaceQueryContext(ProjectAlias, ProductId, TeamId, SprintId, FromSprintId, ToSprintId),
            additionalParams);

    protected void ReplaceCurrentRoute(string route, string? additionalParams = null)
    {
        NavigationManager.NavigateTo(BuildContextRoute(route, additionalParams), replace: true);
    }
    
    /// <summary>
    /// Checks if the user has a valid profile selected and redirects to home if not.
    /// </summary>
    /// <returns>True if profile is valid, false if redirecting to home.</returns>
    protected async Task<bool> EnsureProfileAsync()
    {
        var activeProfile = await ProfileService.GetActiveProfileAsync();
        if (activeProfile == null)
        {
            NavigationManager.NavigateTo($"{WorkspaceRoutes.Home}?noProfile=true");
            return false;
        }
        
        ProfileService.SetCachedActiveProfileId(activeProfile.Id);
        return true;
    }
    
    /// <summary>
    /// Navigates to the Home page.
    /// </summary>
    protected void NavigateToHome()
    {
        NavigationManager.NavigateTo(BuildContextRoute(WorkspaceRoutes.Home));
    }
}
