using PoTool.Shared.Settings;

namespace PoTool.Client.Services;

public static class SprintCadenceResolver
{
    /// <summary>
    /// Uses up to the last 5 completed sprints to smooth cadence noise while staying close to recent team behavior.
    /// </summary>
    public const int CadenceSampleSize = 5;

    /// <summary>
    /// Last-resort UI fallback used only when no valid completed sprint history and no valid current sprint are available.
    /// This preserves the existing visual behavior without recalculating forecast data.
    /// </summary>
    public const double DefaultFallbackSprintDurationDays = 14d;

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
                validCompletedSprints.Count,
                SprintCadenceSource.CompletedSprintAverage);
        }

        var currentSprint = sprints.FirstOrDefault(sprint => IsValidCurrentSprint(sprint, referenceTime));
        if (currentSprint is not null)
        {
            return new SprintCadenceInfo(
                (currentSprint.EndUtc!.Value - currentSprint.StartUtc!.Value).TotalDays,
                1,
                SprintCadenceSource.CurrentSprintFallback);
        }

        return new SprintCadenceInfo(
            DefaultFallbackSprintDurationDays,
            0,
            SprintCadenceSource.DefaultFallback);
    }

    private static bool IsValidCompletedSprint(SprintDto sprint, DateTimeOffset referenceTime)
        => sprint.StartUtc.HasValue
           && sprint.EndUtc.HasValue
           && sprint.EndUtc.Value > sprint.StartUtc.Value
           && sprint.EndUtc.Value < referenceTime;

    private static bool IsValidCurrentSprint(SprintDto sprint, DateTimeOffset referenceTime)
        => sprint.StartUtc.HasValue
           && sprint.EndUtc.HasValue
           && sprint.EndUtc.Value > sprint.StartUtc.Value
           && (string.Equals(sprint.TimeFrame, "current", StringComparison.OrdinalIgnoreCase)
               || (sprint.StartUtc.Value <= referenceTime && sprint.EndUtc.Value >= referenceTime));
}

public sealed record SprintCadenceInfo(
    double DurationDays,
    int SampleCount,
    SprintCadenceSource Source)
{
    public bool UsesFallback => Source != SprintCadenceSource.CompletedSprintAverage;

    public bool UsesDefaultDuration => Source == SprintCadenceSource.DefaultFallback;

    public string TooltipText => Source switch
    {
        SprintCadenceSource.CompletedSprintAverage => $"Duration based on avg of last {SampleCount} completed sprints",
        SprintCadenceSource.CurrentSprintFallback => "Duration based on current sprint",
        _ => "Duration based on default sprint duration"
    };
}

public enum SprintCadenceSource
{
    CompletedSprintAverage,
    CurrentSprintFallback,
    DefaultFallback
}
