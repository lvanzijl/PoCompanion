using PoTool.Shared.Settings;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Canonical sprint definitions for the Battleship mock environment.
/// </summary>
public static class BattleshipSprintSeedCatalog
{
    public static IReadOnlyList<TeamIterationDto> CreateTeamIterations(string projectName, DateTimeOffset now)
    {
        return CreateSprintSeeds(now)
            .Select(seed => new TeamIterationDto(
                seed.Id,
                $"Sprint {seed.SprintNumber}",
                $"\\{projectName}\\Sprint {seed.SprintNumber}",
                seed.StartDateOffsetDays.HasValue ? now.AddDays(seed.StartDateOffsetDays.Value) : null,
                seed.EndDateOffsetDays.HasValue ? now.AddDays(seed.EndDateOffsetDays.Value) : null,
                seed.TimeFrame))
            .ToArray();
    }

    public static IReadOnlyList<string> GetIterationPaths(string projectName)
        => CreateTeamIterations(projectName, DateTimeOffset.UtcNow)
            .Select(static iteration => iteration.Path)
            .ToArray();

    internal static TeamIterationDto? FindTeamIteration(string projectName, DateTimeOffset now, int sprintNumber)
        => CreateTeamIterations(projectName, now)
            .FirstOrDefault(iteration => string.Equals(iteration.Name, $"Sprint {sprintNumber}", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<BattleshipSprintSeed> CreateSprintSeeds(DateTimeOffset now)
        => now switch
        {
            _ =>
            [
                new BattleshipSprintSeed(3, "iteration-past-03-id", -126, -112, "past"),
                new BattleshipSprintSeed(4, "iteration-past-04-id", -112, -98, "past"),
                new BattleshipSprintSeed(5, "iteration-past-05-id", -98, -84, "past"),
                new BattleshipSprintSeed(6, "iteration-past-06-id", -84, -70, "past"),
                new BattleshipSprintSeed(7, "iteration-past-07-id", -70, -56, "past"),
                new BattleshipSprintSeed(8, "iteration-past-08-id", -56, -42, "past"),
                new BattleshipSprintSeed(9, "iteration-past-09-id", -42, -28, "past"),
                new BattleshipSprintSeed(10, "iteration-past-10-id", -28, -14, "past"),
                new BattleshipSprintSeed(11, "iteration-current-id", -7, 7, "current"),
                new BattleshipSprintSeed(12, "iteration-future-1-id", 7, 21, "future"),
                new BattleshipSprintSeed(13, "iteration-future-2-id", 21, 35, "future"),
                new BattleshipSprintSeed(14, "iteration-no-dates-id", null, null, null)
            ]
        };

    private sealed record BattleshipSprintSeed(
        int SprintNumber,
        string Id,
        int? StartDateOffsetDays,
        int? EndDateOffsetDays,
        string? TimeFrame);
}
