using Microsoft.EntityFrameworkCore;
using PoTool.Api.Persistence;
using PoTool.Core.Filters;

namespace PoTool.Api.Services;

internal static class CanonicalFilterDisplayLabelLoader
{
    public static async Task<IReadOnlyDictionary<int, string>> LoadTeamLabelsAsync(
        PoToolDbContext context,
        IEnumerable<int> teamIds,
        CancellationToken cancellationToken)
    {
        var normalizedTeamIds = teamIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        if (normalizedTeamIds.Length == 0)
        {
            return EmptyIntMap;
        }

        return await context.Teams
            .AsNoTracking()
            .Where(team => normalizedTeamIds.Contains(team.Id))
            .OrderBy(team => team.Name)
            .ToDictionaryAsync(team => team.Id, team => team.Name, cancellationToken);
    }

    public static async Task<IReadOnlyDictionary<int, string>> LoadSprintLabelsAsync(
        PoToolDbContext context,
        IEnumerable<int> sprintIds,
        CancellationToken cancellationToken)
    {
        var normalizedSprintIds = sprintIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        if (normalizedSprintIds.Length == 0)
        {
            return EmptyIntMap;
        }

        return await context.Sprints
            .AsNoTracking()
            .Where(sprint => normalizedSprintIds.Contains(sprint.Id))
            .OrderBy(sprint => sprint.StartDateUtc)
            .ThenBy(sprint => sprint.Name)
            .ToDictionaryAsync(sprint => sprint.Id, sprint => sprint.Name, cancellationToken);
    }

    public static IReadOnlyList<int> CollectTeamIds(params FilterSelection<int>[] selections)
        => selections
            .Where(selection => !selection.IsAll)
            .SelectMany(selection => selection.Values)
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

    public static IReadOnlyList<int> CollectSprintIds(params FilterTimeSelection[] selections)
        => selections
            .SelectMany(selection => selection.Mode switch
            {
                FilterTimeSelectionMode.Sprint when selection.SprintId.HasValue => [selection.SprintId.Value],
                FilterTimeSelectionMode.MultiSprint => selection.SprintIds,
                _ => Array.Empty<int>()
            })
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

    private static readonly IReadOnlyDictionary<int, string> EmptyIntMap = new Dictionary<int, string>();
}
