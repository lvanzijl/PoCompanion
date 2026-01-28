using PoTool.Core.Metrics.Models;
using PoTool.Shared.Metrics;

namespace PoTool.Core.Metrics.Services;

/// <summary>
/// Service for selecting sprint windows based on dates for Health workspace displays.
/// Replaces lexicographic iteration path sorting with proper date-based selection.
/// Returns fixed-size windows with placeholders for missing future sprints.
/// </summary>
public class SprintWindowSelector
{
    private const int DefaultSprintLengthDays = 14;
    /// <summary>
    /// Gets sprint slots for Backlog Health Analysis section: current + 2 future sprints.
    /// Returns exactly 3 slots total. Missing future slots are filled with placeholders.
    /// </summary>
    /// <param name="allSprints">All available sprints with dates</param>
    /// <param name="today">Reference date for determining current/past/future</param>
    /// <returns>List containing exactly 3 slots: current + 2 future (with placeholders if needed)</returns>
    public IReadOnlyList<SprintSlot> GetBacklogHealthWindow(
        IEnumerable<SprintMetricsDto> allSprints,
        DateTimeOffset today)
    {
        var sprintsList = allSprints.ToList();
        
        // Filter out sprints without dates (can't determine current/past/future)
        var sprintsWithDates = sprintsList
            .Where(s => s.StartDate.HasValue && s.EndDate.HasValue)
            .ToList();
        
        var result = new List<SprintSlot>();
        
        if (sprintsWithDates.Count == 0)
        {
            // No sprints at all - return 3 placeholders
            var baseDate = today;
            for (int i = 0; i < 3; i++)
            {
                var start = baseDate.AddDays(i * DefaultSprintLengthDays);
                var end = start.AddDays(DefaultSprintLengthDays);
                // Use generic message when no sprint data exists
                result.Add(SprintSlot.CreatePlaceholder(start, end, "sprint data unavailable"));
            }
            return result;
        }
        
        // Find current sprint: StartDate <= today < EndDate
        var currentSprint = sprintsWithDates
            .FirstOrDefault(s => s.StartDate!.Value <= today && s.EndDate!.Value > today);
        
        // If no current sprint, use fallback logic
        if (currentSprint == null)
        {
            // Try to use earliest future sprint as "current"
            currentSprint = sprintsWithDates
                .Where(s => s.StartDate!.Value > today)
                .OrderBy(s => s.StartDate!.Value)
                .FirstOrDefault();
            
            // If still no current, use latest past sprint as "current"
            if (currentSprint == null)
            {
                currentSprint = sprintsWithDates
                    .Where(s => s.EndDate!.Value <= today)
                    .OrderByDescending(s => s.EndDate!.Value)
                    .FirstOrDefault();
            }
        }
        
        // Add current sprint slot if found
        if (currentSprint != null)
        {
            result.Add(SprintSlot.FromSprint(currentSprint));
        }
        
        // Get future sprints: StartDate > today, ordered ascending
        // Exclude current sprint if it's a future sprint being used as current
        var futureSprints = sprintsWithDates
            .Where(s => s.StartDate!.Value > today && s != currentSprint)
            .OrderBy(s => s.StartDate!.Value)
            .Take(2) // Next 2 future sprints
            .ToList();
        
        // Add available future sprint slots
        foreach (var futureSprint in futureSprints)
        {
            result.Add(SprintSlot.FromSprint(futureSprint));
        }
        
        // Fill missing future slots with placeholders to reach exactly 3 slots
        var lastSlot = result.LastOrDefault();
        var nextStart = lastSlot != null ? lastSlot.EndDate : today;
        var sprintLength = CalculateAverageSprintLength(sprintsWithDates) ?? DefaultSprintLengthDays;
        
        while (result.Count < 3)
        {
            var start = nextStart;
            var end = start.AddDays(sprintLength);
            result.Add(SprintSlot.CreatePlaceholder(start, end, "newer sprints aren't available"));
            nextStart = end;
        }
        
        return result;
    }
    
    /// <summary>
    /// Legacy method for backward compatibility. Returns SprintMetricsDto list instead of SprintSlot.
    /// Consider using GetBacklogHealthWindow() for placeholder support.
    /// </summary>
    public IReadOnlyList<SprintMetricsDto> GetBacklogHealthSprints(
        IEnumerable<SprintMetricsDto> allSprints,
        DateTimeOffset today)
    {
        var slots = GetBacklogHealthWindow(allSprints, today);
        // Filter out placeholders and extract Sprint objects safely
        return slots
            .Where(slot => !slot.IsPlaceholder && slot.Sprint != null)
            .Select(slot => slot.Sprint)
            .ToList()!;
    }
    
