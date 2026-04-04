using PoTool.Shared.BuildQuality;
using PoTool.Shared.PullRequests;

namespace PoTool.Client.Services;

public static class TrendsWorkspaceTileSignalService
{
    public const double PipelineUnstableFailureRateThresholdPercent = 20d;

    public static TrendTileSignal GetPrTrendSignal(IEnumerable<PrSprintMetricsDto>? sprints)
    {
        var orderedValues = sprints?
            .OrderBy(sprint => sprint.StartUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(sprint => sprint.SprintId)
            .Select(sprint => sprint.MedianTimeToMergeHours);

        return BuildTrendSignal(
            orderedValues,
            value => value.HasValue && value.Value > 0,
            insufficientTooltip: "Need at least two usable sprint medians to show the PR trend.",
            noDataTooltip: "No usable PR trend data is available for the selected sprint range.");
    }

    public static TrendTileSignal GetBugTrendSignal(IEnumerable<KeyValuePair<string, int>>? addedSeries)
    {
        var orderedValues = addedSeries?
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .TakeLast(2)
            .Select(entry => (double?)entry.Value);

        return BuildTrendSignal(
            orderedValues,
            value => value.HasValue,
            insufficientTooltip: "Need at least two recent periods to show the bug trend.",
            noDataTooltip: "No bug trend data is available for the selected period.");
    }

    public static StatusTileSignal GetPipelineSignal(
        BuildQualityPageDto? buildQualityPage,
        int? selectedProductId,
        double unstableFailureRateThresholdPercent = PipelineUnstableFailureRateThresholdPercent)
    {
        var result = selectedProductId.HasValue
            ? buildQualityPage?.Products?.FirstOrDefault(product => product.ProductId == selectedProductId.Value)?.Result
            : buildQualityPage?.Summary;

        if (result?.Metrics is null || result.Evidence is null)
        {
            return StatusTileSignal.NoData("No pipeline quality data is available for the selected range.");
        }

        if (result.Evidence.SuccessRateUnknown || !result.Metrics.SuccessRate.HasValue || result.Evidence.EligibleBuilds <= 0)
        {
            return StatusTileSignal.NoData("No pipeline success rate is available for the selected range.");
        }

        var failureRatePercent = Math.Max(0d, (1d - result.Metrics.SuccessRate.Value) * 100d);
        return failureRatePercent > unstableFailureRateThresholdPercent
            ? StatusTileSignal.Unstable(failureRatePercent, result.Evidence.EligibleBuilds)
            : StatusTileSignal.Stable(failureRatePercent, result.Evidence.EligibleBuilds);
    }

    private static TrendTileSignal BuildTrendSignal(
        IEnumerable<double?>? values,
        Func<double?, bool> isUsable,
        string insufficientTooltip,
        string noDataTooltip)
    {
        if (values is null)
        {
            return TrendTileSignal.WithFallback(StatusTileSignal.NoData(noDataTooltip));
        }

        var usableValues = values
            .Where(isUsable)
            .Select(value => value!.Value)
            .ToList();

        if (usableValues.Count >= 2)
        {
            return TrendTileSignal.Ready(usableValues.First(), usableValues.Last());
        }

        return usableValues.Count == 1
            ? TrendTileSignal.WithFallback(StatusTileSignal.InsufficientData(insufficientTooltip))
            : TrendTileSignal.WithFallback(StatusTileSignal.NoData(noDataTooltip));
    }
}

public sealed record TrendTileSignal(
    double? StartValue,
    double? EndValue,
    StatusTileSignal? FallbackSignal)
{
    public bool HasTrendValues => StartValue.HasValue && EndValue.HasValue;

    public static TrendTileSignal Ready(double startValue, double endValue)
    {
        return new(startValue, endValue, FallbackSignal: null);
    }

    public static TrendTileSignal WithFallback(StatusTileSignal fallbackSignal)
    {
        return new(StartValue: null, EndValue: null, fallbackSignal);
    }
}

public sealed record StatusTileSignal(
    TileSignalKind Kind,
    string Label,
    string Tooltip,
    TileSignalTone Tone)
{
    public static StatusTileSignal Stable(double failureRatePercent, int eligibleBuilds)
    {
        return new(
            TileSignalKind.Stable,
            "Stable",
            $"Failure rate is {failureRatePercent:F1}% across {eligibleBuilds} eligible {PluralizeBuilds(eligibleBuilds)}.",
            TileSignalTone.Neutral);
    }

    public static StatusTileSignal Unstable(double failureRatePercent, int eligibleBuilds)
    {
        return new(
            TileSignalKind.Unstable,
            "Unstable",
            $"Failure rate is {failureRatePercent:F1}% across {eligibleBuilds} eligible {PluralizeBuilds(eligibleBuilds)}.",
            TileSignalTone.Negative);
    }

    public static StatusTileSignal InsufficientData(string tooltip)
    {
        return new(TileSignalKind.InsufficientData, "Insufficient data", tooltip, TileSignalTone.Neutral);
    }

    public static StatusTileSignal NotReady(string tooltip)
    {
        return new(TileSignalKind.NotReady, "Data not ready", tooltip, TileSignalTone.Neutral);
    }

    public static StatusTileSignal Failed(string tooltip)
    {
        return new(TileSignalKind.Failed, "Data unavailable", tooltip, TileSignalTone.Negative);
    }

    public static StatusTileSignal NoData(string tooltip)
    {
        return new(TileSignalKind.NoData, "No data", tooltip, TileSignalTone.Neutral);
    }

    private static string PluralizeBuilds(int eligibleBuilds)
    {
        return eligibleBuilds == 1 ? "build" : "builds";
    }
}

public enum TileSignalKind
{
    Stable,
    Unstable,
    InsufficientData,
    NotReady,
    Failed,
    NoData
}

public enum TileSignalTone
{
    Neutral,
    Negative
}
