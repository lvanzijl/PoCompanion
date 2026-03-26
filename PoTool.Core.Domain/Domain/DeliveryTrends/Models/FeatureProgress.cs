namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Represents canonical feature progress derived from child PBIs.
/// </summary>
public sealed record FeatureProgress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureProgress"/> class.
    /// </summary>
    public FeatureProgress(
        int featureId,
        string featureTitle,
        int productId,
        int? epicId,
        string? epicTitle,
        int progressPercent,
        double totalScopeStoryPoints,
        double deliveredStoryPoints,
        int donePbiCount,
        bool isDone,
        double sprintDeliveredStoryPoints,
        ProgressionDelta sprintProgressionDelta,
        int sprintEffortDelta,
        int sprintCompletedPbiCount,
        bool sprintCompletedInSprint,
        double? calculatedProgress = null,
        double? overrideProgress = null,
        double? effectiveProgress = null,
        IReadOnlyList<string>? validationSignals = null,
        double? forecastConsumedEffort = null,
        double? forecastRemainingEffort = null,
        double weight = 0,
        bool isExcluded = false)
    {
        DeliveryTrendModelValidation.ValidatePositiveId(featureId, nameof(featureId), "Feature ID");
        DeliveryTrendModelValidation.ValidatePositiveId(productId, nameof(productId), "Product ID");
        DeliveryTrendModelValidation.ValidateRequiredText(featureTitle, nameof(featureTitle), "Feature title");

        if (epicId.HasValue)
        {
            DeliveryTrendModelValidation.ValidatePositiveId(epicId.Value, nameof(epicId), "Epic ID");
            DeliveryTrendModelValidation.ValidateRequiredText(epicTitle, nameof(epicTitle), "Epic title");
        }
        else if (!string.IsNullOrWhiteSpace(epicTitle))
        {
            throw new ArgumentException("Epic title cannot be provided without an epic ID.", nameof(epicTitle));
        }

        DeliveryTrendModelValidation.ValidateBoundedPercentage(progressPercent, nameof(progressPercent), "Progress percent");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(totalScopeStoryPoints, nameof(totalScopeStoryPoints), "Total scope story points");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(deliveredStoryPoints, nameof(deliveredStoryPoints), "Delivered story points");
        DeliveryTrendModelValidation.ValidateCount(donePbiCount, nameof(donePbiCount), "Done PBI count");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(sprintDeliveredStoryPoints, nameof(sprintDeliveredStoryPoints), "Sprint delivered story points");
        DeliveryTrendModelValidation.ValidateCount(sprintCompletedPbiCount, nameof(sprintCompletedPbiCount), "Sprint completed PBI count");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(weight, nameof(weight), "Feature weight");

        FeatureId = featureId;
        FeatureTitle = featureTitle;
        ProductId = productId;
        EpicId = epicId;
        EpicTitle = epicTitle;
        ProgressPercent = progressPercent;
        TotalScopeStoryPoints = totalScopeStoryPoints;
        DeliveredStoryPoints = deliveredStoryPoints;
        DonePbiCount = donePbiCount;
        IsDone = isDone;
        SprintDeliveredStoryPoints = sprintDeliveredStoryPoints;
        SprintProgressionDelta = sprintProgressionDelta;
        SprintEffortDelta = sprintEffortDelta;
        SprintCompletedPbiCount = sprintCompletedPbiCount;
        SprintCompletedInSprint = sprintCompletedInSprint;
        CalculatedProgress = calculatedProgress;
        Override = overrideProgress;
        EffectiveProgress = effectiveProgress;
        ValidationSignals = validationSignals ?? Array.Empty<string>();
        ForecastConsumedEffort = forecastConsumedEffort;
        ForecastRemainingEffort = forecastRemainingEffort;
        Weight = weight;
        IsExcluded = isExcluded;
    }

    public int FeatureId { get; }

    public string FeatureTitle { get; }

    public int ProductId { get; }

    public int? EpicId { get; }

    public string? EpicTitle { get; }

    public int ProgressPercent { get; }

    public double TotalScopeStoryPoints { get; }

    public double DeliveredStoryPoints { get; }

    public int DonePbiCount { get; }

    public bool IsDone { get; }

    public double SprintDeliveredStoryPoints { get; }

    public ProgressionDelta SprintProgressionDelta { get; }

    public int SprintEffortDelta { get; }

    public int SprintCompletedPbiCount { get; }

    public bool SprintCompletedInSprint { get; }

    public double? CalculatedProgress { get; }

    public double? Override { get; }

    public double? EffectiveProgress { get; }

    public IReadOnlyList<string> ValidationSignals { get; }

    public double? ForecastConsumedEffort { get; }

    public double? ForecastRemainingEffort { get; }

    public double Weight { get; }

    public bool IsExcluded { get; }
}
