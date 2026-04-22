using PoTool.Shared.Settings;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Canonical sprint definitions for the Battleship mock environment.
/// </summary>
public static class BattleshipSprintSeedCatalog
{
    public static IReadOnlyList<TeamIterationDto> CreateTeamIterations(string projectName, DateTimeOffset now)
    {
        return CreateSprintSeeds()
            .Select(seed => new TeamIterationDto(
                seed.Id,
                $"Sprint {seed.SprintNumber}",
                $"\\{projectName}\\Sprint {seed.SprintNumber}",
                seed.StartDateUtc,
                seed.EndDateUtc,
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

    private static IReadOnlyList<BattleshipSprintSeed> CreateSprintSeeds()
        =>
        [
            new BattleshipSprintSeed(3, "iteration-past-03-id", CreateUtc(2025, 12, 29), CreateUtc(2026, 1, 12), "past"),
            new BattleshipSprintSeed(4, "iteration-past-04-id", CreateUtc(2026, 1, 12), CreateUtc(2026, 1, 26), "past"),
            new BattleshipSprintSeed(5, "iteration-past-05-id", CreateUtc(2026, 1, 26), CreateUtc(2026, 2, 9), "past"),
            new BattleshipSprintSeed(6, "iteration-past-06-id", CreateUtc(2026, 2, 9), CreateUtc(2026, 2, 23), "past"),
            new BattleshipSprintSeed(7, "iteration-past-07-id", CreateUtc(2026, 2, 23), CreateUtc(2026, 3, 9), "past"),
            new BattleshipSprintSeed(8, "iteration-past-08-id", CreateUtc(2026, 3, 9), CreateUtc(2026, 3, 23), "past"),
            new BattleshipSprintSeed(9, "iteration-past-09-id", CreateUtc(2026, 3, 23), CreateUtc(2026, 4, 6), "past"),
            new BattleshipSprintSeed(10, "iteration-past-10-id", CreateUtc(2026, 4, 6), CreateUtc(2026, 4, 20), "past"),
            new BattleshipSprintSeed(11, "iteration-current-id", CreateUtc(2026, 4, 20), CreateUtc(2026, 5, 4), "current"),
            new BattleshipSprintSeed(12, "iteration-future-1-id", CreateUtc(2026, 5, 4), CreateUtc(2026, 5, 18), "future"),
            new BattleshipSprintSeed(13, "iteration-future-2-id", CreateUtc(2026, 5, 18), CreateUtc(2026, 6, 1), "future"),
            new BattleshipSprintSeed(14, "iteration-future-3-id", CreateUtc(2026, 6, 1), CreateUtc(2026, 6, 15), "future")
        ];

    private static DateTimeOffset CreateUtc(int year, int month, int day)
        => new(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero);

    private sealed record BattleshipSprintSeed(
        int SprintNumber,
        string Id,
        DateTimeOffset? StartDateUtc,
        DateTimeOffset? EndDateUtc,
        string? TimeFrame);
}
