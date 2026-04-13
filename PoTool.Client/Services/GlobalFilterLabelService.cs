using PoTool.Client.Models;

namespace PoTool.Client.Services;

public sealed class GlobalFilterLabelService
{
    private readonly TeamService _teamService;
    private readonly SprintService _sprintService;
    private readonly Dictionary<int, string> _teamNames = new();
    private readonly Dictionary<int, string> _sprintNames = new();
    private readonly HashSet<int> _loadedSprintTeams = new();

    public GlobalFilterLabelService(
        TeamService teamService,
        SprintService sprintService)
    {
        _teamService = teamService;
        _sprintService = sprintService;
    }

    public async Task WarmAsync(FilterState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.TeamId.HasValue)
        {
            await EnsureTeamAsync(state.TeamId.Value, cancellationToken);
            await EnsureSprintsForTeamAsync(state.TeamId.Value, cancellationToken);
        }
    }

    public string FormatTeam(int teamId)
        => _teamNames.TryGetValue(teamId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : teamId.ToString();

    public string FormatTime(FilterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Time.Mode switch
        {
            FilterTimeMode.Snapshot => "Snapshot",
            FilterTimeMode.Sprint => state.Time.SprintId.HasValue ? FormatSprint(state.Time.SprintId.Value) : "Sprint",
            FilterTimeMode.Range => $"{FormatBoundary("from", state.Time.StartSprintId)} → {FormatBoundary("to", state.Time.EndSprintId)}",
            FilterTimeMode.Rolling when state.Time.RollingWindow.HasValue && state.Time.RollingUnit.HasValue
                => $"Rolling {state.Time.RollingWindow.Value} {state.Time.RollingUnit.Value}",
            FilterTimeMode.Rolling => "Rolling",
            _ => state.Time.Mode.ToString()
        };
    }

    private async Task EnsureTeamAsync(int teamId, CancellationToken cancellationToken)
    {
        if (_teamNames.ContainsKey(teamId))
        {
            return;
        }

        var team = await _teamService.GetTeamByIdAsync(teamId, cancellationToken);
        _teamNames[teamId] = string.IsNullOrWhiteSpace(team?.Name)
            ? teamId.ToString()
            : team.Name;
    }

    private async Task EnsureSprintsForTeamAsync(int teamId, CancellationToken cancellationToken)
    {
        if (!_loadedSprintTeams.Add(teamId))
        {
            return;
        }

        var sprints = await _sprintService.GetSprintsForTeamAsync(teamId, cancellationToken);
        foreach (var sprint in sprints)
        {
            _sprintNames[sprint.Id] = string.IsNullOrWhiteSpace(sprint.Name)
                ? sprint.Id.ToString()
                : sprint.Name;
        }
    }

    private string FormatSprint(int sprintId)
        => _sprintNames.TryGetValue(sprintId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : $"Sprint {sprintId}";

    private string FormatBoundary(string label, int? sprintId)
        => sprintId.HasValue ? $"{label} {FormatSprint(sprintId.Value)}" : $"{label} ?";
}
