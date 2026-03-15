namespace PoTool.Core.Domain.DeliveryTrends.Models;

/// <summary>
/// Represents canonical epic progress derived from child feature rollups.
/// </summary>
public sealed record EpicProgress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EpicProgress"/> class.
    /// </summary>
    public EpicProgress(
        int epicId,
        string epicTitle,
        int productId,
        int progressPercent,
        double totalScopeStoryPoints,
        double deliveredStoryPoints,
        int featureCount,
        int doneFeatureCount,
        int donePbiCount,
        bool isDone,
        double sprintDeliveredStoryPoints,
        ProgressionDelta sprintProgressionDelta,
        int sprintEffortDelta,
        int sprintCompletedPbiCount,
        int sprintCompletedFeatureCount)
    {
        DeliveryTrendModelValidation.ValidatePositiveId(epicId, nameof(epicId), "Epic ID");
        DeliveryTrendModelValidation.ValidatePositiveId(productId, nameof(productId), "Product ID");
        DeliveryTrendModelValidation.ValidateRequiredText(epicTitle, nameof(epicTitle), "Epic title");
        DeliveryTrendModelValidation.ValidateBoundedPercentage(progressPercent, nameof(progressPercent), "Progress percent");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(totalScopeStoryPoints, nameof(totalScopeStoryPoints), "Total scope story points");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(deliveredStoryPoints, nameof(deliveredStoryPoints), "Delivered story points");
        DeliveryTrendModelValidation.ValidateCount(featureCount, nameof(featureCount), "Feature count");
        DeliveryTrendModelValidation.ValidateCount(doneFeatureCount, nameof(doneFeatureCount), "Done feature count");
        DeliveryTrendModelValidation.ValidateCount(donePbiCount, nameof(donePbiCount), "Done PBI count");
        DeliveryTrendModelValidation.ValidateNonNegativeStoryPoints(sprintDeliveredStoryPoints, nameof(sprintDeliveredStoryPoints), "Sprint delivered story points");
        DeliveryTrendModelValidation.ValidateCount(sprintCompletedPbiCount, nameof(sprintCompletedPbiCount), "Sprint completed PBI count");
        DeliveryTrendModelValidation.ValidateCount(sprintCompletedFeatureCount, nameof(sprintCompletedFeatureCount), "Sprint completed feature count");

        EpicId = epicId;
        EpicTitle = epicTitle;
        ProductId = productId;
        ProgressPercent = progressPercent;
        TotalScopeStoryPoints = totalScopeStoryPoints;
        DeliveredStoryPoints = deliveredStoryPoints;
        FeatureCount = featureCount;
        DoneFeatureCount = doneFeatureCount;
        DonePbiCount = donePbiCount;
        IsDone = isDone;
        SprintDeliveredStoryPoints = sprintDeliveredStoryPoints;
        SprintProgressionDelta = sprintProgressionDelta;
        SprintEffortDelta = sprintEffortDelta;
        SprintCompletedPbiCount = sprintCompletedPbiCount;
        SprintCompletedFeatureCount = sprintCompletedFeatureCount;
    }

    public int EpicId { get; }

    public string EpicTitle { get; }

    public int ProductId { get; }

    public int ProgressPercent { get; }

    public double TotalScopeStoryPoints { get; }

    public double DeliveredStoryPoints { get; }

    public int FeatureCount { get; }

    public int DoneFeatureCount { get; }

    public int DonePbiCount { get; }

    public bool IsDone { get; }

    public double SprintDeliveredStoryPoints { get; }

    public ProgressionDelta SprintProgressionDelta { get; }

    public int SprintEffortDelta { get; }

    public int SprintCompletedPbiCount { get; }

    public int SprintCompletedFeatureCount { get; }
}
