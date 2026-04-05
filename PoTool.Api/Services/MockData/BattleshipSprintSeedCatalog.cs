using PoTool.Shared.Settings;

namespace PoTool.Api.Services.MockData;

/// <summary>
/// Canonical sprint definitions for the Battleship mock environment.
/// </summary>
public static class BattleshipSprintSeedCatalog
{
    public static IReadOnlyList<TeamIterationDto> CreateTeamIterations(string projectName, DateTimeOffset now)
    {
        return
        [
            new TeamIterationDto(
                "iteration-past-id",
                "Sprint 10",
                $"\\{projectName}\\Sprint 10",
                now.AddDays(-28),
                now.AddDays(-14),
                "past"),
            new TeamIterationDto(
                "iteration-current-id",
                "Sprint 11",
                $"\\{projectName}\\Sprint 11",
                now.AddDays(-7),
                now.AddDays(7),
                "current"),
            new TeamIterationDto(
                "iteration-future-1-id",
                "Sprint 12",
                $"\\{projectName}\\Sprint 12",
                now.AddDays(7),
                now.AddDays(21),
                "future"),
            new TeamIterationDto(
                "iteration-future-2-id",
                "Sprint 13",
                $"\\{projectName}\\Sprint 13",
                now.AddDays(21),
                now.AddDays(35),
                "future"),
            new TeamIterationDto(
                "iteration-no-dates-id",
                "Sprint 14",
                $"\\{projectName}\\Sprint 14",
                null,
                null,
                null)
        ];
    }

    public static IReadOnlyList<string> GetIterationPaths(string projectName)
        => CreateTeamIterations(projectName, DateTimeOffset.UtcNow)
            .Select(static iteration => iteration.Path)
            .ToArray();
}
