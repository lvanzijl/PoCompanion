using System.Text.Json;
using PoTool.Client.Helpers;
using PoTool.Client.Models;
using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public sealed class GlobalFilterDefaultsService
{
    private const string SessionStorageKey = "global-filter-default-routes";
    private const int DefaultRangeWindow = 5;
    private const int DefaultRollingDays = 30;

    private readonly GlobalFilterStore _globalFilterStore;
    private readonly GlobalFilterRouteService _globalFilterRouteService;
    private readonly ProductService _productService;
    private readonly TeamService _teamService;
    private readonly SprintService _sprintService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly HashSet<string> _appliedRoutes = new(StringComparer.Ordinal);

    private bool _loaded;

    public GlobalFilterDefaultsService(
        GlobalFilterStore globalFilterStore,
        GlobalFilterRouteService globalFilterRouteService,
        ProductService productService,
        TeamService teamService,
        SprintService sprintService,
        ISecureStorageService secureStorageService)
    {
        _globalFilterStore = globalFilterStore;
        _globalFilterRouteService = globalFilterRouteService;
        _productService = productService;
        _teamService = teamService;
        _sprintService = sprintService;
        _secureStorageService = secureStorageService;
    }

    public async Task<string?> BuildDefaultedUriAsync(string currentUri, int? activeProfileId, CancellationToken cancellationToken = default)
    {
        var usage = _globalFilterStore.CurrentUsage;
        if (usage is null || activeProfileId is null)
        {
            return null;
        }

        await EnsureLoadedAsync();

        if (_appliedRoutes.Contains(usage.RouteSignature)
            || usage.Status == FilterResolutionStatus.Invalid
            || usage.LastUpdateSource == FilterUpdateSource.Ui)
        {
            return null;
        }

        var defaultState = await CreateDefaultStateAsync(usage, activeProfileId.Value, cancellationToken);
        await MarkAppliedAsync(usage.RouteSignature);

        if (defaultState is null || defaultState == usage.State)
        {
            return null;
        }

        var projectAlias = defaultState.PrimaryProjectId is null
            ? null
            : await _globalFilterRouteService.ResolveProjectAliasAsync(defaultState.PrimaryProjectId, cancellationToken);

        await _globalFilterStore.SetStateAsync(
            currentUri,
            activeProfileId,
            FilterLocalBridgeState.FromState(defaultState, projectAlias),
            FilterUpdateSource.DefaultPreset,
            cancellationToken);

        var targetUri = await _globalFilterRouteService.BuildCurrentPageUriAsync(currentUri, _globalFilterStore.GetState(), cancellationToken);
        return WorkspaceQueryContextHelper.AreEquivalentRoutes(currentUri, targetUri)
            ? null
            : targetUri;
    }

    private async Task<FilterState?> CreateDefaultStateAsync(
        FilterStateResolution usage,
        int activeProfileId,
        CancellationToken cancellationToken)
    {
        var ownedProducts = (await _productService.GetProductsByOwnerAsync(activeProfileId, cancellationToken))
            .OrderBy(product => product.Order)
            .ThenBy(product => product.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var state = usage.State;
        var defaultProductId = ResolveDefaultProductId(usage, state, ownedProducts);
        var defaultTeamSelection = await ResolveDefaultTeamSelectionAsync(usage, state, defaultProductId, ownedProducts, cancellationToken);

        var nextState = state with
        {
            ProductIds = ResolveProductIds(usage, state, defaultProductId),
            TeamId = defaultTeamSelection?.TeamId ?? state.TeamId,
            Time = ResolveDefaultTime(usage, state.Time, defaultTeamSelection)
        };

        return nextState;
    }

    private static IReadOnlyList<int> ResolveProductIds(
        FilterStateResolution usage,
        FilterState state,
        int? defaultProductId)
    {
        if (!state.AllProducts || !usage.UsesProduct || usage.HasRouteProductAuthority)
        {
            return state.ProductIds;
        }

        return usage.PageName == "PlanBoard" && defaultProductId.HasValue
            ? [defaultProductId.Value]
            : Array.Empty<int>();
    }

    private static int? ResolveDefaultProductId(
        FilterStateResolution usage,
        FilterState state,
        IReadOnlyList<ProductDto> ownedProducts)
    {
        if (state.PrimaryProductId.HasValue || usage.HasRouteProductAuthority || ownedProducts.Count == 0)
        {
            return state.PrimaryProductId;
        }

        return usage.PageName == "PlanBoard"
            ? ownedProducts[0].Id
            : state.PrimaryProductId;
    }

    private async Task<DefaultTeamSelection?> ResolveDefaultTeamSelectionAsync(
        FilterStateResolution usage,
        FilterState state,
        int? defaultProductId,
        IReadOnlyList<ProductDto> ownedProducts,
        CancellationToken cancellationToken)
    {
        var requiresTeamContext = usage.MissingTeam || usage.MissingSprint || usage.UsesTeam;
        if (!requiresTeamContext)
        {
            return null;
        }

        var candidateTeamIds = BuildCandidateTeamIds(state, defaultProductId, ownedProducts);
        if (candidateTeamIds.Count == 0)
        {
            candidateTeamIds = (await _teamService.GetAllTeamsAsync(includeArchived: false, cancellationToken))
                .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                .Select(team => team.Id)
                .ToList();
        }

        foreach (var teamId in candidateTeamIds)
        {
            var currentSprint = await _sprintService.GetCurrentSprintForTeamAsync(teamId, cancellationToken);
            if (currentSprint is not null)
            {
                return new DefaultTeamSelection(teamId, currentSprint, null);
            }

            var sprints = (await _sprintService.GetSprintsForTeamAsync(teamId, cancellationToken))
                .Where(sprint => sprint.StartUtc.HasValue)
                .OrderBy(sprint => sprint.StartUtc)
                .ToList();
            if (sprints.Count > 0)
            {
                return new DefaultTeamSelection(teamId, sprints[^1], sprints);
            }
        }

        return null;
    }

    private static List<int> BuildCandidateTeamIds(
        FilterState state,
        int? defaultProductId,
        IReadOnlyList<ProductDto> ownedProducts)
    {
        if (state.TeamId.HasValue)
        {
            return [state.TeamId.Value];
        }

        var relevantProducts = ownedProducts.Where(product =>
            (state.PrimaryProductId.HasValue && product.Id == state.PrimaryProductId.Value)
            || (defaultProductId.HasValue && product.Id == defaultProductId.Value));

        var candidateTeamIds = relevantProducts
            .SelectMany(product => product.TeamIds)
            .Concat(ownedProducts.SelectMany(product => product.TeamIds))
            .Distinct()
            .ToList();

        return candidateTeamIds;
    }

    private static FilterTimeSelection ResolveDefaultTime(
        FilterStateResolution usage,
        FilterTimeSelection currentTime,
        DefaultTeamSelection? defaultTeamSelection)
    {
        if (currentTime.IsResolved || defaultTeamSelection is null)
        {
            return currentTime;
        }

        return currentTime.Mode switch
        {
            FilterTimeMode.Sprint when defaultTeamSelection.CurrentSprint is not null
                => new FilterTimeSelection(FilterTimeMode.Sprint, SprintId: defaultTeamSelection.CurrentSprint.Id),
            FilterTimeMode.Range => BuildRangeSelection(defaultTeamSelection),
            FilterTimeMode.Rolling => new FilterTimeSelection(
                FilterTimeMode.Rolling,
                RollingWindow: currentTime.RollingWindow ?? DefaultRollingDays,
                RollingUnit: currentTime.RollingUnit ?? FilterTimeUnit.Days),
            _ => currentTime
        };
    }

    private static FilterTimeSelection BuildRangeSelection(DefaultTeamSelection selection)
    {
        var sprints = (selection.AllSprints ?? [])
            .Where(sprint => sprint.StartUtc.HasValue)
            .OrderBy(sprint => sprint.StartUtc)
            .ToList();

        if (selection.CurrentSprint is null)
        {
            return FilterTimeSelection.Snapshot;
        }

        if (sprints.Count == 0)
        {
            return new FilterTimeSelection(FilterTimeMode.Range, StartSprintId: selection.CurrentSprint.Id, EndSprintId: selection.CurrentSprint.Id);
        }

        var currentIndex = sprints.FindIndex(sprint => sprint.Id == selection.CurrentSprint.Id);
        if (currentIndex < 0)
        {
            currentIndex = sprints.Count - 1;
        }

        var startIndex = Math.Max(0, currentIndex - (DefaultRangeWindow - 1));
        return new FilterTimeSelection(FilterTimeMode.Range, StartSprintId: sprints[startIndex].Id, EndSprintId: sprints[currentIndex].Id);
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var serialized = await _secureStorageService.GetAsync(SessionStorageKey);
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return;
        }

        try
        {
            var routeSignatures = JsonSerializer.Deserialize<List<string>>(serialized);
            if (routeSignatures is null)
            {
                return;
            }

            foreach (var routeSignature in routeSignatures.Where(static item => !string.IsNullOrWhiteSpace(item)))
            {
                _appliedRoutes.Add(routeSignature);
            }
        }
        catch (JsonException)
        {
        }
    }

    private async Task MarkAppliedAsync(string routeSignature)
    {
        if (!_appliedRoutes.Add(routeSignature))
        {
            return;
        }

        await _secureStorageService.SetAsync(SessionStorageKey, JsonSerializer.Serialize(_appliedRoutes.OrderBy(static item => item)));
    }

    private sealed record DefaultTeamSelection(int TeamId, SprintDto? CurrentSprint, IReadOnlyList<SprintDto>? AllSprints);
}