    /// <summary>
    /// Gets sprint slots for Issue Comparison Across Iterations section: 3 past + current + 2 future sprints.
    /// Returns exactly 6 slots total in chronological order. Missing future slots are filled with placeholders.
    /// </summary>
    /// <param name="allSprints">All available sprints with dates</param>
    /// <param name="today">Reference date for determining current/past/future</param>
    /// <returns>List containing exactly 6 slots: 3 past + current + 2 future (with placeholders if needed), chronologically ordered</returns>
    public IReadOnlyList<SprintSlot> GetIssueComparisonWindow(
        IEnumerable<SprintMetricsDto> allSprints,
        DateTimeOffset today)
    {
        var sprintsList = allSprints.ToList();
        
        // Filter out sprints without dates
        var sprintsWithDates = sprintsList
            .Where(s => s.StartDate.HasValue && s.EndDate.HasValue)
            .ToList();
        
        var result = new List<SprintSlot>();
        
        if (sprintsWithDates.Count == 0)
        {
            // No sprints at all - return 6 placeholders with generic message
            var baseDate = today.AddDays(-3 * DefaultSprintLengthDays); // Start 3 sprints ago
            for (int i = 0; i < 6; i++)
            {
                var start = baseDate.AddDays(i * DefaultSprintLengthDays);
                var end = start.AddDays(DefaultSprintLengthDays);
                result.Add(SprintSlot.CreatePlaceholder(start, end, "sprint data unavailable"));
            }
            return result;
        }
        
        // Get past sprints: EndDate <= today, ordered descending (most recent first), take 3
        var pastSprints = sprintsWithDates
            .Where(s => s.EndDate!.Value <= today)
            .OrderByDescending(s => s.EndDate!.Value)
            .Take(3)
            .Reverse() // Reverse to get chronological order (oldest to newest)
            .ToList();
        
        // Add past sprint slots
        foreach (var pastSprint in pastSprints)
        {
            result.Add(SprintSlot.FromSprint(pastSprint));
        }
        
        // Find current sprint: StartDate <= today < EndDate
        var currentSprint = sprintsWithDates
            .FirstOrDefault(s => s.StartDate!.Value <= today && s.EndDate!.Value > today);
        
        // If no current sprint, use fallback logic
        if (currentSprint == null)
        {
            // Try to use earliest future sprint as "current"
            currentSprint = sprintsWithDates
                .Where(s => s.StartDate!.Value > today)
                .OrderBy(s => s.StartDate!.Value)
                .FirstOrDefault();
            
            // If still no current, use latest past sprint as "current"
            // (but only if we haven't already included it in pastSprints)
            if (currentSprint == null)
            {
                var latestPast = sprintsWithDates
                    .Where(s => s.EndDate!.Value <= today)
                    .OrderByDescending(s => s.EndDate!.Value)
                    .FirstOrDefault();
                
                if (latestPast != null && !pastSprints.Contains(latestPast))
                {
                    currentSprint = latestPast;
                }
            }
        }
        
        // Add current sprint slot if found
        if (currentSprint != null)
        {
            result.Add(SprintSlot.FromSprint(currentSprint));
        }
        
        // Get future sprints: StartDate > today, ordered ascending, take 2
        // Exclude current sprint if it's a future sprint being used as current
        var futureSprints = sprintsWithDates
            .Where(s => s.StartDate!.Value > today && s != currentSprint)
            .OrderBy(s => s.StartDate!.Value)
            .Take(2)
            .ToList();
        
        // Add available future sprint slots
        foreach (var futureSprint in futureSprints)
        {
            result.Add(SprintSlot.FromSprint(futureSprint));
        }
        
        // Fill missing future slots with placeholders to reach exactly 6 slots
        var lastSlot = result.LastOrDefault();
        var nextStart = lastSlot != null ? lastSlot.EndDate : today;
        var sprintLength = CalculateAverageSprintLength(sprintsWithDates) ?? DefaultSprintLengthDays;
        
        while (result.Count < 6)
        {
            var start = nextStart;
            var end = start.AddDays(sprintLength);
            result.Add(SprintSlot.CreatePlaceholder(start, end, "newer sprints aren't available"));
            nextStart = end;
        }
        
        return result;
    }
    
    /// <summary>
    /// Legacy method for backward compatibility. Returns SprintMetricsDto list instead of SprintSlot.
    /// Consider using GetIssueComparisonWindow() for placeholder support.
    /// </summary>
    public IReadOnlyList<SprintMetricsDto> GetIssueComparisonSprints(
        IEnumerable<SprintMetricsDto> allSprints,
        DateTimeOffset today)
    {
        var slots = GetIssueComparisonWindow(allSprints, today);
        // Filter out placeholders and extract Sprint objects safely
        return slots
            .Where(slot => !slot.IsPlaceholder && slot.Sprint != null)
            .Select(slot => slot.Sprint)
            .ToList()!;
    }
    
    /// <summary>
    /// Calculates the average sprint length from a list of sprints.
    /// Returns null if no valid sprints with dates exist.
    /// </summary>
    private int? CalculateAverageSprintLength(List<SprintMetricsDto> sprints)
    {
        if (sprints.Count == 0) return null;
        
        var lengths = sprints
            .Where(s => s.StartDate.HasValue && s.EndDate.HasValue)
            .Select(s => (s.EndDate!.Value - s.StartDate!.Value).Days)
            .Where(days => days > 0)
            .ToList();
        
        if (lengths.Count == 0) return null;
        
        return (int)lengths.Average();
    }
}
