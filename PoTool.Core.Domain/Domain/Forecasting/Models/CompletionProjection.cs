namespace PoTool.Core.Domain.Forecasting.Models;

public sealed record CompletionProjection
{
    public CompletionProjection(
        string sprintName,
        string iterationPath,
        DateTimeOffset sprintStartDate,
        DateTimeOffset sprintEndDate,
        double expectedCompletedStoryPoints,
        double remainingStoryPointsAfterSprint,
        double progressPercentage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sprintName);
        ArgumentException.ThrowIfNullOrWhiteSpace(iterationPath);

        if (sprintEndDate < sprintStartDate)
        {
            throw new ArgumentOutOfRangeException(nameof(sprintEndDate), "Sprint end date must be on or after the sprint start date.");
        }

        ForecastingModelValidation.ValidateNonNegative(expectedCompletedStoryPoints, nameof(expectedCompletedStoryPoints), "Expected completed story points");
        ForecastingModelValidation.ValidateNonNegative(remainingStoryPointsAfterSprint, nameof(remainingStoryPointsAfterSprint), "Remaining story points after sprint");
        ForecastingModelValidation.ValidatePercentage(progressPercentage, nameof(progressPercentage), "Progress percentage");

        SprintName = sprintName;
        IterationPath = iterationPath;
        SprintStartDate = sprintStartDate;
        SprintEndDate = sprintEndDate;
        ExpectedCompletedStoryPoints = expectedCompletedStoryPoints;
        RemainingStoryPointsAfterSprint = remainingStoryPointsAfterSprint;
        ProgressPercentage = progressPercentage;
    }

    public string SprintName { get; }

    public string IterationPath { get; }

    public DateTimeOffset SprintStartDate { get; }

    public DateTimeOffset SprintEndDate { get; }

    public double ExpectedCompletedStoryPoints { get; }

    public double RemainingStoryPointsAfterSprint { get; }

    public double ProgressPercentage { get; }
}
