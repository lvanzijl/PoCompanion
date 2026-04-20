using PoTool.Shared.Planning;

namespace PoTool.Client.Models;

public static class ProductPlanningBoardUxText
{
    public const string HeaderSummary = "Product-scoped epic planning with explicit actions and automatically derived parallel work.";
    public const string AuthoritySummary = "This board defines your plan. Dates are written to TFS for reporting.";

    public static string TranslateVisibleText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text
            .Replace("TFS projection", "TFS reported dates", StringComparison.Ordinal)
            .Replace("Projection", "Reported dates", StringComparison.Ordinal)
            .Replace("projection", "reported dates", StringComparison.Ordinal)
            .Replace("internal intent", "saved plan", StringComparison.Ordinal)
            .Replace("Internal intent", "Saved plan", StringComparison.Ordinal)
            .Replace("Recovered + normalized", "Imported from existing data and cleaned up", StringComparison.Ordinal)
            .Replace("Recovered", "Imported from existing data", StringComparison.Ordinal)
            .Replace("recovered", "imported from existing data", StringComparison.Ordinal)
            .Replace("durable", "saved", StringComparison.Ordinal)
            .Replace("Durable", "Saved", StringComparison.Ordinal);
    }

    public static string GetIntentSourceLabel(PlanningBoardEpicItemDto epic)
        => epic.IntentSource switch
        {
            PlanningBoardIntentSource.Authored => "Planned here",
            PlanningBoardIntentSource.Recovered when epic.RecoveryStatus == ProductPlanningRecoveryStatus.RecoveredWithNormalization
                => "Imported from existing data and cleaned up",
            PlanningBoardIntentSource.Recovered => "Imported from existing data",
            _ => epic.TrackIndex == 0 ? "In main plan" : $"In parallel lane {epic.TrackIndex}"
        };

    public static string GetDriftLabel(PlanningBoardDriftStatus driftStatus)
        => driftStatus switch
        {
            PlanningBoardDriftStatus.MissingTfsDates => "TFS dates missing",
            PlanningBoardDriftStatus.TfsProjectionMismatch => "Out of sync with TFS",
            PlanningBoardDriftStatus.LegacyInvalidTfsDates => "TFS dates need review",
            PlanningBoardDriftStatus.CalendarResolutionFailure => "Sprint calendar blocked",
            PlanningBoardDriftStatus.InsufficientFutureSprintCoverage => "Limited future sprint coverage",
            _ => "In sync"
        };
}
