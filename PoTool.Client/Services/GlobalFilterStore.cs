using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class GlobalFilterStore
{
    private readonly ILogger<GlobalFilterStore> _logger;
    private readonly List<GlobalFilterUsageReport> _history = new();

    public GlobalFilterStore(ILogger<GlobalFilterStore> logger)
    {
        _logger = logger;
    }

    public GlobalFilterState CurrentState { get; private set; } = GlobalFilterState.Neutral;

    public GlobalFilterUsageReport? CurrentUsage { get; private set; }

    public event Action? Changed;

    public IReadOnlyList<GlobalFilterUsageReport> History => _history;

    public void TrackNavigation(string? uri, int? activeProfileId = null)
    {
        if (!GlobalFilterPageCatalog.TryCreateUsageReport(uri, activeProfileId, out var usage) || usage is null)
        {
            CurrentUsage = null;
            CurrentState = GlobalFilterState.Neutral;
            OnChanged();
            return;
        }

        ReportUsage(usage);
    }

    public void ReportUsage(GlobalFilterUsageReport usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        if (CurrentUsage is not null && IsSameObservation(CurrentUsage, usage))
        {
            CurrentUsage = usage;
            CurrentState = new GlobalFilterState(
                usage.ProductIds,
                usage.ProjectAliases,
                usage.TeamId,
                usage.TimeMode,
                usage.TimeValue);
            OnChanged();
            return;
        }

        CurrentUsage = usage;
        CurrentState = new GlobalFilterState(
            usage.ProductIds,
            usage.ProjectAliases,
            usage.TeamId,
            usage.TimeMode,
            usage.TimeValue);

        _history.Add(usage);
        if (_history.Count > 100)
        {
            _history.RemoveAt(0);
        }

        _logger.LogInformation("GlobalFilterUsage {UsageJson}", JsonSerializer.Serialize(usage));
        OnChanged();
    }

    private static bool IsSameObservation(GlobalFilterUsageReport left, GlobalFilterUsageReport right)
        => left.PageName == right.PageName
           && left.Route == right.Route
           && left.UsesProduct == right.UsesProduct
           && left.UsesProject == right.UsesProject
           && left.UsesTeam == right.UsesTeam
           && left.UsesTime == right.UsesTime
           && left.TeamId == right.TeamId
           && left.TimeMode == right.TimeMode
           && left.TimeValue == right.TimeValue
           && left.MissingTeam == right.MissingTeam
           && left.MissingSprint == right.MissingSprint
           && left.ActiveProfileId == right.ActiveProfileId
           && left.ProductIds.SequenceEqual(right.ProductIds)
           && left.ProjectAliases.SequenceEqual(right.ProjectAliases, StringComparer.Ordinal);

    private void OnChanged() => Changed?.Invoke();
}
