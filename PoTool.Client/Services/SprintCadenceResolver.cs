using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public static class SprintCadenceResolver
{
    /// <summary>
    /// Uses up to the last 5 completed sprints to smooth cadence noise while staying close to recent team behavior.
    /// </summary>
    public const int CadenceSampleSize = 5;

    public static SprintCadenceInfo Resolve(IReadOnlyList<SprintDto> sprints, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(sprints);

        var referenceTime = now ?? DateTimeOffset.UtcNow;
        var validCompletedSprints = sprints
            .Where(sprint => IsValidCompletedSprint(sprint, referenceTime))
            .OrderByDescending(sprint => sprint.EndUtc)
            .Take(CadenceSampleSize)
            .ToList();

        if (validCompletedSprints.Count > 0)
        {
            var averageDurationDays = validCompletedSprints
                .Average(sprint => (sprint.EndUtc!.Value - sprint.StartUtc!.Value).TotalDays);

            return new SprintCadenceInfo(
                averageDurationDays,
                validCompletedSprints.Count);
        }

        return new SprintCadenceInfo(
            null,
            0);
    }

    private static bool IsValidCompletedSprint(SprintDto sprint, DateTimeOffset referenceTime)
        => sprint.StartUtc.HasValue
           && sprint.EndUtc.HasValue
           && sprint.EndUtc.Value > sprint.StartUtc.Value
           && sprint.EndUtc.Value < referenceTime;

}

public sealed record SprintCadenceInfo(
    double? DurationDays,
    int SampleCount)
{
    public bool IsResolved => DurationDays.HasValue;

    public string TooltipText => IsResolved
        ? $"Duration based on avg of last {SampleCount} completed sprints"
        : "Sprint cadence unavailable because no completed sprint history is available.";
}
