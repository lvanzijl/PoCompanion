using MudBlazor;

namespace PoTool.Client.Models;

public static class PlanBoardSprintSignalPresentation
{
    public static IReadOnlyList<string> GetSignalDeltaSummaries(PlanningBoardImpactSummary? latestImpactSummary)
        => latestImpactSummary?.SummaryItems.Where(IsSignalDeltaSummary).ToArray() ?? Array.Empty<string>();

    public static IReadOnlyList<string> GetNonSignalSummaries(PlanningBoardImpactSummary? latestImpactSummary)
        => latestImpactSummary?.SummaryItems.Where(static item => !IsSignalDeltaSummary(item)).ToArray() ?? Array.Empty<string>();

    public static string? GetDeltaSummaryForSprint(
        PlanningBoardImpactSummary? latestImpactSummary,
        ProductPlanningSprintColumn sprint)
    {
        ArgumentNullException.ThrowIfNull(sprint);

        return latestImpactSummary?.SummaryItems.FirstOrDefault(summary =>
            summary.StartsWith($"{sprint.Label} ", StringComparison.Ordinal));
    }

    public static bool IsPrimaryExplanationChip(ProductPlanningSprintColumn sprint, int chipIndex)
    {
        ArgumentNullException.ThrowIfNull(sprint);

        return chipIndex == 0 && !IsCalmState(sprint);
    }

    public static Variant GetRiskChipVariant(PlanningBoardSprintRiskLevel riskLevel)
        => riskLevel == PlanningBoardSprintRiskLevel.Low ? Variant.Outlined : Variant.Filled;

    public static Color GetRiskChipColor(PlanningBoardSprintRiskLevel riskLevel)
        => riskLevel switch
        {
            PlanningBoardSprintRiskLevel.High => Color.Error,
            PlanningBoardSprintRiskLevel.Medium => Color.Warning,
            _ => Color.Default
        };

    public static Variant GetConfidenceChipVariant(PlanningBoardSprintConfidenceLevel confidenceLevel)
        => confidenceLevel == PlanningBoardSprintConfidenceLevel.High ? Variant.Text : Variant.Outlined;

    public static Color GetConfidenceChipColor(PlanningBoardSprintConfidenceLevel confidenceLevel)
        => confidenceLevel switch
        {
            PlanningBoardSprintConfidenceLevel.Low => Color.Secondary,
            PlanningBoardSprintConfidenceLevel.Medium => Color.Info,
            _ => Color.Default
        };

    public static Variant GetExplanationChipVariant(ProductPlanningSprintColumn sprint, int chipIndex)
        => IsPrimaryExplanationChip(sprint, chipIndex) ? Variant.Outlined : Variant.Text;

    public static Color GetExplanationChipColor(ProductPlanningSprintColumn sprint, int chipIndex)
    {
        ArgumentNullException.ThrowIfNull(sprint);

        if (!IsPrimaryExplanationChip(sprint, chipIndex))
        {
            return Color.Default;
        }

        return sprint.RiskLevel != PlanningBoardSprintRiskLevel.Low
            ? GetRiskChipColor(sprint.RiskLevel)
            : GetConfidenceChipColor(sprint.ConfidenceLevel);
    }

    public static Color GetDeltaSummaryColor(string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        if (summary.Contains("higher planning strain", StringComparison.OrdinalIgnoreCase) ||
            summary.Contains("closer planning attention", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Warning;
        }

        if (summary.Contains("provisional", StringComparison.OrdinalIgnoreCase) ||
            summary.Contains("less settled", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Info;
        }

        return Color.Default;
    }

    private static bool IsSignalDeltaSummary(string summary)
        => !string.IsNullOrWhiteSpace(summary) && summary.StartsWith("Sprint ", StringComparison.Ordinal);

    private static bool IsCalmState(ProductPlanningSprintColumn sprint)
        => sprint.RiskLevel == PlanningBoardSprintRiskLevel.Low &&
           sprint.ConfidenceLevel == PlanningBoardSprintConfidenceLevel.High;
}
