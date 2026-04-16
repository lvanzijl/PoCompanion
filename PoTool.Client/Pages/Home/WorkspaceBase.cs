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
    protected StartupGateNotificationService StartupGateNotificationService { get; set; } = default!;

    [Inject]
    protected GlobalFilterStore GlobalFilterStore { get; set; } = default!;

    /// <summary>
    /// Project alias for context propagation (parsed from URL).
    /// </summary>
    protected string? ProjectAlias { get; set; }

    /// <summary>
    /// Canonical project identifier from the global filter store.
    /// </summary>
    protected string? ProjectId => GlobalFilterStore.GetState().PrimaryProjectId;

    /// <summary>
    /// Product ID for context propagation (parsed from URL).
    /// </summary>
    protected int? ProductId => GlobalFilterStore.GetState().PrimaryProductId;
    
    /// <summary>
    /// Team ID for context propagation (parsed from URL).
    /// </summary>
    protected int? TeamId => GlobalFilterStore.GetState().TeamId;

    /// <summary>
    /// Sprint ID for context propagation (parsed from URL).
    /// </summary>
    protected int? SprintId => GlobalFilterStore.GetState().Time.Mode == FilterTimeMode.Sprint
        ? GlobalFilterStore.GetState().Time.SprintId
        : null;

    /// <summary>
    /// Optional start sprint for trend-range context.
    /// </summary>
    protected int? FromSprintId => GlobalFilterStore.GetState().Time.Mode == FilterTimeMode.Range
        ? GlobalFilterStore.GetState().Time.StartSprintId
        : null;

    /// <summary>
    /// Optional end sprint for trend-range context.
    /// </summary>
    protected int? ToSprintId => GlobalFilterStore.GetState().Time.Mode == FilterTimeMode.Range
        ? GlobalFilterStore.GetState().Time.EndSprintId
        : null;

    /// <summary>
    /// Parses query parameters from the current URL for context propagation.
    /// Extracts productId and teamId if present.
    /// </summary>
    protected virtual void ParseContextQueryParameters()
    {
        var context = WorkspaceQueryContextHelper.Parse(NavigationManager.Uri);
        ProjectAlias = context.ProjectAlias;
        _ = GlobalFilterStore.TrackNavigationAsync(NavigationManager.Uri, ProfileService.GetActiveProfileId());
    }

    /// <summary>
    /// Builds a query string that includes context parameters (productId, teamId)
    /// along with any additional parameters.
    /// </summary>
    /// <param name="additionalParams">Additional query parameters to include (e.g., "validationType=missing-effort")</param>
    /// <returns>Query string starting with "?" or empty string if no parameters</returns>
    protected string BuildContextQuery(string? additionalParams = null)
        => WorkspaceQueryContextHelper.BuildQueryString(
            new WorkspaceQueryContext(
                ProjectAlias: ProjectAlias,
                ProjectId: ProjectId,
                ProductId: ProductId,
                TeamId: TeamId,
                SprintId: SprintId,
                FromSprintId: FromSprintId,
                ToSprintId: ToSprintId,
                TimeMode: GlobalFilterStore.GetState().Time.Mode,
                RollingWindow: GlobalFilterStore.GetState().Time.RollingWindow,
                RollingUnit: GlobalFilterStore.GetState().Time.RollingUnit),
            additionalParams);
    
    /// <summary>
    /// Checks if the user has a valid profile selected and redirects to home if not.
    /// </summary>
    /// <returns>True if profile is valid, false if redirecting to home.</returns>
    protected async Task<bool> EnsureProfileAsync()
    {
        var activeProfile = await ProfileService.GetActiveProfileAsync();
        if (activeProfile == null)
        {
            StartupGateNotificationService.RequestReevaluation();
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
            NavigationManager.NavigateTo(
                WorkspaceQueryContextHelper.BuildRoute(
                    WorkspaceRoutes.Home,
                    new WorkspaceQueryContext(
                        ProjectAlias: ProjectAlias,
                        ProjectId: ProjectId,
                        ProductId: ProductId,
                        TeamId: TeamId,
                        SprintId: SprintId,
                        FromSprintId: FromSprintId,
                        ToSprintId: ToSprintId,
                        TimeMode: GlobalFilterStore.GetState().Time.Mode,
                        RollingWindow: GlobalFilterStore.GetState().Time.RollingWindow,
                        RollingUnit: GlobalFilterStore.GetState().Time.RollingUnit)));
    }
}
