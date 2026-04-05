using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Client.Helpers;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class GlobalFilterStore
{
    private readonly ILogger<GlobalFilterStore> _logger;
    private readonly FilterStateResolver _resolver;
    private readonly List<FilterStateResolution> _history = new();
    private readonly List<Action> _subscriptions = new();
    private string? _currentRouteSignature;
    private string? _pendingRouteSignature;

    public GlobalFilterStore(ILogger<GlobalFilterStore> logger, FilterStateResolver resolver)
    {
        _logger = logger;
        _resolver = resolver;
    }

    public FilterState CurrentState { get; private set; } = FilterState.Neutral;

    public FilterStateResolution? CurrentUsage { get; private set; }

    public event Action? Changed;

    public IReadOnlyList<FilterStateResolution> History => _history;

    public string? CurrentRouteSignature => _currentRouteSignature;

    public string? PendingRouteSignature => _pendingRouteSignature;

    public FilterState GetState() => CurrentState;

    public IDisposable Subscribe(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _subscriptions.Add(callback);
        return new Subscription(_subscriptions, callback);
    }

    public async Task TrackNavigationAsync(string? uri, int? activeProfileId = null, CancellationToken cancellationToken = default)
    {
        var resolution = await _resolver.ResolveAsync(uri, activeProfileId, cancellationToken: cancellationToken);
        if (resolution is null)
        {
            CurrentUsage = null;
            CurrentState = FilterState.Neutral;
            OnChanged();
            return;
        }

        SetResolvedState(resolution);
    }

    public async Task SetStateAsync(
        string? uri,
        int? activeProfileId = null,
        FilterLocalBridgeState? localState = null,
        FilterUpdateSource localSource = FilterUpdateSource.LocalBridge,
        CancellationToken cancellationToken = default)
    {
        var resolution = await _resolver.ResolveAsync(uri, activeProfileId, localState, localSource, cancellationToken);
        if (resolution is null)
        {
            return;
        }

        SetResolvedState(resolution);
    }

    public void SetResolvedState(FilterStateResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        if (CurrentUsage is not null && IsSameObservation(CurrentUsage, resolution))
        {
            return;
        }

        CurrentUsage = resolution;
        CurrentState = resolution.State;
        _currentRouteSignature = resolution.RouteSignature;
        if (string.Equals(_pendingRouteSignature, resolution.RouteSignature, StringComparison.Ordinal))
        {
            _pendingRouteSignature = null;
        }

        _history.Add(resolution);
        if (_history.Count > 100)
        {
            _history.RemoveAt(0);
        }

        _logger.LogInformation("GlobalFilterUsage {UsageJson}", JsonSerializer.Serialize(resolution));
        OnChanged();
    }

    public bool TryPrepareNavigation(string? currentUri, string? targetUri)
    {
        var targetSignature = WorkspaceQueryContextHelper.CreateRouteSignature(targetUri);
        if (string.IsNullOrWhiteSpace(targetSignature))
        {
            return false;
        }

        var currentSignature = WorkspaceQueryContextHelper.CreateRouteSignature(currentUri);
        if (string.Equals(currentSignature, targetSignature, StringComparison.Ordinal)
            || string.Equals(_currentRouteSignature, targetSignature, StringComparison.Ordinal)
            || string.Equals(_pendingRouteSignature, targetSignature, StringComparison.Ordinal))
        {
            return false;
        }

        _pendingRouteSignature = targetSignature;
        return true;
    }

    private static bool IsSameObservation(FilterStateResolution left, FilterStateResolution right)
        => left.PageName == right.PageName
           && left.Route == right.Route
           && left.RouteSignature == right.RouteSignature
           && left.HasRouteProductAuthority == right.HasRouteProductAuthority
           && left.HasRouteProjectAuthority == right.HasRouteProjectAuthority
           && left.UsesProduct == right.UsesProduct
           && left.UsesProject == right.UsesProject
           && left.UsesTeam == right.UsesTeam
           && left.UsesTime == right.UsesTime
           && left.Status == right.Status
           && left.State.TeamId == right.State.TeamId
           && left.State.Time == right.State.Time
           && left.MissingTeam == right.MissingTeam
           && left.MissingSprint == right.MissingSprint
           && left.ActiveProfileId == right.ActiveProfileId
           && left.LastUpdateSource == right.LastUpdateSource
           && left.State.ProductIds.SequenceEqual(right.State.ProductIds)
           && left.State.ProjectIds.SequenceEqual(right.State.ProjectIds, StringComparer.Ordinal)
           && left.NormalizationDecisions.SequenceEqual(right.NormalizationDecisions, StringComparer.Ordinal)
           && left.StateIssues.SequenceEqual(right.StateIssues, StringComparer.Ordinal);

    private void OnChanged()
    {
        Changed?.Invoke();
        foreach (var callback in _subscriptions.ToArray())
        {
            callback();
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly List<Action> _subscriptions;
        private readonly Action _callback;

        public Subscription(List<Action> subscriptions, Action callback)
        {
            _subscriptions = subscriptions;
            _callback = callback;
        }

        public void Dispose()
        {
            _subscriptions.Remove(_callback);
        }
    }
}
