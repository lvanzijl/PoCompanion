using PoTool.Shared.Planning;

namespace PoTool.Client.Models;

public static class ProductPlanningBoardUxText
{
    private static readonly IReadOnlyList<(string From, string To)> VisibleTextReplacements =
    [
        ("Recovered + normalized", "Imported from existing data and cleaned up"),
        ("TFS projection", "TFS reported dates"),
        ("internal intent", "saved plan"),
        ("Internal intent", "Saved plan"),
        ("Recovered", "Imported from existing data"),
        ("recovered", "imported from existing data"),
        ("Projection", "Reported dates"),
        ("projection", "reported dates"),
        ("durable", "saved"),
        ("Durable", "Saved")
    ];

    public const string HeaderSummary = "Product-scoped epic planning with explicit actions and automatically derived parallel work.";
    public const string AuthoritySummary = "This board defines your plan. Dates are written to TFS for reporting.";
    public const string AuthoritySummaryWithBlockingIssues = "This board defines your plan. The dates reported to TFS need attention in the blocking issues below.";

    public static string TranslateVisibleText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var translated = text;
        foreach (var (from, to) in VisibleTextReplacements.OrderByDescending(static replacement => replacement.From.Length))
        {
            translated = translated.Replace(from, to, StringComparison.Ordinal);
        }

        return translated;
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
