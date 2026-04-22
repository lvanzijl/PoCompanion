using System.Text.RegularExpressions;
using PoTool.Shared.Planning;

namespace PoTool.Client.Models;

public static class ProductPlanningBoardUxText
{
    private static readonly (string From, string To)[] VisibleTextReplacements = new (string From, string To)[]
    {
        ("Recovered + normalized", "Imported from existing data and cleaned up"),
        ("TFS projection", "TFS reported dates"),
        ("internal intent", "saved plan"),
        ("projection", "reported dates"),
        ("Recovered", "Imported from existing data"),
        ("durable", "saved")
    }
    .OrderByDescending(static replacement => replacement.From.Length)
    .ToArray();

    public const string HeaderSummary = "Product-scoped epic planning with explicit actions and automatically derived parallel work.";
    public const string AuthoritySummary = "This board defines your plan. Dates are written to TFS for reporting.";
    public const string AuthoritySummaryWithBlockingIssues = "This board defines your plan. The dates reported to TFS need attention in the blocking issues below.";
    public const string SprintHeatSummary = "Background color indicates sprint planning strain in the current plan. Color strength indicates how settled that sprint still looks, not delivery certainty.";

    public static string TranslateVisibleText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var translated = text;
        foreach (var (from, to) in VisibleTextReplacements)
        {
            translated = Regex.Replace(
                translated,
                Regex.Escape(from),
                match => MatchReplacementCase(match.Value, to),
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
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

    private static string MatchReplacementCase(string matchedText, string replacement)
    {
        if (string.IsNullOrEmpty(matchedText) || string.IsNullOrEmpty(replacement))
        {
            return replacement;
        }

        if (replacement.StartsWith("TFS ", StringComparison.Ordinal))
        {
            return replacement;
        }

        if (replacement.Length == 1)
        {
            return char.IsUpper(matchedText[0])
                ? replacement
                : char.ToLowerInvariant(replacement[0]).ToString();
        }

        return char.IsUpper(matchedText[0])
            ? replacement
            : $"{char.ToLowerInvariant(replacement[0])}{replacement[1..]}";
    }
}
