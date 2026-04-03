using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public sealed class FilterStateResolver
{
    private const int DefaultRollingDays = 180;

    private readonly ProjectService _projectService;
    private readonly Dictionary<string, ProjectDto?> _projectCache = new(StringComparer.OrdinalIgnoreCase);

    public FilterStateResolver(ProjectService projectService)
    {
        _projectService = projectService;
    }

    public async Task<FilterStateResolution?> ResolveAsync(
        string? uri,
        int? activeProfileId = null,
        FilterLocalBridgeState? localState = null,
        FilterUpdateSource localSource = FilterUpdateSource.LocalBridge,
        CancellationToken cancellationToken = default)
    {
        if (!GlobalFilterPageCatalog.TryResolvePage(uri, out var route, out var definition, out var context) || definition is null)
        {
            return null;
        }

        var decisions = new List<string>();
        var lastUpdateSource = FilterUpdateSource.Default;

        var productIds = ResolveProducts(route, context, localState, decisions, ref lastUpdateSource, localSource);
        var projectResolution = await ResolveProjectsAsync(route, context, localState, decisions, localSource, cancellationToken);
        var projectIds = projectResolution.ProjectIds;
        if (projectResolution.Source != FilterUpdateSource.Default)
        {
            lastUpdateSource = projectResolution.Source;
        }
        var teamId = ResolveScalar(
            context.TeamId,
            localState?.TeamId,
            decisions,
            ref lastUpdateSource,
            "teamId",
            localSource);

        var time = ResolveTime(definition.TimeMode, context, localState, decisions, ref lastUpdateSource, localSource);
        var missingTeam = definition.RequiresTeam && !teamId.HasValue;
        var missingSprint = definition.RequiresSprint && !HasRequiredSprint(time);

        return new FilterStateResolution(
            definition.PageName,
            route,
            definition.UsesProduct,
            definition.UsesProject,
            definition.UsesTeam,
            definition.UsesTime,
            new FilterState(productIds, projectIds, teamId, time),
            missingTeam,
            missingSprint,
            activeProfileId,
            lastUpdateSource,
            decisions,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<int> ResolveProducts(
        string route,
        WorkspaceQueryContext context,
        FilterLocalBridgeState? localState,
        ICollection<string> decisions,
        ref FilterUpdateSource lastUpdateSource,
        FilterUpdateSource localSource)
    {
        var routeProductId = GlobalFilterPageCatalog.ResolveRouteProductId(route);
        if (routeProductId.HasValue)
        {
            if (context.ProductId.HasValue && context.ProductId.Value != routeProductId.Value)
            {
                decisions.Add($"route productId {routeProductId.Value} overrode query productId {context.ProductId.Value}");
            }

            lastUpdateSource = FilterUpdateSource.Route;
            return new[] { routeProductId.Value };
        }

        if (context.ProductId.HasValue)
        {
            lastUpdateSource = FilterUpdateSource.Query;
            return new[] { context.ProductId.Value };
        }

        if (localState?.ProductId.HasValue == true)
        {
            decisions.Add("local bridge supplied productId");
            lastUpdateSource = localSource;
            return new[] { localState.ProductId.Value };
        }

        return Array.Empty<int>();
    }

    private async Task<(IReadOnlyList<string> ProjectIds, FilterUpdateSource Source)> ResolveProjectsAsync(
        string route,
        WorkspaceQueryContext context,
        FilterLocalBridgeState? localState,
        ICollection<string> decisions,
        FilterUpdateSource localSource,
        CancellationToken cancellationToken)
    {
        var routeAlias = GlobalFilterPageCatalog.ResolveRouteProjectAlias(route);
        var queryProjectId = NormalizeProjectId(context.ProjectId);
        var queryAlias = NormalizeProjectId(context.ProjectAlias);
        var localProjectId = NormalizeProjectId(localState?.ProjectId);
        var localProjectAlias = NormalizeProjectId(localState?.ProjectAlias);

        if (!string.IsNullOrWhiteSpace(routeAlias))
        {
            if (!string.IsNullOrWhiteSpace(queryAlias) && !string.Equals(routeAlias, queryAlias, StringComparison.OrdinalIgnoreCase))
            {
                decisions.Add($"route project alias '{routeAlias}' overrode query projectAlias '{queryAlias}'");
            }

            if (!string.IsNullOrWhiteSpace(queryProjectId))
            {
                decisions.Add($"route project alias '{routeAlias}' overrode query projectId '{queryProjectId}'");
            }

            var projectId = await ResolveProjectIdAsync(routeAlias, cancellationToken);
            return (string.IsNullOrWhiteSpace(projectId) ? Array.Empty<string>() : new[] { projectId }, FilterUpdateSource.Route);
        }

        if (!string.IsNullOrWhiteSpace(queryProjectId))
        {
            return (new[] { queryProjectId }, FilterUpdateSource.Query);
        }

        if (!string.IsNullOrWhiteSpace(queryAlias))
        {
            var projectId = await ResolveProjectIdAsync(queryAlias, cancellationToken);
            return (string.IsNullOrWhiteSpace(projectId) ? Array.Empty<string>() : new[] { projectId }, FilterUpdateSource.Query);
        }

        if (!string.IsNullOrWhiteSpace(localProjectId))
        {
            decisions.Add("local bridge supplied projectId");
            return (new[] { localProjectId }, localSource);
        }

        if (!string.IsNullOrWhiteSpace(localProjectAlias))
        {
            decisions.Add("local bridge supplied project alias");
            var projectId = await ResolveProjectIdAsync(localProjectAlias, cancellationToken);
            return (string.IsNullOrWhiteSpace(projectId) ? Array.Empty<string>() : new[] { projectId }, localSource);
        }

        return (Array.Empty<string>(), FilterUpdateSource.Default);
    }

    private static int? ResolveScalar(
        int? queryValue,
        int? localValue,
        ICollection<string> decisions,
        ref FilterUpdateSource lastUpdateSource,
        string label,
        FilterUpdateSource localSource)
    {
        if (queryValue.HasValue)
        {
            lastUpdateSource = FilterUpdateSource.Query;
            return queryValue.Value;
        }

        if (localValue.HasValue)
        {
            decisions.Add($"local bridge supplied {label}");
            lastUpdateSource = localSource;
            return localValue.Value;
        }

        return null;
    }

    private static FilterTimeSelection ResolveTime(
        FilterTimeMode mode,
        WorkspaceQueryContext context,
        FilterLocalBridgeState? localState,
        ICollection<string> decisions,
        ref FilterUpdateSource lastUpdateSource,
        FilterUpdateSource localSource)
    {
        return mode switch
        {
            FilterTimeMode.Sprint => ResolveSprintTime(context, localState, decisions, ref lastUpdateSource, localSource),
            FilterTimeMode.Range => ResolveRangeTime(context, localState, decisions, ref lastUpdateSource, localSource),
            FilterTimeMode.Rolling => ResolveRollingTime(context, localState, decisions, ref lastUpdateSource, localSource),
            _ => FilterTimeSelection.Snapshot
        };
    }

    private static FilterTimeSelection ResolveSprintTime(
        WorkspaceQueryContext context,
        FilterLocalBridgeState? localState,
        ICollection<string> decisions,
        ref FilterUpdateSource lastUpdateSource,
        FilterUpdateSource localSource)
    {
        if (context.SprintId.HasValue)
        {
            lastUpdateSource = FilterUpdateSource.Query;
            return new FilterTimeSelection(FilterTimeMode.Sprint, SprintId: context.SprintId);
        }

        if (localState?.SprintId.HasValue == true)
        {
            decisions.Add("local bridge supplied sprintId");
            lastUpdateSource = localSource;
            return new FilterTimeSelection(FilterTimeMode.Sprint, SprintId: localState.SprintId);
        }

        if (context.FromSprintId.HasValue || context.ToSprintId.HasValue)
        {
            decisions.Add("range query kept as unresolved sprint mode");
            lastUpdateSource = FilterUpdateSource.Query;
        }

        return new FilterTimeSelection(FilterTimeMode.Sprint);
    }

    private static FilterTimeSelection ResolveRangeTime(
        WorkspaceQueryContext context,
        FilterLocalBridgeState? localState,
        ICollection<string> decisions,
        ref FilterUpdateSource lastUpdateSource,
        FilterUpdateSource localSource)
    {
        var start = context.FromSprintId ?? localState?.FromSprintId;
        var end = context.ToSprintId ?? localState?.ToSprintId;
        var source = FilterUpdateSource.Default;

        if (context.FromSprintId.HasValue || context.ToSprintId.HasValue)
        {
            source = FilterUpdateSource.Query;
        }
        else if (localState?.FromSprintId.HasValue == true || localState?.ToSprintId.HasValue == true)
        {
            decisions.Add("local bridge supplied sprint range");
            source = localSource;
        }

        if (start.HasValue && end.HasValue && start.Value > end.Value)
        {
            (start, end) = (end, start);
            decisions.Add("invalid range normalized by swapping from/to sprint IDs");
        }

        if (source != FilterUpdateSource.Default)
        {
            lastUpdateSource = source;
        }

        return new FilterTimeSelection(FilterTimeMode.Range, StartSprintId: start, EndSprintId: end);
    }

    private static FilterTimeSelection ResolveRollingTime(
        WorkspaceQueryContext context,
        FilterLocalBridgeState? localState,
        ICollection<string> decisions,
        ref FilterUpdateSource lastUpdateSource,
        FilterUpdateSource localSource)
    {
        if (context.SprintId.HasValue)
        {
            decisions.Add("query sprintId normalized to rolling sprint window");
            lastUpdateSource = FilterUpdateSource.Query;
            return new FilterTimeSelection(FilterTimeMode.Rolling, RollingWindow: 1, RollingUnit: FilterTimeUnit.Sprint, SprintId: context.SprintId);
        }

        if (localState?.RollingWindow.HasValue == true)
        {
            lastUpdateSource = localSource;
            return new FilterTimeSelection(
                FilterTimeMode.Rolling,
                SprintId: localState.SprintId,
                RollingWindow: localState.RollingWindow,
                RollingUnit: localState.RollingUnit ?? FilterTimeUnit.Days);
        }

        decisions.Add($"default rolling window applied ({DefaultRollingDays} days)");
        return new FilterTimeSelection(FilterTimeMode.Rolling, RollingWindow: DefaultRollingDays, RollingUnit: FilterTimeUnit.Days);
    }

    private async Task<string?> ResolveProjectIdAsync(string aliasOrId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeProjectId(aliasOrId);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (_projectCache.TryGetValue(normalized, out var cached))
        {
            return cached?.Id ?? normalized;
        }

        var project = await _projectService.GetProjectAsync(normalized, cancellationToken);
        _projectCache[normalized] = project;
        return project?.Id ?? normalized;
    }

    private static bool HasRequiredSprint(FilterTimeSelection time)
        => time.Mode switch
        {
            FilterTimeMode.Sprint => time.SprintId.HasValue,
            FilterTimeMode.Range => time.StartSprintId.HasValue || time.EndSprintId.HasValue,
            FilterTimeMode.Rolling => time.SprintId.HasValue || time.RollingWindow.HasValue,
            _ => false
        };

    private static string? NormalizeProjectId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
