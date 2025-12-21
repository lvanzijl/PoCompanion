using System.Text;
using PoTool.Client.ApiClient;

namespace PoTool.Client.Services;

/// <summary>
/// Service for generating summary reports from work items.
/// </summary>
public class ReportService
{
    /// <summary>
    /// Generates a summary report for the selected work items.
    /// </summary>
    /// <param name="workItems">The work items to include in the report.</param>
    /// <returns>A formatted markdown report.</returns>
    public string GenerateSummaryReport(IEnumerable<WorkItemDto> workItems)
    {
        if (workItems == null || !workItems.Any())
            return "No work items selected.";

        var items = workItems.ToList();
        var sb = new StringBuilder();
        
        AppendHeader(sb, items.Count);
        AppendSummaryByType(sb, items);
        AppendSummaryByState(sb, items);
        AppendEffortSummary(sb, items);
        AppendSummaryByAreaPath(sb, items);
        AppendDetailedList(sb, items);
        
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, int totalCount)
    {
        sb.AppendLine("# Work Items Summary Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Total Items:** {totalCount}");
        sb.AppendLine();
    }

    private static void AppendSummaryByType(StringBuilder sb, List<WorkItemDto> items)
    {
        sb.AppendLine("## Summary by Type");
        sb.AppendLine();
        
        var typeGroups = items.GroupBy(w => w.Type ?? "Unknown")
            .OrderBy(g => g.Key);
        
        foreach (var group in typeGroups)
        {
            sb.AppendLine($"- **{group.Key}:** {group.Count()} items");
        }
        sb.AppendLine();
    }

    private static void AppendSummaryByState(StringBuilder sb, List<WorkItemDto> items)
    {
        sb.AppendLine("## Summary by State");
        sb.AppendLine();
        
        var stateGroups = items.GroupBy(w => w.State ?? "Unknown")
            .OrderBy(g => g.Key);
        
        foreach (var group in stateGroups)
        {
            sb.AppendLine($"- **{group.Key}:** {group.Count()} items");
        }
        sb.AppendLine();
    }

    private static void AppendEffortSummary(StringBuilder sb, List<WorkItemDto> items)
    {
        var itemsWithEffort = items.Where(w => w.Effort.HasValue).ToList();
        if (!itemsWithEffort.Any())
            return;

        sb.AppendLine("## Effort Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Items with Effort:** {itemsWithEffort.Count} of {items.Count}");
        sb.AppendLine($"- **Total Effort:** {itemsWithEffort.Sum(w => w.Effort!.Value)} hours");
        sb.AppendLine($"- **Average Effort:** {itemsWithEffort.Average(w => w.Effort!.Value):F2} hours");
        sb.AppendLine();
    }

    private static void AppendSummaryByAreaPath(StringBuilder sb, List<WorkItemDto> items)
    {
        sb.AppendLine("## Summary by Area Path");
        sb.AppendLine();
        
        var areaGroups = items.GroupBy(w => w.AreaPath ?? "Unknown")
            .OrderByDescending(g => g.Count())
            .Take(10); // Top 10 area paths
        
        foreach (var group in areaGroups)
        {
            sb.AppendLine($"- **{group.Key}:** {group.Count()} items");
        }
        sb.AppendLine();
    }

    private static void AppendDetailedList(StringBuilder sb, List<WorkItemDto> items)
    {
        sb.AppendLine("## Detailed List");
        sb.AppendLine();
        sb.AppendLine("| ID | Type | State | Title | Effort |");
        sb.AppendLine("|---|---|---|---|---|");
        
        foreach (var item in items.OrderBy(w => w.TfsId))
        {
            var effort = item.Effort.HasValue ? $"{item.Effort.Value}h" : "-";
            sb.AppendLine($"| {item.TfsId} | {item.Type} | {item.State} | {EscapeMarkdown(item.Title)} | {effort} |");
        }
    }

    /// <summary>
    /// Escapes special markdown characters in text.
    /// </summary>
    private static string EscapeMarkdown(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text.Replace("|", "\\|")
                   .Replace("\n", " ")
                   .Replace("\r", string.Empty);
    }
}
