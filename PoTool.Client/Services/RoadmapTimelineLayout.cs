using PoTool.Shared.Metrics;

namespace PoTool.Client.Services;

/// <summary>
/// Builds a shared-axis timeline layout for roadmap epics using persisted forecast data only.
/// </summary>
public static class RoadmapTimelineLayout
{
    private const int SprintDurationDays = 14;
    private const int DefaultForecastWindowDays = SprintDurationDays * 2;

    public static RoadmapTimelineModel Build(
        IReadOnlyList<RoadmapTimelineEpicInput> epics,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(epics);

        var rows = epics
            .OrderBy(epic => epic.RoadmapOrder)
            .Select(BuildRow)
            .ToList();

        var visibleBars = rows
            .Where(row => row.HasTimelineBar && row.StartDate.HasValue && row.EndDate.HasValue)
            .ToList();

        if (visibleBars.Count == 0)
        {
            return new RoadmapTimelineModel(null, null, rows);
        }

        var axisStart = visibleBars.Min(row => row.StartDate!.Value);
        var axisEnd = visibleBars.Max(row => row.EndDate!.Value);
        if (axisEnd <= axisStart)
        {
            axisEnd = axisStart.AddDays(1);
        }

        var axisDays = Math.Max(1d, (axisEnd - axisStart).TotalDays);
        var positionedRows = rows
            .Select(row => PositionRow(row, axisStart, axisDays))
            .ToList();

        return new RoadmapTimelineModel(axisStart, axisEnd, positionedRows);
    }

    private static RoadmapTimelineRow BuildRow(RoadmapTimelineEpicInput epic)
    {
        var hasTimelineBar = epic.HasForecast && epic.EstimatedCompletionDate.HasValue;
        if (!hasTimelineBar)
        {
            return new RoadmapTimelineRow(
                epic.EpicId,
                epic.Title,
                epic.RoadmapOrder,
                false,
                null,
                null,
                null,
                null,
                epic.IsDelayed,
                epic.Confidence == ForecastConfidence.Low,
                true);
        }

        var endDate = NormalizeDateToUtcMidnight(epic.EstimatedCompletionDate!.Value);
        var durationDays = epic.SprintsRemaining.HasValue
            ? Math.Max(0, epic.SprintsRemaining.Value) * SprintDurationDays
            : DefaultForecastWindowDays;
        var startDate = endDate.AddDays(-durationDays);

        if (startDate > endDate)
        {
            startDate = endDate;
        }

        return new RoadmapTimelineRow(
            epic.EpicId,
            epic.Title,
            epic.RoadmapOrder,
            true,
            startDate,
            endDate,
            null,
            null,
            epic.IsDelayed,
            epic.Confidence == ForecastConfidence.Low,
            false);
    }

    private static RoadmapTimelineRow PositionRow(
        RoadmapTimelineRow row,
        DateTimeOffset axisStart,
        double axisDays)
    {
        if (!row.HasTimelineBar || !row.StartDate.HasValue || !row.EndDate.HasValue)
        {
            return row;
        }

        var offsetDays = Math.Max(0d, (row.StartDate.Value - axisStart).TotalDays);
        var widthDays = Math.Max(0d, (row.EndDate.Value - row.StartDate.Value).TotalDays);
        var leftPercent = offsetDays / axisDays * 100d;
        var widthPercent = Math.Max(widthDays / axisDays * 100d, 2d);

        return row with
        {
            LeftPercent = leftPercent,
            WidthPercent = widthPercent
        };
    }

    private static DateTimeOffset NormalizeDateToUtcMidnight(DateTimeOffset value)
        => new(value.UtcDateTime.Date, TimeSpan.Zero);
}

public sealed record RoadmapTimelineEpicInput(
    int EpicId,
    string Title,
    int RoadmapOrder,
    DateTimeOffset? EstimatedCompletionDate,
    int? SprintsRemaining,
    ForecastConfidence? Confidence,
    bool HasForecast,
    bool IsDelayed);

public sealed record RoadmapTimelineRow(
    int EpicId,
    string Title,
    int RoadmapOrder,
    bool HasTimelineBar,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    double? LeftPercent,
    double? WidthPercent,
    bool IsDelayed,
    bool IsLowConfidence,
    bool IsForecastMissing);

public sealed record RoadmapTimelineModel(
    DateTimeOffset? AxisStart,
    DateTimeOffset? AxisEnd,
    IReadOnlyList<RoadmapTimelineRow> Rows);
