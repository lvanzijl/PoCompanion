using System.Text.Json;
using PoTool.Client.Helpers;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class GlobalFilterDefaultsService
{
    private const string SessionStorageKey = "global-filter-default-routes";
    private readonly GlobalFilterStore _globalFilterStore;
    private readonly GlobalFilterRouteService _globalFilterRouteService;
    private readonly GlobalFilterAutoResolveService _autoResolveService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly HashSet<string> _appliedRoutes = new(StringComparer.Ordinal);

    private bool _loaded;

    public GlobalFilterDefaultsService(
        GlobalFilterStore globalFilterStore,
        GlobalFilterRouteService globalFilterRouteService,
        GlobalFilterAutoResolveService autoResolveService,
        ISecureStorageService secureStorageService)
    {
        _globalFilterStore = globalFilterStore;
        _globalFilterRouteService = globalFilterRouteService;
        _autoResolveService = autoResolveService;
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

        var defaultState = await _autoResolveService.ResolveAsync(usage, activeProfileId, usage.LastUpdateSource, cancellationToken)
            ?? (usage.LastUpdateSource == FilterUpdateSource.DefaultPreset ? usage.State : null);
        await MarkAppliedAsync(usage.RouteSignature);

        if (defaultState is null)
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
}
