namespace PoTool.Core.Domain.Forecasting.Models;

public enum EffortForecastDirection
{
    Stable,
    Increasing,
    Decreasing,
    Volatile
}

public sealed record EffortDistributionWorkItem
{
    public EffortDistributionWorkItem(string areaPath, string iterationPath, int effort)
    {
        areaPath ??= string.Empty;
        iterationPath ??= string.Empty;
        ForecastingModelValidation.ValidateCount(effort, nameof(effort), "Effort");

        AreaPath = areaPath;
        IterationPath = iterationPath;
        Effort = effort;
    }

    public string AreaPath { get; }

    public string IterationPath { get; }

    public int Effort { get; }
}

public sealed record EffortSprintTrend
{
    public EffortSprintTrend(
        string iterationPath,
        string sprintName,
        int totalEffort,
        int workItemCount,
        double utilizationPercentage,
        double changeFromPrevious,
        EffortForecastDirection direction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iterationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sprintName);
        ForecastingModelValidation.ValidateCount(totalEffort, nameof(totalEffort), "Total effort");
        ForecastingModelValidation.ValidateCount(workItemCount, nameof(workItemCount), "Work item count");
        ForecastingModelValidation.ValidateNonNegative(utilizationPercentage, nameof(utilizationPercentage), "Utilization percentage");

        IterationPath = iterationPath;
        SprintName = sprintName;
        TotalEffort = totalEffort;
        WorkItemCount = workItemCount;
        UtilizationPercentage = utilizationPercentage;
        ChangeFromPrevious = changeFromPrevious;
        Direction = direction;
    }

    public string IterationPath { get; }

    public string SprintName { get; }

    public int TotalEffort { get; }

    public int WorkItemCount { get; }

    public double UtilizationPercentage { get; }

    public double ChangeFromPrevious { get; }

    public EffortForecastDirection Direction { get; }
}

public sealed record EffortAreaPathTrend
{
    public EffortAreaPathTrend(
        string areaPath,
        IReadOnlyList<int> effortBySprint,
        double averageEffort,
        double standardDeviation,
        EffortForecastDirection direction,
        double trendSlope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaPath);
        ArgumentNullException.ThrowIfNull(effortBySprint);
        ForecastingModelValidation.ValidateNonNegative(averageEffort, nameof(averageEffort), "Average effort");
        ForecastingModelValidation.ValidateNonNegative(standardDeviation, nameof(standardDeviation), "Standard deviation");

        AreaPath = areaPath;
        EffortBySprint = effortBySprint;
        AverageEffort = averageEffort;
        StandardDeviation = standardDeviation;
        Direction = direction;
        TrendSlope = trendSlope;
    }

    public string AreaPath { get; }

    public IReadOnlyList<int> EffortBySprint { get; }

    public double AverageEffort { get; }

    public double StandardDeviation { get; }

    public EffortForecastDirection Direction { get; }

    public double TrendSlope { get; }
}

public sealed record EffortDistributionForecast
{
    public EffortDistributionForecast(
        string sprintName,
        int forecastedEffort,
        int lowEstimate,
        int highEstimate,
        double confidenceLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sprintName);
        ForecastingModelValidation.ValidateProbability(confidenceLevel, nameof(confidenceLevel), "Confidence level");

        SprintName = sprintName;
        ForecastedEffort = forecastedEffort;
        LowEstimate = lowEstimate;
        HighEstimate = highEstimate;
        ConfidenceLevel = confidenceLevel;
    }

    public string SprintName { get; }

    public int ForecastedEffort { get; }

    public int LowEstimate { get; }

    public int HighEstimate { get; }

    public double ConfidenceLevel { get; }
}

public sealed record EffortDistributionAnalysis
{
    public EffortDistributionAnalysis(
        IReadOnlyList<EffortSprintTrend> trendBySprint,
        IReadOnlyList<EffortAreaPathTrend> trendByAreaPath,
        EffortForecastDirection overallTrend,
        double trendSlope,
        IReadOnlyList<EffortDistributionForecast> forecasts)
    {
        ArgumentNullException.ThrowIfNull(trendBySprint);
        ArgumentNullException.ThrowIfNull(trendByAreaPath);
        ArgumentNullException.ThrowIfNull(forecasts);

        TrendBySprint = trendBySprint;
        TrendByAreaPath = trendByAreaPath;
        OverallTrend = overallTrend;
        TrendSlope = trendSlope;
        Forecasts = forecasts;
    }

    public IReadOnlyList<EffortSprintTrend> TrendBySprint { get; }

    public IReadOnlyList<EffortAreaPathTrend> TrendByAreaPath { get; }

    public EffortForecastDirection OverallTrend { get; }

    public double TrendSlope { get; }

    public IReadOnlyList<EffortDistributionForecast> Forecasts { get; }
}
