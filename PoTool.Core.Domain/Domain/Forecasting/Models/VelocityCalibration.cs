namespace PoTool.Core.Domain.Forecasting.Models;

public sealed record VelocityCalibrationSample
{
    public VelocityCalibrationSample(
        string sprintName,
        double plannedStoryPoints,
        double derivedStoryPoints,
        double completedStoryPoints,
        int completedEffort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sprintName);
        ForecastingModelValidation.ValidateNonNegative(plannedStoryPoints, nameof(plannedStoryPoints), "Planned story points");
        ForecastingModelValidation.ValidateNonNegative(derivedStoryPoints, nameof(derivedStoryPoints), "Derived story points");
        ForecastingModelValidation.ValidateNonNegative(completedStoryPoints, nameof(completedStoryPoints), "Completed story points");
        ForecastingModelValidation.ValidateCount(completedEffort, nameof(completedEffort), "Completed effort");

        SprintName = sprintName;
        PlannedStoryPoints = plannedStoryPoints;
        DerivedStoryPoints = derivedStoryPoints;
        CompletedStoryPoints = completedStoryPoints;
        CompletedEffort = completedEffort;
    }

    public string SprintName { get; }

    public double PlannedStoryPoints { get; }

    public double DerivedStoryPoints { get; }

    public double CompletedStoryPoints { get; }

    public int CompletedEffort { get; }
}

public sealed record VelocityCalibrationEntry
{
    public VelocityCalibrationEntry(
        string sprintName,
        double committedStoryPoints,
        double deliveredStoryPoints,
        int deliveredEffort,
        double hoursPerStoryPoint,
        double predictabilityRatio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sprintName);
        ForecastingModelValidation.ValidateNonNegative(committedStoryPoints, nameof(committedStoryPoints), "Committed story points");
        ForecastingModelValidation.ValidateNonNegative(deliveredStoryPoints, nameof(deliveredStoryPoints), "Delivered story points");
        ForecastingModelValidation.ValidateCount(deliveredEffort, nameof(deliveredEffort), "Delivered effort");
        ForecastingModelValidation.ValidateNonNegative(hoursPerStoryPoint, nameof(hoursPerStoryPoint), "Hours per story point");
        ForecastingModelValidation.ValidateNonNegative(predictabilityRatio, nameof(predictabilityRatio), "Predictability ratio");

        SprintName = sprintName;
        CommittedStoryPoints = committedStoryPoints;
        DeliveredStoryPoints = deliveredStoryPoints;
        DeliveredEffort = deliveredEffort;
        HoursPerStoryPoint = hoursPerStoryPoint;
        PredictabilityRatio = predictabilityRatio;
    }

    public string SprintName { get; }

    public double CommittedStoryPoints { get; }

    public double DeliveredStoryPoints { get; }

    public int DeliveredEffort { get; }

    public double HoursPerStoryPoint { get; }

    public double PredictabilityRatio { get; }
}

public sealed record VelocityCalibration
{
    public VelocityCalibration(
        IReadOnlyList<VelocityCalibrationEntry> entries,
        double medianVelocity,
        double p25Velocity,
        double p75Velocity,
        double medianPredictability,
        IReadOnlyList<string> outlierSprintNames)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(outlierSprintNames);
        ForecastingModelValidation.ValidateNonNegative(medianVelocity, nameof(medianVelocity), "Median velocity");
        ForecastingModelValidation.ValidateNonNegative(p25Velocity, nameof(p25Velocity), "P25 velocity");
        ForecastingModelValidation.ValidateNonNegative(p75Velocity, nameof(p75Velocity), "P75 velocity");
        ForecastingModelValidation.ValidateNonNegative(medianPredictability, nameof(medianPredictability), "Median predictability");
        ForecastingModelValidation.ValidateQuartiles(p25Velocity, medianVelocity, p75Velocity);

        Entries = entries;
        MedianVelocity = medianVelocity;
        P25Velocity = p25Velocity;
        P75Velocity = p75Velocity;
        MedianPredictability = medianPredictability;
        OutlierSprintNames = outlierSprintNames;
    }

    public IReadOnlyList<VelocityCalibrationEntry> Entries { get; }

    public double MedianVelocity { get; }

    public double P25Velocity { get; }

    public double P75Velocity { get; }

    public double MedianPredictability { get; }

    public IReadOnlyList<string> OutlierSprintNames { get; }
}
