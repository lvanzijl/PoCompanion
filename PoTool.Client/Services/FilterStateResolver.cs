using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public sealed class FilterStateResolver
{
    private const int DefaultRollingDays = 180;

    private readonly ProjectIdentityMapper _projectIdentityMapper;

    public FilterStateResolver(ProjectIdentityMapper projectIdentityMapper)
    {
        _projectIdentityMapper = projectIdentityMapper;
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
        var issues = new List<string>();
        var lastUpdateSource = FilterUpdateSource.Default;
        var routeSignature = WorkspaceQueryContextHelper.CreateRouteSignature(uri);
        var routeProductAuthority = false;
        var routeProjectAuthority = !string.IsNullOrWhiteSpace(GlobalFilterPageCatalog.ResolveRouteProjectAlias(route));

        var projectResolution = await ResolveProjectsAsync(route, context, localState, decisions, issues, localSource, cancellationToken);
        var projectIds = projectResolution.ProjectIds;
        if (projectResolution.Source != FilterUpdateSource.Default)
        {
            lastUpdateSource = projectResolution.Source;
        }
        var productIds = ResolveProducts(
            route,
            context,
            localState,
            decisions,
            issues,
            ref lastUpdateSource,
            localSource,
            projectResolution.AllowedProductIds,
            projectResolution.RouteProjectAlias);
        var teamId = ResolveScalar(
            context.TeamId,
            localState?.TeamId,
            localState is not null,
            decisions,
            ref lastUpdateSource,
            "teamId",
            localSource);

        var time = ResolveTime(definition.TimeMode, context, localState, decisions, issues, ref lastUpdateSource, localSource);
        var missingTeam = definition.RequiresTeam && !teamId.HasValue;
        var missingSprint = definition.RequiresSprint && !HasRequiredSprint(time);
        var status = DetermineStatus(missingTeam, missingSprint, decisions, issues);

        return new FilterStateResolution(
            definition.PageName,
            route,
            routeSignature,
            routeProductAuthority,
            routeProjectAuthority,
            definition.UsesProduct,
            definition.UsesProject,
            definition.UsesTeam,
            definition.UsesTime,
            new FilterState(productIds, projectIds, teamId, time),
            status,
            missingTeam,
            missingSprint,
            activeProfileId,
            lastUpdateSource,
            decisions,
            issues,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<int> ResolveProducts(
        string route,
        WorkspaceQueryContext context,
        FilterLocalBridgeState? localState,
        ICollection<string> decisions,
        ICollection<string> issues,
        ref FilterUpdateSource lastUpdateSource,
        FilterUpdateSource localSource,
        IReadOnlyCollection<int>? allowedProductIds,
        string? routeProjectAlias)
    {
        var routeProductId = GlobalFilterPageCatalog.ResolveRouteProductId(route);
        if (routeProductId.HasValue)
        {
            decisions.Add($"route productId {routeProductId.Value} is treated as a lookup hint only");
        }

        IReadOnlyList<int> selectedProductIds;
        if (localState is not null)
        {
            lastUpdateSource = localSource;
            if (localState.ProductId.HasValue)
            {
                decisions.Add("local bridge supplied productId");
                selectedProductIds = [localState.ProductId.Value];
            }
            else
            {
                if (context.ProductId.HasValue)
                {
                    decisions.Add("local bridge cleared productId");
                }

                selectedProductIds = Array.Empty<int>();
            }
        }
        else if (context.ProductId.HasValue)
        {
            lastUpdateSource = FilterUpdateSource.Query;
            selectedProductIds = [context.ProductId.Value];
        }
        else
        {
            selectedProductIds = Array.Empty<int>();
        }

        if (routeProductId.HasValue
            && selectedProductIds.Count == 1
            && selectedProductIds[0] != routeProductId.Value)
        {
            issues.Add($"Route product '{routeProductId.Value}' does not match the selected global product '{selectedProductIds[0]}'.");
        }

        if (allowedProductIds is not null && selectedProductIds.Count > 0)
        {
            var allowedProductSet = allowedProductIds.ToHashSet();
            var invalidProductIds = selectedProductIds
                .Where(productId => !allowedProductSet.Contains(productId))
                .Distinct()
                .ToArray();

            if (invalidProductIds.Length > 0)
            {
                var invalidIds = string.Join(", ", invalidProductIds);
                var scopeLabel = string.IsNullOrWhiteSpace(routeProjectAlias)
                    ? "the current route scope"
                    : $"project route '{routeProjectAlias}'";
                issues.Add($"Selected global product '{invalidIds}' is not available in {scopeLabel}.");
            }
        }

        return selectedProductIds;
    }

    private async Task<(IReadOnlyList<string> ProjectIds, FilterUpdateSource Source, IReadOnlyCollection<int>? AllowedProductIds, string? RouteProjectAlias)> ResolveProjectsAsync(
        string route,
        WorkspaceQueryContext context,
        FilterLocalBridgeState? localState,
        ICollection<string> decisions,
        ICollection<string> issues,
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

            var routeProject = await _projectIdentityMapper.ResolveProjectAsync(routeAlias, cancellationToken);
            var projectId = routeProject?.Id;
            if (string.IsNullOrWhiteSpace(projectId))
            {
                decisions.Add($"route project alias '{routeAlias}' is authoritative without a resolved projectId");
            }
            return (
                string.IsNullOrWhiteSpace(projectId) ? Array.Empty<string>() : [projectId],
                FilterUpdateSource.Route,
                routeProject?.ProductIds,
                routeAlias);
        }

        if (localState is not null)
        {
            if (!string.IsNullOrWhiteSpace(localProjectId))
            {
                decisions.Add("local bridge supplied projectId");
                return ([localProjectId], localSource, null, null);
            }

            if (!string.IsNullOrWhiteSpace(localProjectAlias))
            {
                decisions.Add("local bridge supplied project alias");
                var projectId = await ResolveProjectIdAsync(localProjectAlias, cancellationToken);
                if (string.IsNullOrWhiteSpace(projectId))
                {
                    issues.Add($"local project alias '{localProjectAlias}' could not be resolved");
                }
                return (string.IsNullOrWhiteSpace(projectId) ? Array.Empty<string>() : [projectId], localSource, null, null);
            }

            if (!string.IsNullOrWhiteSpace(queryProjectId) || !string.IsNullOrWhiteSpace(queryAlias))
            {
                decisions.Add("local bridge cleared project selection");
                return (Array.Empty<string>(), localSource, null, null);
            }
        }

        if (!string.IsNullOrWhiteSpace(queryProjectId))
        {
            return ([queryProjectId], FilterUpdateSource.Query, null, null);
        }

        if (!string.IsNullOrWhiteSpace(queryAlias))
        {
            var projectId = await ResolveProjectIdAsync(queryAlias, cancellationToken);
            if (string.IsNullOrWhiteSpace(projectId))
            {
                issues.Add($"query project alias '{queryAlias}' could not be resolved");
            }
            return (string.IsNullOrWhiteSpace(projectId) ? Array.Empty<string>() : [projectId], FilterUpdateSource.Query, null, null);
        }

        return (Array.Empty<string>(), FilterUpdateSource.Default, null, null);
    }

    private static int? ResolveScalar(
        int? queryValue,
        int? localValue,
        bool hasLocalOverride,
        ICollection<string> decisions,
        ref FilterUpdateSource lastUpdateSource,
        string label,
        FilterUpdateSource localSource)
    {
        if (hasLocalOverride)
        {
            lastUpdateSource = localSource;
            if (localValue.HasValue)
            {
                decisions.Add($"local bridge supplied {label}");
                return localValue.Value;
            }

            if (queryValue.HasValue)
            {
                decisions.Add($"local bridge cleared {label}");
            }

            return null;
        }

        if (queryValue.HasValue)
        {
            lastUpdateSource = FilterUpdateSource.Query;
            return queryValue.Value;
        }

        return null;
    }

    private static FilterTimeSelection ResolveTime(
        FilterTimeMode mode,
        WorkspaceQueryContext context,
        FilterLocalBridgeState? localState,
        ICollection<string> decisions,
        ICollection<string> issues,
        ref FilterUpdateSource lastUpdateSource,
        FilterUpdateSource localSource)
    {
        return mode switch
        {
            FilterTimeMode.Sprint => ResolveSprintTime(context, localState, decisions, ref lastUpdateSource, localSource),
            FilterTimeMode.Range => ResolveRangeTime(context, localState, decisions, ref lastUpdateSource, localSource),
            FilterTimeMode.Rolling => ResolveRollingTime(context, localState, decisions, issues, ref lastUpdateSource, localSource),
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
        if (localState is not null)
        {
            lastUpdateSource = localSource;
            if (localState.SprintId.HasValue)
            {
                decisions.Add("local bridge supplied sprintId");
                return new FilterTimeSelection(FilterTimeMode.Sprint, SprintId: localState.SprintId);
            }

            if (context.SprintId.HasValue)
            {
                decisions.Add("local bridge cleared sprintId");
            }

            return new FilterTimeSelection(FilterTimeMode.Sprint);
        }

        if (context.SprintId.HasValue)
        {
            lastUpdateSource = FilterUpdateSource.Query;
            return new FilterTimeSelection(FilterTimeMode.Sprint, SprintId: context.SprintId);
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
        var start = localState is not null ? localState.FromSprintId : context.FromSprintId;
        var end = localState is not null ? localState.ToSprintId : context.ToSprintId;
        var source = FilterUpdateSource.Default;

        if (localState is not null)
        {
            source = localSource;
            if (localState.FromSprintId.HasValue || localState.ToSprintId.HasValue)
            {
                decisions.Add("local bridge supplied sprint range");
            }
            else if (context.FromSprintId.HasValue || context.ToSprintId.HasValue)
            {
                decisions.Add("local bridge cleared sprint range");
            }
        }
        else if (context.FromSprintId.HasValue || context.ToSprintId.HasValue)
        {
            source = FilterUpdateSource.Query;
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
        ICollection<string> issues,
        ref FilterUpdateSource lastUpdateSource,
        FilterUpdateSource localSource)
    {
        if (localState is not null)
        {
            lastUpdateSource = localSource;

            if (localState.RollingWindow.HasValue)
            {
                if (localState.RollingWindow <= 0 || localState.RollingUnit is null)
                {
                    issues.Add("rolling time selection requires a positive rolling window and explicit unit");
                    return new FilterTimeSelection(FilterTimeMode.Rolling);
                }

                decisions.Add("local bridge supplied rolling window");
                return new FilterTimeSelection(
                    FilterTimeMode.Rolling,
                    SprintId: localState.SprintId,
                    RollingWindow: localState.RollingWindow,
                    RollingUnit: localState.RollingUnit ?? FilterTimeUnit.Days);
            }

            if (localState.SprintId.HasValue)
            {
                decisions.Add("local bridge supplied rolling sprintId");
                return new FilterTimeSelection(
                    FilterTimeMode.Rolling,
                    SprintId: localState.SprintId,
                    RollingWindow: DefaultRollingDays,
                    RollingUnit: FilterTimeUnit.Days);
            }

            if (context.SprintId.HasValue || context.RollingWindow.HasValue || context.RollingUnit.HasValue)
            {
                decisions.Add("local bridge cleared rolling selection");
            }

            return new FilterTimeSelection(FilterTimeMode.Rolling, RollingWindow: DefaultRollingDays, RollingUnit: FilterTimeUnit.Days);
        }

        if (context.SprintId.HasValue)
        {
            decisions.Add("query sprintId normalized to rolling sprint window");
            lastUpdateSource = FilterUpdateSource.Query;
            return new FilterTimeSelection(FilterTimeMode.Rolling, RollingWindow: 1, RollingUnit: FilterTimeUnit.Sprint, SprintId: context.SprintId);
        }

        if (context.RollingWindow.HasValue || context.RollingUnit.HasValue)
        {
            if (!context.RollingWindow.HasValue || context.RollingWindow.Value <= 0 || !context.RollingUnit.HasValue)
            {
                issues.Add("rolling time selection requires a positive rolling window and explicit unit");
                lastUpdateSource = FilterUpdateSource.Query;
                return new FilterTimeSelection(FilterTimeMode.Rolling);
            }

            lastUpdateSource = FilterUpdateSource.Query;
            return new FilterTimeSelection(
                FilterTimeMode.Rolling,
                RollingWindow: context.RollingWindow,
                RollingUnit: context.RollingUnit);
        }

        decisions.Add($"default rolling window applied ({DefaultRollingDays} days)");
        return new FilterTimeSelection(FilterTimeMode.Rolling, RollingWindow: DefaultRollingDays, RollingUnit: FilterTimeUnit.Days);
    }

    private async Task<string?> ResolveProjectIdAsync(string aliasOrId, CancellationToken cancellationToken)
        => await _projectIdentityMapper.ResolveProjectIdAsync(aliasOrId, cancellationToken);

    private static bool HasRequiredSprint(FilterTimeSelection time)
        => time.Mode switch
        {
            FilterTimeMode.Sprint => time.SprintId.HasValue,
            FilterTimeMode.Range => time.StartSprintId.HasValue && time.EndSprintId.HasValue,
            FilterTimeMode.Rolling => time.RollingWindow.HasValue && time.RollingUnit.HasValue,
            _ => false
        };

    private static FilterResolutionStatus DetermineStatus(
        bool missingTeam,
        bool missingSprint,
        IReadOnlyCollection<string> decisions,
        IReadOnlyCollection<string> issues)
    {
        if (issues.Count > 0)
        {
            return FilterResolutionStatus.Invalid;
        }

        if (missingTeam || missingSprint)
        {
            return FilterResolutionStatus.Unresolved;
        }

        return decisions.Count > 0
            ? FilterResolutionStatus.ResolvedWithNormalization
            : FilterResolutionStatus.Resolved;
    }

    private static string? NormalizeProjectId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
