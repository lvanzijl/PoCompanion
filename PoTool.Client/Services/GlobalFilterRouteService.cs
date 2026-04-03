using PoTool.Client.Helpers;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class GlobalFilterRouteService
{
    private readonly ProjectIdentityMapper _projectIdentityMapper;

    public GlobalFilterRouteService(ProjectIdentityMapper projectIdentityMapper)
    {
        _projectIdentityMapper = projectIdentityMapper;
    }

    public async Task<string> BuildCurrentPageUriAsync(
        string currentUri,
        FilterState state,
        CancellationToken cancellationToken = default)
    {
        if (!GlobalFilterPageCatalog.TryResolvePage(currentUri, out var route, out var definition, out _))
        {
            return currentUri;
        }

        var absoluteUri = Uri.TryCreate(currentUri, UriKind.Absolute, out var parsedAbsolute)
            ? parsedAbsolute
            : new Uri(new Uri("http://localhost"), currentUri.StartsWith('/') ? currentUri : $"/{currentUri}");

        var path = await ResolvePathAsync(route, definition!.PageName, state, cancellationToken);
        var queryContext = await BuildContextAsync(state, cancellationToken);
        var additionalParams = WorkspaceQueryContextHelper.ExtractAdditionalParameters(currentUri);
        return $"{path}{WorkspaceQueryContextHelper.BuildQueryString(queryContext, additionalParams)}";
    }

    public async Task<string> BuildUriAsync(
        string route,
        FilterState state,
        string? additionalParams = null,
        CancellationToken cancellationToken = default)
    {
        var queryContext = await BuildContextAsync(state, cancellationToken);
        return WorkspaceQueryContextHelper.BuildRoute(route, queryContext, additionalParams);
    }

    public async Task<string> BuildPlanningPageUriAsync(
        string pageName,
        FilterState state,
        string? additionalParams = null,
        CancellationToken cancellationToken = default)
    {
        var route = pageName switch
        {
            "ProductRoadmaps" => "planning/product-roadmaps",
            "ProjectPlanningOverview" => "planning/overview",
            "PlanBoard" => "planning/plan-board",
            "PlanningWorkspace" => "home/planning",
            _ => throw new InvalidOperationException($"Unsupported planning page '{pageName}'.")
        };

        var path = await ResolvePathAsync(route, pageName, state, cancellationToken);
        var queryContext = await BuildContextAsync(state, cancellationToken);
        return $"{path}{WorkspaceQueryContextHelper.BuildQueryString(queryContext, additionalParams)}";
    }

    public async Task<string> BuildPlanningPageUriAsync(
        string pageName,
        string? projectIdentity,
        int? productId = null,
        string? additionalParams = null,
        CancellationToken cancellationToken = default)
    {
        var projectId = string.IsNullOrWhiteSpace(projectIdentity)
            ? null
            : await _projectIdentityMapper.ResolveProjectIdAsync(projectIdentity, cancellationToken) ?? projectIdentity;

        return await BuildPlanningPageUriAsync(
            pageName,
            new FilterState(
                productId.HasValue ? [productId.Value] : Array.Empty<int>(),
                string.IsNullOrWhiteSpace(projectId) ? Array.Empty<string>() : [projectId],
                null,
                FilterTimeSelection.Snapshot),
            additionalParams,
            cancellationToken);
    }

    public async Task<string?> ResolveProjectAliasAsync(string? projectId, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(projectId)
            ? null
            : await _projectIdentityMapper.ResolveProjectAliasAsync(projectId, cancellationToken);

    private async Task<WorkspaceQueryContext> BuildContextAsync(FilterState state, CancellationToken cancellationToken)
        => new(
            ProjectAlias: await ResolveProjectAliasAsync(state.PrimaryProjectId, cancellationToken),
            ProjectId: state.PrimaryProjectId,
            ProductId: state.PrimaryProductId,
            TeamId: state.TeamId,
            SprintId: state.Time.SprintId,
            FromSprintId: state.Time.StartSprintId,
            ToSprintId: state.Time.EndSprintId,
            TimeMode: state.Time.Mode,
            RollingWindow: state.Time.RollingWindow,
            RollingUnit: state.Time.RollingUnit);

    private async Task<string> ResolvePathAsync(
        string normalizedRoute,
        string pageName,
        FilterState state,
        CancellationToken cancellationToken)
    {
        var defaultPath = $"/{normalizedRoute}";
        var projectAlias = await ResolveProjectAliasAsync(state.PrimaryProjectId, cancellationToken);

        return pageName switch
        {
            "ProductRoadmaps" => string.IsNullOrWhiteSpace(projectAlias)
                ? WorkspaceRoutes.ProductRoadmaps
                : WorkspaceRoutes.GetProjectProductRoadmaps(projectAlias),
            "ProjectPlanningOverview" => string.IsNullOrWhiteSpace(projectAlias)
                ? WorkspaceRoutes.PlanningWorkspace
                : WorkspaceRoutes.GetProjectPlanningOverview(projectAlias),
            "PlanBoard" => string.IsNullOrWhiteSpace(projectAlias)
                ? WorkspaceRoutes.PlanBoard
                : WorkspaceRoutes.GetProjectPlanBoard(projectAlias),
            "ProductRoadmapEditor" when state.PrimaryProductId.HasValue => WorkspaceRoutes.GetProductRoadmapEditor(state.PrimaryProductId.Value),
            _ => defaultPath
        };
    }
}
