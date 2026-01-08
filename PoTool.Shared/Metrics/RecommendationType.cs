namespace PoTool.Shared.Metrics;

/// <summary>
/// Type of recommendation for effort balancing.
/// </summary>
public enum RecommendationType
{
    ReduceTeamLoad = 0,
    IncreaseTeamLoad = 1,
    LevelSprintLoad = 2,
    RedistributeAcrossTeams = 3
}
