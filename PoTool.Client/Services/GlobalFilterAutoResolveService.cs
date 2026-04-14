using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class GlobalFilterAutoResolveService
{
    // Keep the auto-correction fallback aligned with the existing 180-day rolling-window correction path in shared client filter resolution.
    private const int DefaultRollingDays = 180;
    private const int DefaultRangeWindow = 5;

    private readonly ProductService _productService;
    private readonly TeamService _teamService;
    private readonly SprintService _sprintService;
    private readonly GlobalFilterContextResolver _contextResolver;

    public GlobalFilterAutoResolveService(
        ProductService productService,
        TeamService teamService,
        SprintService sprintService,
        GlobalFilterContextResolver contextResolver)
    {
        _productService = productService;
        _teamService = teamService;
        _sprintService = sprintService;
        _contextResolver = contextResolver;
    }

    public async Task<FilterState?> ResolveAsync(
        FilterStateResolution usage,
        int? activeProfileId,
        FilterUpdateSource updateSource,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usage);

        if (activeProfileId is null)
        {
            return null;
        }

        var correctedState = usage.Status == FilterResolutionStatus.Invalid
            ? BuildCorrectedInvalidState(usage)
            : null;

        var state = correctedState ?? usage.State;
        if (!RequiresDefaultResolution(usage, correctedState))
        {
            return correctedState is not null && correctedState != usage.State
                ? correctedState
                : null;
        }

        var ownedProducts = (await _productService.GetProductsByOwnerAsync(activeProfileId.Value, cancellationToken))
            .OrderBy(product => product.Order)
            .ThenBy(product => product.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var defaultTeamSelection = await ResolveDefaultTeamSelectionAsync(usage, state, ownedProducts, cancellationToken);
        var nextState = state with
        {
            ProductIds = ResolveProductIds(usage, state),
            TeamId = defaultTeamSelection?.TeamId ?? state.TeamId,
            Time = ResolveDefaultTime(state.Time, defaultTeamSelection)
        };

        return nextState == usage.State ? null : nextState;
    }

    private static bool RequiresDefaultResolution(FilterStateResolution usage, FilterState? correctedState)
    {
        if (usage.Status == FilterResolutionStatus.Invalid && correctedState is null)
        {
            return false;
        }

        return usage.MissingTeam || usage.MissingSprint;
    }

    private static FilterState? BuildCorrectedInvalidState(FilterStateResolution usage)
    {
        if (usage.StateIssues.Any(issue => issue.Contains("rolling time selection", StringComparison.OrdinalIgnoreCase)))
        {
            return usage.State with
            {
                Time = new FilterTimeSelection(
                    FilterTimeMode.Rolling,
                    RollingWindow: DefaultRollingDays,
                    RollingUnit: FilterTimeUnit.Days)
            };
        }

        return null;
    }

    private static IReadOnlyList<int> ResolveProductIds(
        FilterStateResolution usage,
        FilterState state)
    {
        if (!state.AllProducts || !usage.UsesProduct || usage.HasRouteProductAuthority)
        {
            return state.ProductIds;
        }

        return Array.Empty<int>();
    }

    private async Task<DefaultTeamSelection?> ResolveDefaultTeamSelectionAsync(
        FilterStateResolution usage,
        FilterState state,
        IReadOnlyList<ProductDto> ownedProducts,
        CancellationToken cancellationToken)
    {
        var requiresTeamContext = usage.MissingTeam || usage.MissingSprint || usage.UsesTeam;
        if (!requiresTeamContext)
        {
            return null;
        }

        var candidateTeamIds = BuildCandidateTeamIds(state, ownedProducts, _contextResolver);
        if (candidateTeamIds.Count == 0)
        {
            if (state.PrimaryProductId.HasValue)
            {
                return null;
            }

            candidateTeamIds = (await _teamService.GetAllTeamsAsync(includeArchived: false, cancellationToken))
                .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                .Select(team => team.Id)
                .ToList();
        }

        foreach (var teamId in candidateTeamIds)
        {
            var sprints = (await _sprintService.GetSprintsForTeamAsync(teamId, cancellationToken))
                .Where(sprint => sprint.StartUtc.HasValue)
                .OrderBy(sprint => sprint.StartUtc)
                .ToList();

            var currentSprint = await _sprintService.GetCurrentSprintForTeamAsync(teamId, cancellationToken)
                ?? sprints.LastOrDefault();
            if (currentSprint is not null)
            {
                return new DefaultTeamSelection(teamId, currentSprint, sprints);
            }

            if (sprints.Count > 0)
            {
                return new DefaultTeamSelection(teamId, sprints[^1], sprints);
            }
        }

        return null;
    }

    private static List<int> BuildCandidateTeamIds(
        FilterState state,
        IReadOnlyList<ProductDto> ownedProducts,
        GlobalFilterContextResolver contextResolver)
    {
        if (state.TeamId.HasValue)
        {
            return [state.TeamId.Value];
        }

        return contextResolver.GetAllowedTeamIds(state.PrimaryProductId, ownedProducts).ToList();
    }

    private static FilterTimeSelection ResolveDefaultTime(
        FilterTimeSelection currentTime,
        DefaultTeamSelection? defaultTeamSelection)
    {
        if (defaultTeamSelection is null)
        {
            return currentTime;
        }

        if (HasValidSprintSelection(currentTime, defaultTeamSelection))
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

    private static bool HasValidSprintSelection(FilterTimeSelection time, DefaultTeamSelection selection)
    {
        if (!time.IsResolved)
        {
            return false;
        }

        var availableSprintIds = (selection.AllSprints ?? [])
            .Select(sprint => sprint.Id)
            .ToHashSet();
        if (availableSprintIds.Count == 0)
        {
            return time.Mode != FilterTimeMode.Range
                || (selection.CurrentSprint is not null
                    && time.StartSprintId == selection.CurrentSprint.Id
                    && time.EndSprintId == selection.CurrentSprint.Id);
        }

        return time.Mode switch
        {
            FilterTimeMode.Sprint => time.SprintId.HasValue && availableSprintIds.Contains(time.SprintId.Value),
            FilterTimeMode.Range => time.StartSprintId.HasValue
                && time.EndSprintId.HasValue
                && availableSprintIds.Contains(time.StartSprintId.Value)
                && availableSprintIds.Contains(time.EndSprintId.Value),
            _ => true
        };
    }

    private sealed record DefaultTeamSelection(int TeamId, SprintDto? CurrentSprint, IReadOnlyList<SprintDto>? AllSprints);
}
